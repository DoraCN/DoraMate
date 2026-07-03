using System.Text;

namespace DoraOperator;

internal static class SendOutputBridge
{
    public static SendOutput Create(nint nativeSendOutput)
    {
        return new SendOutputDispatcher(nativeSendOutput).SendBytes;
    }

    internal static DoraResult SendArrow(SendOutput sendOutput, string outputId, ArrowPayload payload)
    {
        if (sendOutput.Target is not SendOutputDispatcher dispatcher)
        {
            return DoraResult.Err("Arrow output is only supported for runtime-provided SendOutput delegates.");
        }

        return dispatcher.SendArrow(outputId, payload);
    }

    internal static DoraResult SendArrow(SendOutput sendOutput, string outputId, ArrowArray array, ArrowSchema schema)
    {
        if (sendOutput.Target is not SendOutputDispatcher dispatcher)
        {
            return DoraResult.Err("Arrow output is only supported for runtime-provided SendOutput delegates.");
        }

        return dispatcher.SendArrow(outputId, array, schema);
    }

    internal static DoraResult SendRecordBatch(SendOutput sendOutput, string outputId, byte[] ipcBytes)
    {
        if (sendOutput.Target is not SendOutputDispatcher dispatcher)
        {
            return DoraResult.Err("RecordBatch output is only supported for runtime-provided SendOutput delegates.");
        }

        return dispatcher.SendRecordBatch(outputId, ipcBytes);
    }

    internal static DoraResult SendWithCurrentActivity(SendOutput sendOutput, string outputId, byte[] data)
    {
        if (sendOutput.Target is not SendOutputDispatcher dispatcher)
        {
            return sendOutput(outputId, data);
        }

        return dispatcher.SendBytesWithCurrentActivity(outputId, data);
    }

    internal static DoraResult Send(SendOutput sendOutput, string outputId, byte[] data, System.Diagnostics.ActivityContext? context)
    {
        if (sendOutput.Target is not SendOutputDispatcher dispatcher)
        {
            return sendOutput(outputId, data);
        }

        var openTelemetryContext = context.HasValue ? DoraTelemetry.SerializeContext(context.Value) : null;
        return dispatcher.SendBytes(outputId, data, openTelemetryContext);
    }

    private static DoraResult SendBytes(nint nativeSendOutput, string outputId, byte[] data)
    {
        return SendBytes(nativeSendOutput, outputId, data, DoraTelemetry.SerializeCurrentActivityContext());
    }

    private static DoraResult SendBytes(nint nativeSendOutput, string outputId, byte[] data, string? openTelemetryContext)
    {
        NativeMethods.EnsureLoaded();

        if (nativeSendOutput == 0)
        {
            return DoraResult.Err("Native SendOutput pointer was null.");
        }

        if (string.IsNullOrEmpty(outputId))
        {
            return DoraResult.Err("Output ID cannot be null or empty.");
        }

        data ??= Array.Empty<byte>();
        var outputIdUtf8 = Encoding.UTF8.GetBytes(outputId + "\0");
        var nativeResult = SendOperatorOutput(nativeSendOutput, outputIdUtf8, data, openTelemetryContext);

        try
        {
            if (nativeResult.IsSuccess)
            {
                return DoraResult.Ok();
            }

            return DoraResult.Err(nativeResult.ReadErrorUtf8() ?? "dora_send_operator_output failed.");
        }
        finally
        {
            TryFreeResult(nativeResult);
        }
    }

    private static NativeTypes.NativeDoraResult SendOperatorOutput(
        nint nativeSendOutput,
        byte[] outputIdUtf8,
        byte[] data,
        string? openTelemetryContext)
    {
        if (string.IsNullOrEmpty(openTelemetryContext))
        {
            return NativeMethods.SendOperatorOutput(
                (IntPtr)nativeSendOutput,
                outputIdUtf8,
                data,
                (nuint)data.Length);
        }

        var openTelemetryContextUtf8 = Encoding.UTF8.GetBytes(openTelemetryContext + "\0");
        try
        {
            return NativeMethods.SendOperatorOutputWithMetadata(
                (IntPtr)nativeSendOutput,
                outputIdUtf8,
                data,
                (nuint)data.Length,
                openTelemetryContextUtf8);
        }
        catch (EntryPointNotFoundException)
        {
            return NativeMethods.SendOperatorOutput(
                (IntPtr)nativeSendOutput,
                outputIdUtf8,
                data,
                (nuint)data.Length);
        }
    }

    private static DoraResult SendArrow(nint nativeSendOutput, string outputId, ArrowPayload payload)
    {
        NativeMethods.EnsureLoaded();

        if (nativeSendOutput == 0)
        {
            return DoraResult.Err("Native SendOutput pointer was null.");
        }

        if (string.IsNullOrEmpty(outputId))
        {
            return DoraResult.Err("Output ID cannot be null or empty.");
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

            return DoraResult.Err("Arrow payload had already been consumed or disposed.");
        }

        var outputIdUtf8 = Encoding.UTF8.GetBytes(outputId + "\0");
        var nativeResult = SendOperatorArrowOutput(
            nativeSendOutput,
            outputIdUtf8,
            (IntPtr)arrayHandle,
            (IntPtr)schemaHandle,
            DoraTelemetry.SerializeCurrentActivityContext());

        try
        {
            if (nativeResult.IsSuccess)
            {
                return DoraResult.Ok();
            }

            return DoraResult.Err(nativeResult.ReadErrorUtf8() ?? "dora_send_operator_arrow_output failed.");
        }
        finally
        {
            TryFreeResult(nativeResult);
        }
    }

