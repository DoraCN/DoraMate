using System.Text;
using DoraNode;

namespace CSharpOperatorProducer;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Starting C# operator producer node...");

        try
        {
            using var node = new DoraNode.DoraNode();
            var counter = 0;
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

                    counter++;
                    var message = $"message-{counter}";
                    if (node.SendOutput("message", Encoding.UTF8.GetBytes(message)))
                    {
                        Console.WriteLine($"Produced: {message}");
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
