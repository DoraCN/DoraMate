using CSharpComplexArrowNodeDataflow;
using DoraNode;

namespace CSharpComplexArrowNodeProducer;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Starting C# complex Arrow node producer...");

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
                    using var batch = RichComplexArrowContract.CreateRecordBatch(
                        invalidNestedPriorityType: GetTestMode() == ArrowTestMode.ContractFailure);
                    var payload = DoraOutputPayload.RecordBatchPayload(batch);
                    if (!node.Send("batch", payload))
                    {
                        Console.Error.WriteLine("Failed to send complex Arrow RecordBatch.");
                        Environment.Exit(1);
                    }

                    Console.WriteLine("Producer sent complex Arrow payload");
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

    private static ArrowTestMode GetTestMode()
    {
        var rawMode = Environment.GetEnvironmentVariable("DORA_CSHARP_ARROW_TEST_MODE");
        return rawMode?.Trim().ToLowerInvariant() switch
        {
            null or "" or "normal" => ArrowTestMode.Normal,
            "contract-failure" => ArrowTestMode.ContractFailure,
            _ => throw new InvalidOperationException($"Unsupported Arrow test mode '{rawMode}'.")
        };
    }

    private enum ArrowTestMode
    {
        Normal,
        ContractFailure
    }
}
