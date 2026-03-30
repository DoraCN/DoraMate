using DoraNode;
using System.Text;

namespace Consumer;

/// <summary>
/// Consumer node that receives and displays data
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Starting C# Consumer Node...");

        try
        {
            using var node = new DoraNode.DoraNode();

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
                    var inputId = ev.Id ?? "unknown";
                    var data = ev.Data;

                    if (data != null && data.Length > 0)
                    {
                        var message = Encoding.UTF8.GetString(data);
                        Console.WriteLine($"Received on '{inputId}': {message}");

                        // Send acknowledgment
                        var ack = Encoding.UTF8.GetBytes($"ACK: {message}");
                        node.SendOutput("ack", ack);
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
