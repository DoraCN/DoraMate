using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;

namespace DoraNode;

/// <summary>
/// Main entry point for creating and interacting with a Dora node.
/// </summary>
public sealed class DoraNode : IDisposable
{
    private const string AsyncReadFailureSimulationEnvVar = "DORA_CSHARP_SIMULATE_NODE_ASYNC_NATIVE_FAILURE";

    private enum NodeReadMode
    {
        None,
        Sync,
        Async
    }

    private sealed class AsyncEventStreamState
    {
        private readonly Channel<DoraEvent> _channel = Channel.CreateUnbounded<DoraEvent>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
            });

        public ChannelReader<DoraEvent> Reader => _channel.Reader;

        public bool Publish(DoraEvent ev)
        {
            ArgumentNullException.ThrowIfNull(ev);
            return _channel.Writer.TryWrite(ev);
        }

        public void Complete(Exception? error = null)
        {
            _channel.Writer.TryComplete(error);
        }
    }

    private readonly object _stateSync = new();
    private readonly IntPtr _context;
    private volatile bool _disposed;
    private NodeReadMode _readMode;
    private AsyncEventStreamState? _asyncEventStream;
    private Task? _asyncPumpTask;
    private int _asyncReadInFlight;
    private int _simulatedAsyncReadFailureTriggered;

    /// <summary>
    /// Captures a structured diagnostics snapshot for DoraNode operations.
    /// </summary>
    public static DoraNodeDiagnosticInfo CaptureDiagnostics(string operation, string? detail = null)
    {
        return DoraNodeDiagnosticInfo.Capture(operation, detail);
    }

    public DoraNode()
    {
        try
        {
            NativeMethods.EnsureLoaded();
            _context = NativeMethods.InitDoraContextFromEnv();
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException)
        {
            throw DoraException.Create(
                "Failed to load Dora native node library.",
                DoraNodeErrorCode.NativeLibraryLoadFailed,
                operation: "InitializeNode",
                innerException: ex);
        }

        if (_context == IntPtr.Zero)
        {
            throw DoraException.Create(
                "Failed to initialize Dora context from environment. Make sure the node is started by the Dora coordinator.",
                DoraNodeErrorCode.ContextInitializationFailed,
                operation: "InitializeNode");
        }
    }

    public DoraEvent? Next()
    {
        ThrowIfDisposed();
        EnsureReadMode(NodeReadMode.Sync);
        return ReadNextEventCore("ReadNextEvent");
    }

    public async ValueTask<DoraEvent?> NextAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (Interlocked.CompareExchange(ref _asyncReadInFlight, 1, 0) != 0)
        {
            throw DoraException.Create(
                "Only one asynchronous DoraNode read may be in flight at a time.",
                DoraNodeErrorCode.LifecycleViolation,
                operation: "ReadNextEventAsync");
        }

        try
        {
            var asyncEventStream = EnsureAsyncEventStreamStarted();
            while (await asyncEventStream.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (asyncEventStream.Reader.TryRead(out var ev))
                {
                    return ev;
                }
            }

            if (_asyncPumpTask is not null)
            {
                await _asyncPumpTask.ConfigureAwait(false);
            }

            return null;
        }
        finally
        {
            Interlocked.Exchange(ref _asyncReadInFlight, 0);
        }
    }

    public async IAsyncEnumerable<DoraEvent> ReadAllEventsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var ev = await NextAsync(cancellationToken).ConfigureAwait(false);
            if (ev is null)
            {
                yield break;
            }

            yield return ev;
        }
    }

    public bool SendOutput(string outputId, byte[] data)
    {
        ThrowIfDisposed();

        if (string.IsNullOrEmpty(outputId))
        {
            throw new ArgumentException("Output ID cannot be null or empty", nameof(outputId));
        }

        data ??= Array.Empty<byte>();
        var idBytes = Encoding.UTF8.GetBytes(outputId);
        var result = NativeMethods.DoraSendOutput(_context, idBytes, (UIntPtr)idBytes.Length, data, (UIntPtr)data.Length);
        return result == 0;
    }

    /// <summary>
    /// Sends output data and throws a diagnostic-rich exception when the send fails.
    /// </summary>
    public void SendOutputOrThrow(string outputId, byte[] data)
    {
        if (!SendOutput(outputId, data))
        {
            throw DoraException.Create(
                $"Failed to send node output '{outputId}'.",
                DoraNodeErrorCode.OutputSendFailed,
                operation: "SendOutput",
                detail: outputId);
        }
    }

    public bool SendOutput(string outputId, string data)
    {
        return SendOutput(outputId, Encoding.UTF8.GetBytes(data));
    }

    /// <summary>
    /// Sends string output data and throws a diagnostic-rich exception when the send fails.
    /// </summary>
    public void SendOutputOrThrow(string outputId, string data)
    {
        SendOutputOrThrow(outputId, Encoding.UTF8.GetBytes(data));
    }

    public bool SendArrow(string outputId, ArrowPayload payload)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(payload);

        if (string.IsNullOrEmpty(outputId))
        {
            throw new ArgumentException("Output ID cannot be null or empty", nameof(outputId));
        }

        var (arrayHandle, schemaHandle) = payload.DetachHandles();
        if (arrayHandle == 0 || schemaHandle == 0)
        {
            if (arrayHandle != 0)
            {
                NativeMethods.FreeArrowArray((IntPtr)arrayHandle);
            }

            if (schemaHandle != 0)
            {
                NativeMethods.FreeArrowSchema((IntPtr)schemaHandle);
            }

            return false;
        }

        var idBytes = Encoding.UTF8.GetBytes(outputId);
        var result = NativeMethods.DoraSendOutputArrow(
            _context,
            idBytes,
            (UIntPtr)idBytes.Length,
            (IntPtr)arrayHandle,
            (IntPtr)schemaHandle);

        return result == 0;
    }

    /// <summary>
    /// Sends an Arrow payload and throws a diagnostic-rich exception when the send fails.
    /// </summary>
    public void SendArrowOrThrow(string outputId, ArrowPayload payload)
    {
        if (!SendArrow(outputId, payload))
        {
            throw DoraException.Create(
                $"Failed to send Arrow payload to node output '{outputId}'.",
                DoraNodeErrorCode.ArrowOutputSendFailed,
                operation: "SendArrow",
                detail: outputId);
        }
    }

    /// <summary>
    /// Sends an Arrow array/schema pair to the specified output ID.
    /// Ownership of both Arrow handles transfers to the native Dora runtime on success.
    /// </summary>
    public bool SendArrow(string outputId, ArrowArray array, ArrowSchema schema)
    {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentNullException.ThrowIfNull(schema);
        return SendArrow(outputId, new ArrowPayload(array, schema));
    }

    /// <summary>
    /// Sends an Arrow array/schema pair and throws a diagnostic-rich exception when the send fails.
    /// </summary>
    public void SendArrowOrThrow(string outputId, ArrowArray array, ArrowSchema schema)
    {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentNullException.ThrowIfNull(schema);
        SendArrowOrThrow(outputId, new ArrowPayload(array, schema));
    }

    internal bool SendRecordBatchIpc(string outputId, byte[] ipcBytes)
    {
        ThrowIfDisposed();

        if (string.IsNullOrEmpty(outputId))
        {
            throw new ArgumentException("Output ID cannot be null or empty", nameof(outputId));
        }

        ipcBytes ??= Array.Empty<byte>();
        var idBytes = Encoding.UTF8.GetBytes(outputId);
        var result = NativeMethods.DoraSendOutputArrowIpc(
            _context,
            idBytes,
            (UIntPtr)idBytes.Length,
            ipcBytes,
            (UIntPtr)ipcBytes.Length);

        return result == 0;
    }

    internal void SendRecordBatchIpcOrThrow(string outputId, byte[] ipcBytes)
    {
        if (!SendRecordBatchIpc(outputId, ipcBytes))
        {
            throw DoraException.Create(
                $"Failed to send RecordBatch to node output '{outputId}'.",
                DoraNodeErrorCode.RecordBatchOutputSendFailed,
                operation: "SendRecordBatch",
                detail: outputId);
        }
    }

    public void Dispose()
    {
        AsyncEventStreamState? asyncEventStream = null;
        Task? asyncPumpTask = null;

        lock (_stateSync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            asyncEventStream = _asyncEventStream;
            asyncPumpTask = _asyncPumpTask;
        }

        if (asyncPumpTask is not null && _context != IntPtr.Zero)
        {
            NativeMethods.CloseDoraEventStream(_context);
        }

        // Best-effort unblock for pending async readers while the native call unwinds.
        asyncEventStream?.Complete();

        if (asyncPumpTask is not null)
        {
            ObserveAsyncPumpCompletion(asyncPumpTask);
        }

        if (_context != IntPtr.Zero)
        {
            NativeMethods.FreeDoraContext(_context);
        }

        GC.SuppressFinalize(this);
    }

    ~DoraNode()
    {
        Dispose();
    }

    private DoraEvent? ReadNextEventCore(string operation)
    {
        try
        {
            SimulateAsyncNativeReadFailureIfRequested();
            var eventPtr = NativeMethods.DoraNextEvent(_context);
            if (eventPtr == IntPtr.Zero)
            {
                return null;
            }

            return new DoraEvent(eventPtr);
        }
        catch (Exception ex)
        {
            throw WrapNativeReadException(ex, operation);
        }
    }

    private void EnsureReadMode(NodeReadMode requestedMode)
    {
        lock (_stateSync)
        {
            if (_readMode == NodeReadMode.None)
            {
                _readMode = requestedMode;
                return;
            }

            if (_readMode != requestedMode)
            {
                throw DoraException.Create(
                    "DoraNode does not support mixing synchronous and asynchronous event reads on the same instance.",
                    DoraNodeErrorCode.LifecycleViolation,
                    operation: requestedMode == NodeReadMode.Async ? "ReadNextEventAsync" : "ReadNextEvent");
            }
        }
    }

    private AsyncEventStreamState EnsureAsyncEventStreamStarted()
    {
        lock (_stateSync)
        {
            if (_disposed)
            {
                throw DoraException.Create(
                    "DoraNode was already disposed.",
                    DoraNodeErrorCode.LifecycleViolation,
                    operation: "ReadNextEventAsync");
            }

            if (_readMode == NodeReadMode.None)
            {
                _readMode = NodeReadMode.Async;
            }
            else if (_readMode != NodeReadMode.Async)
            {
                throw DoraException.Create(
                    "DoraNode does not support mixing synchronous and asynchronous event reads on the same instance.",
                    DoraNodeErrorCode.LifecycleViolation,
                    operation: "ReadNextEventAsync");
            }

            if (_asyncEventStream is not null)
            {
                return _asyncEventStream;
            }

            _asyncEventStream = new AsyncEventStreamState();
            _asyncPumpTask = Task.Factory.StartNew(
                static state => ((DoraNode)state!).RunAsyncEventPump(),
                this,
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            return _asyncEventStream;
        }
    }

    private void RunAsyncEventPump()
    {
        var asyncEventStream = _asyncEventStream;
        if (asyncEventStream is null)
        {
            return;
        }

        try
        {
            while (true)
            {
                var ev = ReadNextEventCore("ReadNextEventAsync");
                if (ev is null)
                {
                    asyncEventStream.Complete();
                    return;
                }

                if (!asyncEventStream.Publish(ev))
                {
                    ev.Dispose();
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            asyncEventStream.Complete(WrapAsyncPumpException(ex));
        }
    }

    private static Exception WrapAsyncPumpException(Exception ex)
    {
        if (ex is DoraException)
        {
            return ex;
        }

        return DoraException.Create(
            "The asynchronous DoraNode event pump failed while reading from the native event stream.",
            ClassifyNativeReadFailure(ex),
            operation: "ReadNextEventAsync",
            innerException: ex);
    }

    private static void ObserveAsyncPumpCompletion(Task asyncPumpTask)
    {
        try
        {
            asyncPumpTask.GetAwaiter().GetResult();
        }
        catch
        {
            // Async read callers observe pump failures through NextAsync/ReadAllEventsAsync.
            // Dispose only drains the background task to avoid leaving a native read in flight.
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw DoraException.Create(
                "DoraNode was already disposed.",
                DoraNodeErrorCode.LifecycleViolation,
                operation: "AccessNode");
        }
    }

    private void SimulateAsyncNativeReadFailureIfRequested()
    {
        var mode = Environment.GetEnvironmentVariable(AsyncReadFailureSimulationEnvVar);
        if (!string.Equals(mode, "invalid-native-handle", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _simulatedAsyncReadFailureTriggered, 1, 0) != 0)
        {
            return;
        }

        throw new ExternalException("Simulated native async read failure for DoraNode smoke validation.");
    }

    private static DoraException WrapNativeReadException(Exception ex, string operation)
    {
        if (ex is DoraException doraException)
        {
            return doraException;
        }

        return DoraException.Create(
            "Failed to read the next Dora node event from the native event stream.",
            ClassifyNativeReadFailure(ex),
            operation,
            innerException: ex);
    }

    private static DoraNodeErrorCode ClassifyNativeReadFailure(Exception ex)
    {
        return ex switch
        {
            DllNotFoundException or BadImageFormatException => DoraNodeErrorCode.NativeLibraryLoadFailed,
            ExternalException or SEHException => DoraNodeErrorCode.InvalidNativeHandle,
            _ => DoraNodeErrorCode.Unknown,
        };
    }
}