    private static DoraResult SendRecordBatch(nint nativeSendOutput, string outputId, byte[] ipcBytes)
    {
        NativeMethods.EnsureLoaded();

        if (nativeSendOutput == 0)
        {
            return DoraResult.Err("Native SendOutput pointer was null.");
        }

        if (string.IsNullOrEmpty(outputId))
        {
            return DoraResult.Err("Output ID cannot be null or empty.");
        }

        ipcBytes ??= Array.Empty<byte>();
        var outputIdUtf8 = Encoding.UTF8.GetBytes(outputId + "\0");
        var nativeResult = SendOperatorArrowIpcOutput(
            nativeSendOutput,
            outputIdUtf8,
            ipcBytes,
            DoraTelemetry.SerializeCurrentActivityContext());

        try
        {
            if (nativeResult.IsSuccess)
            {
                return DoraResult.Ok();
            }

            return DoraResult.Err(nativeResult.ReadErrorUtf8() ?? "dora_send_operator_arrow_ipc_output failed.");
        }
        finally
        {
            TryFreeResult(nativeResult);
        }
    }

    private static void TryFreeResult(NativeTypes.NativeDoraResult nativeResult)
    {
        try
        {
            NativeMethods.FreeResult(nativeResult);
        }
        catch (EntryPointNotFoundException)
        {
            // Minimal native APIs may not expose an explicit free helper.
        }
    }

    private static NativeTypes.NativeDoraResult SendOperatorArrowOutput(
        nint nativeSendOutput,
        byte[] outputIdUtf8,
        IntPtr array,
        IntPtr schema,
        string? openTelemetryContext)
    {
        if (string.IsNullOrEmpty(openTelemetryContext))
        {
            return NativeMethods.SendOperatorArrowOutput(
                (IntPtr)nativeSendOutput,
                outputIdUtf8,
                array,
                schema);
        }

        var openTelemetryContextUtf8 = Encoding.UTF8.GetBytes(openTelemetryContext + "\0");
        try
        {
            return NativeMethods.SendOperatorArrowOutputWithMetadata(
                (IntPtr)nativeSendOutput,
                outputIdUtf8,
                array,
                schema,
                openTelemetryContextUtf8);
        }
        catch (EntryPointNotFoundException)
        {
            return NativeMethods.SendOperatorArrowOutput(
                (IntPtr)nativeSendOutput,
                outputIdUtf8,
                array,
                schema);
        }
    }

    private static NativeTypes.NativeDoraResult SendOperatorArrowIpcOutput(
        nint nativeSendOutput,
        byte[] outputIdUtf8,
        byte[] ipcBytes,
        string? openTelemetryContext)
    {
        if (string.IsNullOrEmpty(openTelemetryContext))
        {
            return NativeMethods.SendOperatorArrowIpcOutput(
                (IntPtr)nativeSendOutput,
                outputIdUtf8,
                ipcBytes,
                (nuint)ipcBytes.Length);
        }

        var openTelemetryContextUtf8 = Encoding.UTF8.GetBytes(openTelemetryContext + "\0");
        try
        {
            return NativeMethods.SendOperatorArrowIpcOutputWithMetadata(
                (IntPtr)nativeSendOutput,
                outputIdUtf8,
                ipcBytes,
                (nuint)ipcBytes.Length,
                openTelemetryContextUtf8);
        }
        catch (EntryPointNotFoundException)
        {
            return NativeMethods.SendOperatorArrowIpcOutput(
                (IntPtr)nativeSendOutput,
                outputIdUtf8,
                ipcBytes,
                (nuint)ipcBytes.Length);
        }
    }

    private sealed class SendOutputDispatcher
    {
        private readonly nint _nativeSendOutput;

        public SendOutputDispatcher(nint nativeSendOutput)
        {
            _nativeSendOutput = nativeSendOutput;
        }

        public DoraResult SendBytes(string outputId, byte[] data)
        {
            return SendOutputBridge.SendBytes(_nativeSendOutput, outputId, data);
        }

        public DoraResult SendBytes(string outputId, byte[] data, string? openTelemetryContext)
        {
            return SendOutputBridge.SendBytes(_nativeSendOutput, outputId, data, openTelemetryContext);
        }

        public DoraResult SendBytesWithCurrentActivity(string outputId, byte[] data)
        {
            var openTelemetryContext = System.Diagnostics.Activity.Current is null
                ? null
                : DoraTelemetry.SerializeContext(System.Diagnostics.Activity.Current.Context);
            return SendOutputBridge.SendBytes(_nativeSendOutput, outputId, data, openTelemetryContext);
        }

        public DoraResult SendArrow(string outputId, ArrowPayload payload)
        {
            ArgumentNullException.ThrowIfNull(payload);
            return SendOutputBridge.SendArrow(_nativeSendOutput, outputId, payload);
        }

        public DoraResult SendArrow(string outputId, ArrowArray array, ArrowSchema schema)
        {
            ArgumentNullException.ThrowIfNull(array);
            ArgumentNullException.ThrowIfNull(schema);
            return SendOutputBridge.SendArrow(_nativeSendOutput, outputId, new ArrowPayload(array, schema));
        }

        public DoraResult SendRecordBatch(string outputId, byte[] ipcBytes)
        {
            return SendOutputBridge.SendRecordBatch(_nativeSendOutput, outputId, ipcBytes);
        }
    }
}
