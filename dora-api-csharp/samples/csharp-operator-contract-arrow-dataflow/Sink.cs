using DoraNode;

namespace CSharpOperatorContractArrowSink;

internal static class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("Starting standalone C# operator contract Arrow sink node...");

        try
        {
            using var node = new DoraNode.DoraNode();

            while (true)
            {
                using var ev = node.Next();
                if (ev is null)
                {
                    Console.WriteLine("Sink event stream closed");
                    break;
                }

                if (ev.Type == EventType.Input && ev.Data is { Length: > 0 } data)
                {
                    var message = System.Text.Encoding.UTF8.GetString(data);
                    Console.WriteLine($"Sink received: {message}");
                    return;
                }

                if (ev.Type == EventType.Stop)
                {
                    Console.WriteLine("Sink received stop event");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Sink error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
