using Apache.Arrow;

namespace DoraNode;

/// <summary>
/// Unified output helpers for sending byte, text, and Arrow payloads from <see cref="DoraNode"/>.
/// </summary>
public static class DoraNodeOutputExtensions
{
    /// <summary>
    /// Sends a byte payload to the specified node output.
    /// </summary>
    public static bool Send(this DoraNode node, string outputId, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.SendOutput(outputId, data);
    }

    /// <summary>
    /// Sends a byte payload and throws when the send fails.
    /// </summary>
    public static void SendOrThrow(this DoraNode node, string outputId, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(node);
        node.SendOutputOrThrow(outputId, data);
    }

    /// <summary>
    /// Sends a byte payload from a memory buffer to the specified node output.
    /// </summary>
    public static bool Send(this DoraNode node, string outputId, ReadOnlyMemory<byte> data)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.SendOutput(outputId, data.ToArray());
    }

    /// <summary>
    /// Sends a byte payload from a memory buffer and throws when the send fails.
    /// </summary>
    public static void SendOrThrow(this DoraNode node, string outputId, ReadOnlyMemory<byte> data)
    {
        ArgumentNullException.ThrowIfNull(node);
        node.SendOutputOrThrow(outputId, data.ToArray());
    }

    /// <summary>
    /// Sends a UTF-8 string payload to the specified node output.
    /// </summary>
    public static bool Send(this DoraNode node, string outputId, string data)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.SendOutput(outputId, data);
    }

    /// <summary>
    /// Sends a UTF-8 string payload and throws when the send fails.
    /// </summary>
    public static void SendOrThrow(this DoraNode node, string outputId, string data)
    {
        ArgumentNullException.ThrowIfNull(node);
        node.SendOutputOrThrow(outputId, data);
    }

    /// <summary>
    /// Sends an Arrow payload to the specified node output.
    /// </summary>
    public static bool Send(this DoraNode node, string outputId, ArrowPayload payload)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(payload);
        return node.SendArrow(outputId, payload);
    }

    /// <summary>
    /// Sends an Arrow payload and throws when the send fails.
    /// </summary>
    public static void SendOrThrow(this DoraNode node, string outputId, ArrowPayload payload)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(payload);
        node.SendArrowOrThrow(outputId, payload);
    }

    /// <summary>
    /// Sends an Arrow array/schema pair to the specified node output.
    /// </summary>
    public static bool Send(this DoraNode node, string outputId, ArrowArray array, ArrowSchema schema)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(array);
        ArgumentNullException.ThrowIfNull(schema);
        return node.SendArrow(outputId, array, schema);
    }

    /// <summary>
    /// Sends an Arrow array/schema pair and throws when the send fails.
    /// </summary>
    public static void SendOrThrow(this DoraNode node, string outputId, ArrowArray array, ArrowSchema schema)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(array);
        ArgumentNullException.ThrowIfNull(schema);
        node.SendArrowOrThrow(outputId, array, schema);
    }

    /// <summary>
    /// Sends an Arrow record batch to the specified node output.
    /// </summary>
    public static bool Send(this DoraNode node, string outputId, RecordBatch recordBatch)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(recordBatch);
        return node.SendRecordBatch(outputId, recordBatch);
    }

    /// <summary>
    /// Sends an Arrow record batch and throws when the send fails.
    /// </summary>
    public static void SendOrThrow(this DoraNode node, string outputId, RecordBatch recordBatch)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(recordBatch);
        node.SendRecordBatchOrThrow(outputId, recordBatch);
    }

    /// <summary>
    /// Sends a unified payload wrapper to the specified node output.
    /// </summary>
    public static bool Send(this DoraNode node, string outputId, DoraOutputPayload payload)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(payload);

        return payload.Kind switch
        {
            DoraOutputPayloadKind.Bytes => node.Send(outputId, payload.Bytes!),
            DoraOutputPayloadKind.Text => node.Send(outputId, payload.Text!),
            DoraOutputPayloadKind.ArrowPayload => node.Send(outputId, payload.ArrowPayload!),
            DoraOutputPayloadKind.ArrowPair => node.Send(outputId, payload.ArrowArray!, payload.ArrowSchema!),
            DoraOutputPayloadKind.RecordBatch => node.Send(outputId, payload.RecordBatch!),
            _ => throw new InvalidOperationException($"Unsupported output payload kind '{payload.Kind}'.")
        };
    }

    /// <summary>
    /// Sends a unified payload wrapper and throws when the send fails.
    /// </summary>
    public static void SendOrThrow(this DoraNode node, string outputId, DoraOutputPayload payload)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(payload);

        switch (payload.Kind)
        {
            case DoraOutputPayloadKind.Bytes:
                node.SendOrThrow(outputId, payload.Bytes!);
                return;
            case DoraOutputPayloadKind.Text:
                node.SendOrThrow(outputId, payload.Text!);
                return;
            case DoraOutputPayloadKind.ArrowPayload:
                node.SendOrThrow(outputId, payload.ArrowPayload!);
                return;
            case DoraOutputPayloadKind.ArrowPair:
                node.SendOrThrow(outputId, payload.ArrowArray!, payload.ArrowSchema!);
                return;
            case DoraOutputPayloadKind.RecordBatch:
                node.SendOrThrow(outputId, payload.RecordBatch!);
                return;
            default:
                throw new InvalidOperationException($"Unsupported output payload kind '{payload.Kind}'.");
        }
    }
}
