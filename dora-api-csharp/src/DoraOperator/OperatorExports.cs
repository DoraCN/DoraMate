using System.Runtime.InteropServices;

namespace DoraOperator;

internal static class OperatorExports
{
    private static readonly object SyncRoot = new();
    private static OperatorFactory? _factory;

    public static void RegisterFactory(OperatorFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        lock (SyncRoot)
        {
            _factory = factory;
        }
    }

    public static NativeTypes.NativeDoraInitResult DoraInitOperatorExport()
    {
        try
        {
            NativeMethods.EnsureLoaded();
            var host = CreateHost();
            if (host is null)
            {
                var message = DoraOperatorRuntimeErrors.FormatMessage(
                    DoraOperatorErrorCode.InitializationFailed,
                    "No OperatorFactory has been registered for DoraOperator.");
                DoraOperatorRuntimeErrors.LogFailure("init", "CreateHost", message, initContext: null);
                return CreateInitError(message);
            }

            var initResult = host.Init();
            if (!initResult.IsSuccess)
            {
                return CreateInitError(
                    initResult.Error
                    ?? DoraOperatorRuntimeErrors.FormatMessage(
                        DoraOperatorErrorCode.InitializationFailed,
                        "Operator initialization failed."));
            }

            if (initResult.OperatorContext == 0)
            {
                var message = DoraOperatorRuntimeErrors.FormatMessage(
                    DoraOperatorErrorCode.InvalidOperatorContext,
                    "Operator initialization returned a zero operator context.");
                DoraOperatorRuntimeErrors.LogFailure("init", "Init", message, initContext: null);
                return CreateInitError(message);
            }

            return new NativeTypes.NativeDoraInitResult
            {
                Result = CreateSuccessResult(),
                OperatorContext = (IntPtr)initResult.OperatorContext,
            };
        }
        catch (Exception ex)
        {
            DoraOperatorRuntimeErrors.LogException("init", ex);
            return CreateInitError(DoraOperatorRuntimeErrors.FormatException(ex));
        }
    }

    public static NativeTypes.NativeDoraResult DoraDropOperatorExport(IntPtr operatorContext)
    {
        try
        {
            NativeMethods.EnsureLoaded();
            var host = OperatorContextHandle.GetHost((nint)operatorContext);
            host.Drop();
            OperatorContextHandle.Free((nint)operatorContext);
            return CreateSuccessResult();
        }
        catch (Exception ex)
        {
            DoraOperatorRuntimeErrors.LogException("drop", ex);
            return CreateErrorResult(DoraOperatorRuntimeErrors.FormatException(ex));
        }
    }

    public static NativeTypes.NativeOnEventResult DoraOnEventExport(
        IntPtr eventPtr,
        IntPtr sendOutputPtr,
        IntPtr operatorContext)
    {
        try
        {
            NativeMethods.EnsureLoaded();
            if (eventPtr == IntPtr.Zero)
            {
                DoraOperatorRuntimeErrors.LogFailure(
                    "on_event",
                    "MarshalRawEvent",
                    "Received null RawEvent pointer.",
                    initContext: null);
                return CreateOnEventError("Received null RawEvent pointer.", DoraStatus.Continue);
            }

            var host = OperatorContextHandle.GetHost((nint)operatorContext);
            var nativeEvent = Marshal.PtrToStructure<NativeTypes.NativeRawEvent>(eventPtr);
            var managedEvent = RawEventMarshaller.Marshal(nativeEvent);
            var sendOutput = CreateSendOutputDelegate(sendOutputPtr);
            var result = host.OnEvent(managedEvent, sendOutput);
            return ToNative(result);
        }
        catch (Exception ex)
        {
            DoraOperatorRuntimeErrors.LogException("on_event", ex);
            return CreateOnEventError(DoraOperatorRuntimeErrors.FormatException(ex), DoraStatus.Continue);
        }
    }

    private static OperatorHost? CreateHost()
    {
        lock (SyncRoot)
        {
            return _factory is null ? null : new OperatorHost(_factory.CreateOperator());
        }
    }

    private static SendOutput CreateSendOutputDelegate(IntPtr sendOutputPtr) =>
        SendOutputBridge.Create((nint)sendOutputPtr);

    private static NativeTypes.NativeDoraResult CreateSuccessResult()
    {
        return new NativeTypes.NativeDoraResult
        {
            Error = IntPtr.Zero,
        };
    }

    private static NativeTypes.NativeDoraInitResult CreateInitError(string message)
    {
        return new NativeTypes.NativeDoraInitResult
        {
            Result = CreateErrorResult(message),
            OperatorContext = IntPtr.Zero,
        };
    }

    private static NativeTypes.NativeOnEventResult CreateOnEventError(string message, DoraStatus status)
    {
        return new NativeTypes.NativeOnEventResult
        {
            Result = CreateErrorResult(message),
            Status = status,
        };
    }

    private static NativeTypes.NativeOnEventResult ToNative(OnEventResult result)
    {
        return new NativeTypes.NativeOnEventResult
        {
            Result = result.IsSuccess
                ? CreateSuccessResult()
                : CreateErrorResult(result.Error ?? "Unknown operator error."),
            Status = result.Status,
        };
    }

    private static NativeTypes.NativeDoraResult CreateErrorResult(string message)
    {
        NativeMethods.EnsureLoaded();
        var errorUtf8 = System.Text.Encoding.UTF8.GetBytes((message ?? string.Empty) + "\0");
        try
        {
            return NativeMethods.CreateErrorResult(errorUtf8);
        }
        catch (EntryPointNotFoundException)
        {
            var bytes = errorUtf8[..^1];
            var unmanaged = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, unmanaged, bytes.Length);

            var vec = new NativeTypes.NativeVecU8
            {
                Ptr = unmanaged,
                Len = (nuint)bytes.Length,
                Cap = (nuint)bytes.Length,
            };

            var vecPtr = Marshal.AllocHGlobal(Marshal.SizeOf<NativeTypes.NativeVecU8>());
            Marshal.StructureToPtr(vec, vecPtr, fDeleteOld: false);

            return new NativeTypes.NativeDoraResult
            {
                Error = vecPtr,
            };
        }
    }
}
