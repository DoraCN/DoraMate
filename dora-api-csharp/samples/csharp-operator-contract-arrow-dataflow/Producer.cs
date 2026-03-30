using System.Text;
using DoraNode;

namespace CSharpOperatorContractArrowProducer;

internal static class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("Starting standalone C# operator contract Arrow producer node...");

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

                if (ev.Type == EventType.Input)
                {
                    if (sent)
                    {
                        continue;
                    }

                    const string message = "build-complex-record-batch";
                    if (node.SendOutput("trigger", Encoding.UTF8.GetBytes(message)))
                    {
                        Console.WriteLine($"Produced trigger: {message}");
                        sent = true;
                        Thread.Sleep(500);
                        return;
                    }
                }
                else if (ev.Type == EventType.Stop)
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
