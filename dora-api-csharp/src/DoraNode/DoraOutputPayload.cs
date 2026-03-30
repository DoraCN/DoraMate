using Apache.Arrow;

namespace DoraNode;

/// <summary>
/// Unified managed payload wrapper for node outputs, covering byte/text and Arrow payloads.
/// </summary>
public sealed class DoraOutputPayload
{
    private DoraOutputPayload(
        DoraOutputPayloadKind kind,
        byte[]? bytes = null,
        string? text = null,
        ArrowPayload? arrowPayload = null,
        ArrowArray? arrowArray = null,
        ArrowSchema? arrowSchema = null,
        RecordBatch? recordBatch = null)
    {
        Kind = kind;
        Bytes = bytes;
        Text = text;
        ArrowPayload = arrowPayload;
        ArrowArray = arrowArray;
        ArrowSchema = arrowSchema;
        RecordBatch = recordBatch;
    }

    internal DoraOutputPayloadKind Kind { get; }

    internal byte[]? Bytes { get; }

    internal string? Text { get; }

    internal ArrowPayload? ArrowPayload { get; }

    internal ArrowArray? ArrowArray { get; }

    internal ArrowSchema? ArrowSchema { get; }

    internal RecordBatch? RecordBatch { get; }

    public static DoraOutputPayload BytesPayload(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return new DoraOutputPayload(DoraOutputPayloadKind.Bytes, bytes: bytes);
    }

    public static DoraOutputPayload BytesPayload(ReadOnlyMemory<byte> bytes) =>
        new(DoraOutputPayloadKind.Bytes, bytes: bytes.ToArray());

    public static DoraOutputPayload TextPayload(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return new DoraOutputPayload(DoraOutputPayloadKind.Text, text: text);
    }

    public static DoraOutputPayload ArrowPayloadValue(ArrowPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return new DoraOutputPayload(DoraOutputPayloadKind.ArrowPayload, arrowPayload: payload);
    }

    public static DoraOutputPayload ArrowPayloadValue(ArrowArray array, ArrowSchema schema)
    {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentNullException.ThrowIfNull(schema);
        return new DoraOutputPayload(DoraOutputPayloadKind.ArrowPair, arrowArray: array, arrowSchema: schema);
    }

    public static DoraOutputPayload RecordBatchPayload(RecordBatch recordBatch)
    {
        ArgumentNullException.ThrowIfNull(recordBatch);
        return new DoraOutputPayload(DoraOutputPayloadKind.RecordBatch, recordBatch: recordBatch);
    }
}

internal enum DoraOutputPayloadKind
{
    Bytes,
    Text,
    ArrowPayload,
    ArrowPair,
    RecordBatch
}
