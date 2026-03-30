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

    public EventType Type { get; }
    public string? Id { get; }
    public string? OpenTelemetryContext { get; }
    public ulong Timestamp { get; }
    public string? InputClosedId { get; }
    public string? ErrorMessage { get; }
    public byte[]? Data => GetData();

    internal DoraEvent(IntPtr eventPtr)
    {
        _eventPtr = eventPtr;
        Type = NativeMethods.ReadDoraEventType(eventPtr);

        if (Type == EventType.Input)
        {
            Id = ReadUtf8Slice(NativeMethods.ReadDoraInputId, eventPtr);
            OpenTelemetryContext = ReadUtf8Slice(NativeMethods.ReadDoraInputOpenTelemetryContext, eventPtr);
            Timestamp = NativeMethods.ReadDoraInputTimestamp(eventPtr);
        }
        else if (Type == EventType.InputClosed)
        {
            InputClosedId = ReadUtf8Slice(NativeMethods.ReadDoraInputClosedId, eventPtr);
        }
        else if (Type == EventType.Error)
        {
            ErrorMessage = ReadUtf8Slice(NativeMethods.ReadDoraErrorMessage, eventPtr);
        }
    }

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
