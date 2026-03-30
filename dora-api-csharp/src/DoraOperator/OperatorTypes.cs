using System.Text;

namespace DoraOperator;

public enum DoraStatus : byte
{
    Continue = 0,
    Stop = 1,
    StopAll = 2
}

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

    public string Id { get; }
    public byte[] Data => GetData();

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

    public string? OpenTelemetryContext { get; }

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

public sealed class RawEvent
{
    public Input? Input { get; init; }
    public string InputClosed { get; init; } = string.Empty;
    public bool Stop { get; init; }
    public string Error { get; init; } = string.Empty;

    internal void InvalidateNativeAccess()
    {
        Input?.InvalidateNativeAccess();
    }
}

public delegate DoraResult SendOutput(string outputId, byte[] data);

public sealed class DoraResult
{
    public bool IsSuccess { get; init; }
    public string? Error { get; init; }

    public static DoraResult Ok() => new() { IsSuccess = true };
    public static DoraResult Err(string error) => new() { IsSuccess = false, Error = error };
    public static DoraResult Err(DoraOperatorErrorCode errorCode, string error) =>
        new() { IsSuccess = false, Error = DoraOperatorRuntimeErrors.FormatMessage(errorCode, error) };
    public static DoraResult Err(Exception exception) =>
        new() { IsSuccess = false, Error = DoraOperatorRuntimeErrors.FormatException(exception) };
}

public sealed class InitResult
{
    public bool IsSuccess { get; init; }
    public string? Error { get; init; }
    public nint OperatorContext { get; init; }

    public static InitResult Ok(nint operatorContext = 0) =>
        new() { IsSuccess = true, OperatorContext = operatorContext };
    public static InitResult Err(string error) =>
        new() { IsSuccess = false, Error = error };
    public static InitResult Err(DoraOperatorErrorCode errorCode, string error) =>
        new() { IsSuccess = false, Error = DoraOperatorRuntimeErrors.FormatMessage(errorCode, error) };
    public static InitResult Err(Exception exception) =>
        new() { IsSuccess = false, Error = DoraOperatorRuntimeErrors.FormatException(exception) };
}

public sealed class OnEventResult
{
    public bool IsSuccess { get; init; }
    public string? Error { get; init; }
    public DoraStatus Status { get; init; }

    public static OnEventResult Continue() =>
        new() { IsSuccess = true, Status = DoraStatus.Continue };
    public static OnEventResult Stop(bool stopAll = false) =>
        new() { IsSuccess = true, Status = stopAll ? DoraStatus.StopAll : DoraStatus.Stop };
    public static OnEventResult Err(string error) =>
        new() { IsSuccess = false, Error = error, Status = DoraStatus.Continue };
    public static OnEventResult Err(DoraOperatorErrorCode errorCode, string error) =>
        new()
        {
            IsSuccess = false,
            Error = DoraOperatorRuntimeErrors.FormatMessage(errorCode, error),
            Status = DoraStatus.Continue
        };
    public static OnEventResult Err(Exception exception) =>
        new()
        {
            IsSuccess = false,
            Error = DoraOperatorRuntimeErrors.FormatException(exception),
            Status = DoraStatus.Continue
        };
}
