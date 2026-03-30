using System.Text;
using DoraNode;

namespace CSharpOperatorSink;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Starting C# operator sink node...");

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
                    var message = Encoding.UTF8.GetString(data);
                    Console.WriteLine($"Sink received: {message}");
                }
                else if (ev.Type == EventType.Stop)
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
