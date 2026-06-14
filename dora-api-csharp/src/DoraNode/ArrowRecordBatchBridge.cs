using System.IO;
using Apache.Arrow;
using Apache.Arrow.Ipc;
using Apache.Arrow.Types;

namespace DoraNode;

/// <summary>
/// Managed RecordBatch helpers built on top of Dora's native Arrow bridge for nodes.
/// </summary>
public static class ArrowRecordBatchExtensions
{
    /// <summary>
    /// Attempts to deserialize the input event payload as an Arrow <see cref="RecordBatch"/>.
    /// </summary>
    public static bool TryReadRecordBatch(this DoraEvent doraEvent, out RecordBatch? recordBatch)
    {
        ArgumentNullException.ThrowIfNull(doraEvent);

        recordBatch = null;
        if (doraEvent.HasBytePayload())
        {
            return false;
        }

        if (!doraEvent.TryReadArrowPayload(out var payload) || payload is null)
        {
            return false;
        }

        using (payload)
        {
            if (!ArrowRecordBatchBridge.TrySerializePayloadToIpcBytes(payload, out var ipcBytes))
            {
                throw DoraException.Create(
                    "Failed to convert node Arrow payload to IPC bytes.",
                    DoraNodeErrorCode.ArrowPayloadConversionFailed,
                    operation: "TryReadRecordBatch");
            }

            recordBatch = ArrowRecordBatchBridge.DeserializeRecordBatch(ipcBytes);
            return recordBatch is not null;
        }
    }

    /// <summary>
    /// Attempts to read and validate an Arrow record batch against an expected schema contract.
    /// </summary>
    public static bool TryReadExpectedRecordBatch(
        this DoraEvent doraEvent,
        long? expectedRowCount,
        IReadOnlyList<string> expectedFieldNames,
        IReadOnlyList<ArrowTypeId> expectedTypeIds,
        out RecordBatch? recordBatch,
        out string? error)
    {
        return TryReadExpectedRecordBatch(
            doraEvent,
            expectedRowCount,
            expectedFieldNames,
            expectedTypeIds,
            out recordBatch,
            out error,
            out _);
    }

    /// <summary>
    /// Attempts to read and validate an Arrow record batch against an expected schema contract.
    /// </summary>
    public static bool TryReadExpectedRecordBatch(
        this DoraEvent doraEvent,
        long? expectedRowCount,
        IReadOnlyList<string> expectedFieldNames,
        IReadOnlyList<ArrowTypeId> expectedTypeIds,
        out RecordBatch? recordBatch,
        out string? error,
        out DoraNodeErrorCode errorCode)
    {
        ArgumentNullException.ThrowIfNull(doraEvent);
        ArgumentNullException.ThrowIfNull(expectedFieldNames);
        ArgumentNullException.ThrowIfNull(expectedTypeIds);

        error = null;
        errorCode = DoraNodeErrorCode.Unknown;
        if (!doraEvent.TryReadRecordBatch(out recordBatch) || recordBatch is null)
        {
            error = "Input event did not contain an Arrow RecordBatch payload.";
            errorCode = DoraNodeErrorCode.ArrowPayloadMissing;
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
        errorCode = DoraNodeErrorCode.SchemaValidationFailed;
        return false;
    }

    /// <summary>
    /// Attempts to read an Arrow record batch and project it into a typed model.
    /// </summary>
    public static bool TryReadModel<TModel>(
        this DoraEvent doraEvent,
        IArrowRecordBatchContract<TModel> contract,
        out TModel? model,
        out string? error)
    {
        return TryReadModel(doraEvent, contract, out model, out error, out _);
    }

    /// <summary>
    /// Attempts to read an Arrow record batch and project it into a typed model.
    /// </summary>
    public static bool TryReadModel<TModel>(
        this DoraEvent doraEvent,
        IArrowRecordBatchContract<TModel> contract,
        out TModel? model,
        out string? error,
        out DoraNodeErrorCode errorCode)
    {
        ArgumentNullException.ThrowIfNull(doraEvent);
        ArgumentNullException.ThrowIfNull(contract);

        model = default;
        error = null;
        errorCode = DoraNodeErrorCode.Unknown;
        if (!doraEvent.TryReadRecordBatch(out var recordBatch) || recordBatch is null)
        {
            error = "Input event did not contain an Arrow RecordBatch payload.";
            errorCode = DoraNodeErrorCode.ArrowPayloadMissing;
            return false;
        }

        using (recordBatch)
        {
            var succeeded = contract.TryRead(recordBatch, out model, out error);
            if (!succeeded)
            {
                errorCode = DoraNodeErrorCode.ContractValidationFailed;
            }

            return succeeded;
        }
    }

    /// <summary>
    /// Serializes a record batch to IPC bytes and sends it through a node output.
    /// </summary>
    public static bool SendRecordBatch(this DoraNode node, string outputId, RecordBatch recordBatch)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(recordBatch);

        var ipcBytes = ArrowRecordBatchBridge.SerializeRecordBatch(recordBatch);
        return node.SendRecordBatchIpc(outputId, ipcBytes);
    }

    /// <summary>
    /// Serializes a record batch to IPC bytes, sends it through a node output, and throws when the send fails.
    /// </summary>
    public static void SendRecordBatchOrThrow(this DoraNode node, string outputId, RecordBatch recordBatch)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(recordBatch);

        var ipcBytes = ArrowRecordBatchBridge.SerializeRecordBatch(recordBatch);
        node.SendRecordBatchIpcOrThrow(outputId, ipcBytes);
    }
}

internal static class ArrowRecordBatchBridge
{
    public static bool TrySerializePayloadToIpcBytes(ArrowPayload payload, out byte[] ipcBytes)
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

            return false;
        }

        var result = NativeMethods.ArrowPayloadToIpcBytes(
            (IntPtr)arrayHandle,
            (IntPtr)schemaHandle,
            out var nativeBytes);

        try
        {
            if (result != 0)
            {
                return false;
            }

            ipcBytes = nativeBytes.ToArray();
            return true;
        }
        finally
        {
            NativeMethods.FreeOwnedBytes(nativeBytes);
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
