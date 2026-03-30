using System.Text;
using DoraNode;

namespace CSharpAsyncNodeProducer;

internal static class Program
{
    private const string Payload = "async-message";
    private static readonly TimeSpan DefaultStartupDelay = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan CancelModeStartupDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan DisposeModeStartupDelay = TimeSpan.FromMilliseconds(750);

    private static void Main(string[] args)
    {
        Console.WriteLine("Starting C# async node producer...");

        try
        {
            using var node = new DoraNode.DoraNode();
            Thread.Sleep(GetStartupDelay());
            if (ShouldSendPayload())
            {
                node.SendOutputOrThrow("message", Encoding.UTF8.GetBytes(Payload));
                Console.WriteLine($"ASYNC_PRODUCER_SENT payload={Payload}");
            }

            Thread.Sleep(500);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Producer error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static TimeSpan GetStartupDelay()
    {
        var rawMode = Environment.GetEnvironmentVariable("DORA_CSHARP_ASYNC_TEST_MODE");
        return rawMode?.Trim().ToLowerInvariant() switch
        {
            "cancel-before-input" => CancelModeStartupDelay,
            "dispose-pending-read" => DisposeModeStartupDelay,
            _ => DefaultStartupDelay,
        };
    }

    private static bool ShouldSendPayload()
    {
        var rawMode = Environment.GetEnvironmentVariable("DORA_CSHARP_ASYNC_TEST_MODE");
        return !string.Equals(rawMode, "dispose-pending-read", StringComparison.OrdinalIgnoreCase);
    }
}
