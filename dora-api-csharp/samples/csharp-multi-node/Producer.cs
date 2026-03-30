using DoraNode;
using System.Text;
using System.Security.Cryptography;

namespace Producer;

/// <summary>
/// Producer node that generates random data
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Starting C# Producer Node...");

        try
        {
            using var node = new DoraNode.DoraNode();
            var counter = 0;

            while (true)
            {
                var ev = node.Next();

                if (ev == null)
                {
                    Console.WriteLine("Event stream closed");
                    break;
                }

                if (ev.Type == EventType.Input)
                {
                    // Generate random data
                    counter++;
                    var message = $"Message #{counter} from C# Producer at {DateTime.Now:HH:mm:ss.fff}";
                    var data = Encoding.UTF8.GetBytes(message);

                    if (node.SendOutput("data", data))
                    {
                        Console.WriteLine($"Sent: {message}");
                    }
                    else
                    {
                        Console.WriteLine("Failed to send output");
                    }
                }
                else if (ev.Type == EventType.Stop)
                {
                    Console.WriteLine("Received stop event");
                    return;
                }

                ev.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
