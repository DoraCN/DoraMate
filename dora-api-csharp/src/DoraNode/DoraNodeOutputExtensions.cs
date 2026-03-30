using Apache.Arrow;

namespace DoraNode;

/// <summary>
/// Unified output helpers for sending byte, text, and Arrow payloads from <see cref="DoraNode"/>.
/// </summary>
public static class DoraNodeOutputExtensions
{
    public static bool Send(this DoraNode node, string outputId, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.SendOutput(outputId, data);
    }

    public static void SendOrThrow(this DoraNode node, string outputId, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(node);
        node.SendOutputOrThrow(outputId, data);
    }

    public static bool Send(this DoraNode node, string outputId, ReadOnlyMemory<byte> data)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.SendOutput(outputId, data.ToArray());
    }

    public static void SendOrThrow(this DoraNode node, string outputId, ReadOnlyMemory<byte> data)
    {
        ArgumentNullException.ThrowIfNull(node);
        node.SendOutputOrThrow(outputId, data.ToArray());
    }

    public static bool Send(this DoraNode node, string outputId, string data)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.SendOutput(outputId, data);
    }

    public static void SendOrThrow(this DoraNode node, string outputId, string data)
    {
        ArgumentNullException.ThrowIfNull(node);
        node.SendOutputOrThrow(outputId, data);
    }

    public static bool Send(this DoraNode node, string outputId, ArrowPayload payload)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(payload);
        return node.SendArrow(outputId, payload);
    }

    public static void SendOrThrow(this DoraNode node, string outputId, ArrowPayload payload)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(payload);
        node.SendArrowOrThrow(outputId, payload);
    }

    public static bool Send(this DoraNode node, string outputId, ArrowArray array, ArrowSchema schema)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(array);
        ArgumentNullException.ThrowIfNull(schema);
        return node.SendArrow(outputId, array, schema);
    }

    public static void SendOrThrow(this DoraNode node, string outputId, ArrowArray array, ArrowSchema schema)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(array);
        ArgumentNullException.ThrowIfNull(schema);
        node.SendArrowOrThrow(outputId, array, schema);
    }

    public static bool Send(this DoraNode node, string outputId, RecordBatch recordBatch)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(recordBatch);
        return node.SendRecordBatch(outputId, recordBatch);
    }

    public static void SendOrThrow(this DoraNode node, string outputId, RecordBatch recordBatch)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(recordBatch);
        node.SendRecordBatchOrThrow(outputId, recordBatch);
    }

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
