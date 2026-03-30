using System.IO;
using Apache.Arrow;
using Apache.Arrow.Ipc;

namespace DoraOperator;

/// <summary>
/// Managed RecordBatch helpers built on top of Dora's native Arrow FFI bridge.
/// </summary>
public static class ArrowRecordBatchExtensions
{
    public static bool TryReadRecordBatch(this Input input, out RecordBatch? recordBatch)
    {
        ArgumentNullException.ThrowIfNull(input);

        recordBatch = null;
        if (!input.TryTakeArrowPayload(out var payload) || payload is null)
        {
            return false;
        }

        using (payload)
        {
            var ipcBytesResult = ArrowRecordBatchBridge.TrySerializePayloadToIpcBytes(payload, out var ipcBytes);
            if (!ipcBytesResult.IsSuccess)
            {
                throw DoraOperatorException.Create(
                    ipcBytesResult.Error ?? "Failed to convert native Arrow payload to IPC bytes.",
                    DoraOperatorErrorCode.ArrowPayloadConversionFailed,
                    operation: "TryReadRecordBatch",
                    initContext: null);
            }

            recordBatch = ArrowRecordBatchBridge.DeserializeRecordBatch(ipcBytes);
            return recordBatch is not null;
        }
    }

    public static bool TryReadExpectedRecordBatch(
        this Input input,
        long? expectedRowCount,
        IReadOnlyList<string> expectedFieldNames,
        IReadOnlyList<Apache.Arrow.Types.ArrowTypeId> expectedTypeIds,
        out RecordBatch? recordBatch,
        out string? error)
    {
        return TryReadExpectedRecordBatch(
            input,
            expectedRowCount,
            expectedFieldNames,
            expectedTypeIds,
            out recordBatch,
            out error,
            out _);
    }

    public static bool TryReadExpectedRecordBatch(
        this Input input,
        long? expectedRowCount,
        IReadOnlyList<string> expectedFieldNames,
        IReadOnlyList<Apache.Arrow.Types.ArrowTypeId> expectedTypeIds,
        out RecordBatch? recordBatch,
        out string? error,
        out DoraOperatorErrorCode errorCode)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(expectedFieldNames);
        ArgumentNullException.ThrowIfNull(expectedTypeIds);

        error = null;
        errorCode = DoraOperatorErrorCode.Unknown;
        if (!input.TryReadRecordBatch(out recordBatch) || recordBatch is null)
        {
            error = "Input did not contain an Arrow RecordBatch payload.";
            errorCode = DoraOperatorErrorCode.ArrowPayloadMissing;
            return false;
        }

        if (ArrowSchemaValidation.TryValidateRecordBatch(
                recordBatch,
                expectedRowCount,
                expectedFieldNames,
                expectedTypeIds,
                out error))
        {
            return true;
        }

        recordBatch.Dispose();
        recordBatch = null;
        errorCode = DoraOperatorErrorCode.SchemaValidationFailed;
        return false;
    }

    public static bool TryReadModel<TModel>(
        this Input input,
        IArrowRecordBatchContract<TModel> contract,
        out TModel? model,
        out string? error)
    {
        return TryReadModel(input, contract, out model, out error, out _);
    }

    public static bool TryReadModel<TModel>(
        this Input input,
        IArrowRecordBatchContract<TModel> contract,
        out TModel? model,
        out string? error,
        out DoraOperatorErrorCode errorCode)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(contract);

        model = default;
        error = null;
        errorCode = DoraOperatorErrorCode.Unknown;
        if (!input.TryReadRecordBatch(out var recordBatch) || recordBatch is null)
        {
            error = "Input did not contain an Arrow RecordBatch payload.";
            errorCode = DoraOperatorErrorCode.ArrowPayloadMissing;
            return false;
        }

        using (recordBatch)
        {
            var succeeded = contract.TryRead(recordBatch, out model, out error);
            if (!succeeded)
            {
                errorCode = DoraOperatorErrorCode.ContractValidationFailed;
            }

            return succeeded;
        }
    }

    public static DoraResult SendRecordBatch(this SendOutput sendOutput, string outputId, RecordBatch recordBatch)
    {
        ArgumentNullException.ThrowIfNull(sendOutput);
        ArgumentNullException.ThrowIfNull(recordBatch);

        var ipcBytes = ArrowRecordBatchBridge.SerializeRecordBatch(recordBatch);
        return SendOutputBridge.SendRecordBatch(sendOutput, outputId, ipcBytes);
    }
}

internal static class ArrowRecordBatchBridge
{
    public static DoraResult TrySerializePayloadToIpcBytes(ArrowPayload payload, out byte[] ipcBytes)
    {
        ArgumentNullException.ThrowIfNull(payload);

        ipcBytes = System.Array.Empty<byte>();
        NativeMethods.EnsureLoaded();

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

        var nativeResult = NativeMethods.ArrowPayloadToIpcBytes(
            (IntPtr)arrayHandle,
            (IntPtr)schemaHandle,
            out var nativeBytes);

        try
        {
            if (!nativeResult.IsSuccess)
            {
                return DoraResult.Err(nativeResult.ReadErrorUtf8() ?? "dora_arrow_payload_to_ipc_bytes failed.");
            }

            ipcBytes = nativeBytes.ToArray();
            return DoraResult.Ok();
        }
        finally
        {
            NativeMethods.FreeResult(nativeResult);
            NativeMethods.FreeData(nativeBytes);
        }
    }

    public static byte[] SerializeRecordBatch(RecordBatch recordBatch)
    {
        using var stream = new MemoryStream();
        using (var writer = new ArrowStreamWriter(stream, recordBatch.Schema))
        {
            writer.WriteRecordBatchAsync(recordBatch).GetAwaiter().GetResult();
            writer.WriteEndAsync().GetAwaiter().GetResult();
        }

        return stream.ToArray();
    }

    public static RecordBatch? DeserializeRecordBatch(byte[] ipcBytes)
    {
        using var stream = new MemoryStream(ipcBytes, writable: false);
        using var reader = new ArrowStreamReader(stream);
        return reader.ReadNextRecordBatchAsync().GetAwaiter().GetResult();
    }
}
