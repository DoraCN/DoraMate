using Apache.Arrow;
using CSharpNodeOperatorArrowDataflow;
using DoraNode;

namespace CSharpNodeOperatorArrowProducer;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Starting C# node->operator Arrow producer...");

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
                    using var batch = CreateRecordBatch();
                    if (!node.Send("batch", batch))
                    {
                        Console.Error.WriteLine("Failed to send Arrow RecordBatch to operator");
                        Environment.Exit(1);
                    }

                    Console.WriteLine("Producer sent Arrow RecordBatch to operator");
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

    private static RecordBatch CreateRecordBatch()
    {
        return RichArrowContract.CreateRecordBatch();
    }
}
