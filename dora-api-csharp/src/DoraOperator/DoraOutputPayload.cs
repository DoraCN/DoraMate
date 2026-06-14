using Apache.Arrow;

namespace DoraOperator;

/// <summary>
/// Unified managed payload wrapper for operator outputs.
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

    /// <summary>
    /// Creates a payload wrapper for raw bytes.
    /// </summary>
    public static DoraOutputPayload BytesPayload(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return new DoraOutputPayload(DoraOutputPayloadKind.Bytes, bytes: bytes);
    }

    /// <summary>
    /// Creates a payload wrapper for raw bytes from a memory buffer.
    /// </summary>
    public static DoraOutputPayload BytesPayload(ReadOnlyMemory<byte> bytes) =>
        new(DoraOutputPayloadKind.Bytes, bytes: bytes.ToArray());

    /// <summary>
    /// Creates a payload wrapper for a UTF-8 string.
    /// </summary>
    public static DoraOutputPayload TextPayload(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return new DoraOutputPayload(DoraOutputPayloadKind.Text, text: text);
    }

    /// <summary>
    /// Creates a payload wrapper for an owned Arrow payload.
    /// </summary>
    public static DoraOutputPayload ArrowPayloadValue(ArrowPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return new DoraOutputPayload(DoraOutputPayloadKind.ArrowPayload, arrowPayload: payload);
    }

    /// <summary>
    /// Creates a payload wrapper for an Arrow array/schema pair.
    /// </summary>
    public static DoraOutputPayload ArrowPayloadValue(ArrowArray array, ArrowSchema schema)
    {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentNullException.ThrowIfNull(schema);
        return new DoraOutputPayload(DoraOutputPayloadKind.ArrowPair, arrowArray: array, arrowSchema: schema);
    }

    /// <summary>
    /// Creates a payload wrapper for an Apache Arrow <see cref="RecordBatch"/>.
    /// </summary>
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
