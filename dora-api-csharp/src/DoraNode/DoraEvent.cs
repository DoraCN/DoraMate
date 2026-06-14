using System.Runtime.InteropServices;

namespace DoraNode;

/// <summary>
/// Represents a Dora event containing input data or control signals.
/// </summary>
public sealed class DoraEvent : IDisposable
{
    private delegate void ReadUtf8SliceDelegate(IntPtr doraEvent, out IntPtr outPtr, out UIntPtr outLen);

    private readonly object _sync = new();
    private readonly IntPtr _eventPtr;
    private byte[]? _data;
    private bool _dataMaterialized;
    private bool _disposed;

    /// <summary>
    /// Gets the kind of Dora event represented by this instance.
    /// </summary>
    public EventType Type { get; }

    /// <summary>
    /// Gets the input ID for <see cref="EventType.Input"/> events.
    /// </summary>
    public string? Id { get; }

    /// <summary>
    /// Gets the serialized OpenTelemetry context attached to an input event, when present.
    /// </summary>
    public string? OpenTelemetryContext { get; }

    /// <summary>
    /// Gets the native timestamp associated with an input event.
    /// </summary>
    public ulong Timestamp { get; }

    /// <summary>
    /// Gets the closed input ID for <see cref="EventType.InputClosed"/> events.
    /// </summary>
    public string? InputClosedId { get; }

    /// <summary>
    /// Gets the runtime error message for <see cref="EventType.Error"/> events.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets the byte payload for input events, materializing it on first access.
    /// </summary>
    public byte[]? Data => GetData();

    internal DoraEvent(IntPtr eventPtr)
    {
        _eventPtr = eventPtr;
        Type = NativeMethods.ReadDoraEventType(eventPtr);

        if (Type == EventType.Input)
        {
            Id = ReadUtf8Slice(NativeMethods.ReadDoraInputId, eventPtr);
            OpenTelemetryContext = TryReadUtf8Slice(NativeMethods.ReadDoraInputOpenTelemetryContext, eventPtr);
            Timestamp = NativeMethods.ReadDoraInputTimestamp(eventPtr);
        }
        else if (Type == EventType.InputClosed)
        {
            InputClosedId = TryReadUtf8Slice(NativeMethods.ReadDoraInputClosedId, eventPtr);
        }
        else if (Type == EventType.Error)
        {
            ErrorMessage = TryReadUtf8Slice(NativeMethods.ReadDoraErrorMessage, eventPtr);
        }
    }

    /// <summary>
    /// Materializes the byte payload for an input event.
    /// </summary>
    /// <returns>The input bytes, or <see langword="null"/> when the event does not carry a byte payload.</returns>
    public byte[]? GetData()
    {
        lock (_sync)
        {
            if (_dataMaterialized)
            {
                return _data;
            }

            if (Type != EventType.Input)
            {
                _dataMaterialized = true;
                return null;
            }

            ThrowIfDisposed();

            NativeMethods.ReadDoraInputData(_eventPtr, out var dataPtr, out var dataLen);
            if (dataPtr != IntPtr.Zero && dataLen.ToUInt64() > 0)
            {
                var len = checked((int)dataLen);
                _data = new byte[len];
                Marshal.Copy(dataPtr, _data, 0, len);
            }

            _dataMaterialized = true;
            return _data;
        }
    }

    /// <summary>
    /// Attempts to take the input payload as an Arrow array/schema pair.
    /// </summary>
    /// <param name="payload">Receives the Arrow payload when the input carries Arrow data.</param>
    /// <returns><see langword="true"/> when an Arrow payload was available; otherwise, <see langword="false"/>.</returns>
    public bool TryReadArrowPayload(out ArrowPayload? payload)
    {
        lock (_sync)
        {
            payload = null;
            if (Type != EventType.Input)
            {
                return false;
            }

            ThrowIfDisposed();
            var nativePayload = NativeMethods.ReadDoraInputArrowData(_eventPtr);
            payload = ArrowPayload.FromNative(nativePayload);
            return payload is not null;
        }
    }

    internal bool HasBytePayload()
    {
        lock (_sync)
        {
            if (Type != EventType.Input)
            {
                return false;
            }

            ThrowIfDisposed();
            return NativeMethods.ReadDoraInputHasBytes(_eventPtr);
        }
    }

    /// <summary>
    /// Releases the native event handle associated with this managed wrapper.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        NativeMethods.FreeDoraEvent(_eventPtr);
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~DoraEvent()
    {
        Dispose();
    }

    private static string? ReadUtf8Slice(ReadUtf8SliceDelegate reader, IntPtr eventPtr)
    {
        reader(eventPtr, out var ptr, out var len);
        return ptr != IntPtr.Zero && len.ToUInt64() > 0
            ? Marshal.PtrToStringUTF8(ptr, (int)len)
            : null;
    }

    private static string? TryReadUtf8Slice(ReadUtf8SliceDelegate reader, IntPtr eventPtr)
    {
        try
        {
            return ReadUtf8Slice(reader, eventPtr);
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw DoraException.Create(
                "DoraEvent was already disposed.",
                DoraNodeErrorCode.LifecycleViolation,
                operation: "AccessEvent");
        }
    }
}
