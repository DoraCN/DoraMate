using DoraNode;
using System.Text;
using System.Security.Cryptography;

namespace CSharpNode;

/// <summary>
/// Simple Dora node that generates random data and passes it through
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Starting C# Dora Node...");

        try
        {
            using var node = new DoraNode.DoraNode();
            Console.WriteLine("Dora node initialized successfully");

            while (true)
            {
                var ev = node.Next();

                if (ev == null)
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
                        Console.WriteLine($"Error event received: {ev.ErrorMessage}");
                        break;

                    default:
                        Console.WriteLine($"Unknown event type: {ev.Type}");
                        break;
                }

                ev.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }

    static void HandleInput(DoraNode.DoraNode node, DoraEvent ev)
    {
        var inputId = ev.Id ?? "unknown";
        var data = ev.Data;

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Received input '{inputId}' with {data?.Length ?? 0} bytes");
        if (!string.IsNullOrEmpty(ev.OpenTelemetryContext))
        {
            Console.WriteLine($"  -> OpenTelemetry context: {ev.OpenTelemetryContext}");
        }

        // Process the data and send output
        if (data != null && data.Length > 0)
        {
            // Example: Transform the data (e.g., convert to uppercase if it's text)
            string? textData = null;
            try
            {
                textData = Encoding.UTF8.GetString(data);
                var transformed = $"C# processed: {textData}";
                var outputData = Encoding.UTF8.GetBytes(transformed);

                if (node.SendOutput("output", outputData))
                {
                    Console.WriteLine($"  -> Sent output: {transformed.Substring(0, Math.Min(50, transformed.Length))}...");
                }
                else
                {
                    Console.WriteLine("  -> Failed to send output");
                }
            }
            catch
            {
                // Not text data, just pass through
                if (node.SendOutput("output", data))
                {
                    Console.WriteLine("  -> Sent binary output");
                }
            }
        }
    }
}
