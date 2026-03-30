using CSharpAdvancedArrowNodeDataflow;
using DoraNode;

namespace CSharpAdvancedArrowNodeProducer;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Starting C# advanced Arrow node producer...");

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
                    using var batch = RichAdvancedArrowContract.CreateRecordBatch();
                    var payload = DoraOutputPayload.RecordBatchPayload(batch);
                    if (!node.Send("batch", payload))
                    {
                        Console.Error.WriteLine("Failed to send advanced Arrow RecordBatch.");
                        Environment.Exit(1);
                    }

                    Console.WriteLine("Producer sent advanced Arrow payload");
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
}
