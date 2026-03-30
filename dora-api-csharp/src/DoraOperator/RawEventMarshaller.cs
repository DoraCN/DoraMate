namespace DoraOperator;

internal static class RawEventMarshaller
{
    public static RawEvent Marshal(NativeTypes.NativeRawEvent nativeEvent)
    {
        if (nativeEvent.HasInput)
        {
            return new RawEvent
            {
                Input = MarshalInput(nativeEvent.Input),
            };
        }

        var inputClosed = nativeEvent.InputClosed.ToUtf8String();
        if (!string.IsNullOrEmpty(inputClosed))
        {
            return new RawEvent
            {
                InputClosed = inputClosed,
            };
        }

        if (nativeEvent.Stop)
        {
            return new RawEvent
            {
                Stop = true,
            };
        }

        var error = nativeEvent.Error.ToUtf8String();
        if (!string.IsNullOrEmpty(error))
        {
            return new RawEvent
            {
                Error = error,
            };
        }

        return new RawEvent();
    }

    private static Input MarshalInput(IntPtr nativeInput)
    {
        var id = ReadInputId(nativeInput);
        return new Input(id, ReadOpenTelemetryContext(nativeInput), (nint)nativeInput);
    }

    private static string ReadInputId(IntPtr nativeInput)
    {
        NativeMethods.EnsureLoaded();
        var inputIdPtr = NativeMethods.ReadInputId(nativeInput);
        try
        {
            return inputIdPtr == IntPtr.Zero
                ? string.Empty
                : (System.Runtime.InteropServices.Marshal.PtrToStringUTF8(inputIdPtr) ?? string.Empty);
        }
        finally
        {
            if (inputIdPtr != IntPtr.Zero)
            {
                NativeMethods.FreeInputId(inputIdPtr);
            }
        }
    }

    private static string? ReadOpenTelemetryContext(IntPtr nativeInput)
    {
        NativeMethods.EnsureLoaded();
        var contextPtr = NativeMethods.ReadInputOpenTelemetryContext(nativeInput);
        try
        {
            return contextPtr == IntPtr.Zero
                ? null
                : (System.Runtime.InteropServices.Marshal.PtrToStringUTF8(contextPtr) ?? string.Empty);
        }
        finally
        {
            if (contextPtr != IntPtr.Zero)
            {
                NativeMethods.FreeInputOpenTelemetryContext(contextPtr);
            }
        }
    }
}
