using Apache.Arrow;
using DoraNode;
using CSharpArrowNodeDataflow;

namespace CSharpArrowNodeProducer;

class Program
{
    private const string NonArrowPayloadText = "not-arrow-bytes";

    static void Main(string[] args)
    {
        Console.WriteLine("Starting C# Arrow node producer...");

        try
        {
            using var node = new DoraNode.DoraNode();
            var sent = false;

            while (true)
            {
                using var ev = node.Next();
                if (ev is null)
                {
                    Console.WriteLine("Producer event stream closed");
                    break;
                }

                if (ev.Type == EventType.Input && !sent)
                {
                    if (!TrySendPayload(node, out var sendError))
                    {
                        Console.Error.WriteLine(sendError);
                        Environment.Exit(1);
                    }

                    Console.WriteLine("Producer sent test payload");
                    sent = true;
                    Thread.Sleep(500);
                    return;
                }

                if (ev.Type == EventType.Stop)
                {
                    Console.WriteLine("Producer received stop event");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Producer error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static bool TrySendPayload(DoraNode.DoraNode node, out string? error)
    {
        error = null;
        using var payloadOwner = CreatePayloadOwner(GetTestMode());
        if (!node.Send("batch", payloadOwner.Payload))
        {
            error = "Failed to send output payload.";
            return false;
        }

        return true;
    }

    private static ArrowTestMode GetTestMode()
    {
        var rawMode = Environment.GetEnvironmentVariable("DORA_CSHARP_ARROW_TEST_MODE");
        return rawMode?.Trim().ToLowerInvariant() switch
        {
            null or "" or "normal" => ArrowTestMode.Normal,
            "non-arrow-bytes" => ArrowTestMode.NonArrowBytes,
            "schema-mismatch" => ArrowTestMode.SchemaMismatch,
            "empty-batch" => ArrowTestMode.EmptyBatch,
            _ => throw new InvalidOperationException($"Unsupported Arrow test mode '{rawMode}'.")
        };
    }

    private static RecordBatch CreateRecordBatch(ArrowTestMode mode)
    {
        var firstFieldName = mode == ArrowTestMode.SchemaMismatch ? "label" : "name";
        var empty = mode == ArrowTestMode.EmptyBatch;
        return RichArrowContract.CreateRecordBatch(firstFieldName, empty);
    }

    private static PayloadOwner CreatePayloadOwner(ArrowTestMode mode)
    {
        if (mode == ArrowTestMode.NonArrowBytes)
        {
            return new PayloadOwner(DoraOutputPayload.TextPayload(NonArrowPayloadText));
        }

        var batch = CreateRecordBatch(mode);
        return new PayloadOwner(DoraOutputPayload.RecordBatchPayload(batch), batch);
    }

    private sealed class PayloadOwner : IDisposable
    {
        private readonly IDisposable? _ownedResource;

        public PayloadOwner(DoraOutputPayload payload, IDisposable? ownedResource = null)
        {
            Payload = payload;
            _ownedResource = ownedResource;
        }

        public DoraOutputPayload Payload { get; }

        public void Dispose()
        {
            _ownedResource?.Dispose();
        }
    }

    private enum ArrowTestMode
    {
        Normal,
        NonArrowBytes,
        SchemaMismatch,
        EmptyBatch
    }
}
