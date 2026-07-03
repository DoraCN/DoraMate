using System.Diagnostics;
using System.Text;

namespace DoraOperator;

/// <summary>
/// Indicates how the operator runtime should proceed after handling an event.
/// </summary>
public enum DoraStatus : byte
{
    /// <summary>
    /// Continue processing subsequent events.
    /// </summary>
    Continue = 0,

    /// <summary>
    /// Stop the current operator.
    /// </summary>
    Stop = 1,

    /// <summary>
    /// Stop the entire dataflow.
    /// </summary>
    StopAll = 2
}

/// <summary>
/// Represents an input payload delivered to an operator.
/// </summary>
public sealed class Input
{
    private readonly object _sync = new();
    private readonly nint _nativeInput;
    private byte[]? _data;
    private bool _dataMaterialized;
    private bool _nativeAccessAllowed;
    private bool _arrowTaken;

    internal Input(string id, string? openTelemetryContext, nint nativeInput)
    {
        Id = id;
        OpenTelemetryContext = openTelemetryContext;
        _nativeInput = nativeInput;
        _nativeAccessAllowed = nativeInput != 0;
    }

    /// <summary>
    /// Gets the Dora input ID that produced this payload.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the byte payload, materializing it on first access.
    /// </summary>
    public byte[] Data => GetData();

