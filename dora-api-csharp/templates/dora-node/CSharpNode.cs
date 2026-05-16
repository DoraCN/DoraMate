using DoraNode;
using System.Text;

namespace MyDoraNode;

/// <summary>
/// Minimal Dora node that receives events and sends output.
/// Replace MyDoraNode with your own node name via --NodeName.
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Starting C# Dora node...");

        try
        {
            using var node = new DoraNode.DoraNode();

            while (true)
            {
                using var ev = node.Next();
                if (ev is null)
                {
                    Console.WriteLine("Event stream closed");
                    break;
                }

                switch (ev.Type)
                {
                    case EventType.Input:
                        HandleInput(node, ev);
                        break;

                    case EventType.Stop:
                        Console.WriteLine("Received stop event");
                        return;

                    case EventType.InputClosed:
                        Console.WriteLine($"Input closed: {ev.InputClosedId}");
                        break;

                    case EventType.Error:
                        Console.Error.WriteLine($"Error event: {ev.ErrorMessage}");
                        break;

                    default:
                        Console.WriteLine($"Unknown event type: {ev.Type}");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void HandleInput(DoraNode.DoraNode node, DoraEvent ev)
    {
        var inputId = ev.Id ?? "unknown";
        var data = ev.Data;

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Received input '{inputId}', {data?.Length ?? 0} bytes");

        if (data != null && data.Length > 0)
        {
            try
            {
                var text = Encoding.UTF8.GetString(data);
                var processed = $"C# processed: {text}";
                var outputBytes = Encoding.UTF8.GetBytes(processed);

                if (node.SendOutput("output", outputBytes))
                {
                    Console.WriteLine($"  -> Sent output ({outputBytes.Length} bytes)");
                }
                else
                {
                    Console.Error.WriteLine("  -> Failed to send output");
                }
            }
            catch
            {
                // Binary data passthrough
                if (node.SendOutput("output", data))
                {
                    Console.WriteLine("  -> Sent binary output");
                }
            }
        }
    }
}