    /// <summary>
    /// Gets a value indicating whether the input currently exposes a byte payload.
    /// </summary>
    public bool HasBytes
    {
        get
        {
            lock (_sync)
            {
                if (_dataMaterialized)
                {
                    return true;
                }

                if (_arrowTaken)
                {
                    return false;
                }

                return CanInspectPayload() && NativeMethods.InputHasBytes((IntPtr)_nativeInput);
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the input currently exposes an Arrow payload.
    /// </summary>
    public bool HasArrow
    {
        get
        {
            lock (_sync)
            {
                if (_dataMaterialized || _arrowTaken)
                {
                    return false;
                }

                return CanInspectPayload() && NativeMethods.InputHasArrow((IntPtr)_nativeInput);
            }
        }
    }

    /// <summary>
    /// Gets the serialized OpenTelemetry context attached to the input, when present.
    /// </summary>
    public string? OpenTelemetryContext { get; }

    /// <summary>
    /// Attempts to parse the input's serialized OpenTelemetry context.
    /// </summary>
    public bool TryGetActivityContext(out ActivityContext context)
    {
        return DoraTelemetry.TryParseContext(OpenTelemetryContext, out context);
    }

    /// <summary>
    /// Starts an activity for processing this input.
    /// </summary>
    public Activity? StartActivity(string? name = null, ActivityKind kind = ActivityKind.Consumer)
    {
        var activity = DoraTelemetry.StartActivityFromContext(OpenTelemetryContext, name, kind);
        DoraTelemetry.ApplyInputTags(activity, this);
        return activity;
    }

    /// <summary>
    /// Materializes the input as a byte payload.
    /// </summary>
    public byte[] GetData()
    {
        lock (_sync)
        {
            if (_dataMaterialized)
            {
                return _data ?? Array.Empty<byte>();
            }

            if (_arrowTaken)
            {
                throw DoraOperatorException.Create(
                    "Input payload was already taken as Arrow.",
                    DoraOperatorErrorCode.LifecycleViolation,
                    operation: "ReadInputBytes",
                    initContext: null,
                    detail: Id);
            }

            EnsureNativeAccess();
            NativeMethods.EnsureLoaded();

            var data = NativeMethods.ReadData((IntPtr)_nativeInput);
            try
            {
                _data = data.ToArray();
                _dataMaterialized = true;
                return _data;
            }
            finally
            {
                NativeMethods.FreeData(data);
            }
        }
    }

    /// <summary>
    /// Attempts to take ownership of the input as an Arrow payload.
    /// </summary>
    public bool TryTakeArrowPayload(out ArrowPayload? payload)
    {
        lock (_sync)
        {
            if (_arrowTaken || _dataMaterialized)
            {
                payload = null;
                return false;
            }

            EnsureNativeAccess();
            NativeMethods.EnsureLoaded();

            var nativePayload = NativeMethods.TakeInputArrowData((IntPtr)_nativeInput);
            payload = ArrowPayload.FromNative(nativePayload);
            _arrowTaken = payload is not null;
            return payload is not null;
        }
    }

    /// <summary>
    /// Decodes the byte payload as a UTF-8 string.
    /// </summary>
    public string GetUtf8String()
    {
        return Encoding.UTF8.GetString(Data);
    }

    internal void InvalidateNativeAccess()
    {
        lock (_sync)
        {
            _nativeAccessAllowed = false;
        }
    }

    private void EnsureNativeAccess()
    {
        if (_nativeInput == 0)
        {
            throw DoraOperatorException.Create(
                "Input did not expose a native payload handle.",
                DoraOperatorErrorCode.InvalidNativeHandle,
                operation: "AccessInputPayload",
                initContext: null,
                detail: Id);
        }

        if (!_nativeAccessAllowed)
        {
            throw DoraOperatorException.Create(
                "Input native payload can only be accessed during OnEvent unless it was already materialized.",
                DoraOperatorErrorCode.LifecycleViolation,
                operation: "AccessInputPayload",
                initContext: null,
                detail: Id);
        }
    }

    private bool CanInspectPayload()
    {
        return _nativeInput != 0 && _nativeAccessAllowed;
    }
}

/// <summary>
/// Represents a low-level operator event before it is projected into higher-level event types.
/// </summary>
public sealed class RawEvent
{
    /// <summary>
    /// Gets the input payload when the event represents an input.
    /// </summary>
    public Input? Input { get; init; }

    /// <summary>
    /// Gets the input ID that was closed for input-closed events.
    /// </summary>
    public string InputClosed { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the runtime requested the operator to stop.
    /// </summary>
    public bool Stop { get; init; }

    /// <summary>
    /// Gets the raw runtime error message for error events.
    /// </summary>
    public string Error { get; init; } = string.Empty;

    internal void InvalidateNativeAccess()
    {
        Input?.InvalidateNativeAccess();
    }
}

/// <summary>
/// Delegate used by the low-level operator runtime to send byte outputs.
/// </summary>
public delegate DoraResult SendOutput(string outputId, byte[] data);

/// <summary>
/// Represents the outcome of a Dora operator runtime operation.
/// </summary>
public sealed class DoraResult
{
    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the error message when the operation failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static DoraResult Ok() => new() { IsSuccess = true };

    /// <summary>
    /// Creates a failed result with a plain error message.
    /// </summary>
    public static DoraResult Err(string error) => new() { IsSuccess = false, Error = error };

    /// <summary>
    /// Creates a failed result with an SDK-formatted error code and message.
    /// </summary>
    public static DoraResult Err(DoraOperatorErrorCode errorCode, string error) =>
        new() { IsSuccess = false, Error = DoraOperatorRuntimeErrors.FormatMessage(errorCode, error) };

    /// <summary>
    /// Creates a failed result from an exception.
    /// </summary>
    public static DoraResult Err(Exception exception) =>
        new() { IsSuccess = false, Error = DoraOperatorRuntimeErrors.FormatException(exception) };
}

/// <summary>
/// Represents the outcome of operator initialization.
/// </summary>
public sealed class InitResult
{
    /// <summary>
    /// Gets a value indicating whether initialization succeeded.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the error message when initialization failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets the native operator context pointer returned to the runtime.
    /// </summary>
    public nint OperatorContext { get; init; }

    /// <summary>
    /// Creates a successful initialization result.
    /// </summary>
    public static InitResult Ok(nint operatorContext = 0) =>
        new() { IsSuccess = true, OperatorContext = operatorContext };

    /// <summary>
    /// Creates a failed initialization result with a plain error message.
    /// </summary>
    public static InitResult Err(string error) =>
        new() { IsSuccess = false, Error = error };

    /// <summary>
    /// Creates a failed initialization result with an SDK-formatted error code and message.
    /// </summary>
    public static InitResult Err(DoraOperatorErrorCode errorCode, string error) =>
        new() { IsSuccess = false, Error = DoraOperatorRuntimeErrors.FormatMessage(errorCode, error) };

    /// <summary>
    /// Creates a failed initialization result from an exception.
    /// </summary>
    public static InitResult Err(Exception exception) =>
        new() { IsSuccess = false, Error = DoraOperatorRuntimeErrors.FormatException(exception) };
}

/// <summary>
/// Represents the outcome of handling a single operator event.
/// </summary>
public sealed class OnEventResult
{
    /// <summary>
    /// Gets a value indicating whether event handling succeeded.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the error message when event handling failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets the runtime status that should be returned to Dora.
    /// </summary>
    public DoraStatus Status { get; init; }

    /// <summary>
    /// Creates a successful result that continues processing.
    /// </summary>
    public static OnEventResult Continue() =>
        new() { IsSuccess = true, Status = DoraStatus.Continue };

    /// <summary>
    /// Creates a successful result that requests the operator or the entire dataflow to stop.
    /// </summary>
    public static OnEventResult Stop(bool stopAll = false) =>
        new() { IsSuccess = true, Status = stopAll ? DoraStatus.StopAll : DoraStatus.Stop };

    /// <summary>
    /// Creates a failed event result with a plain error message.
    /// </summary>
    public static OnEventResult Err(string error) =>
        new() { IsSuccess = false, Error = error, Status = DoraStatus.Continue };

    /// <summary>
    /// Creates a failed event result with an SDK-formatted error code and message.
    /// </summary>
    public static OnEventResult Err(DoraOperatorErrorCode errorCode, string error) =>
        new()
        {
            IsSuccess = false,
            Error = DoraOperatorRuntimeErrors.FormatMessage(errorCode, error),
            Status = DoraStatus.Continue
        };

    /// <summary>
    /// Creates a failed event result from an exception.
    /// </summary>
    public static OnEventResult Err(Exception exception) =>
        new()
        {
            IsSuccess = false,
            Error = DoraOperatorRuntimeErrors.FormatException(exception),
            Status = DoraStatus.Continue
        };
}
