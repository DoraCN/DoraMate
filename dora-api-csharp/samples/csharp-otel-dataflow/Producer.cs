using System.Diagnostics;
using System.Text;
using DoraNode;

namespace Producer;

internal static class Program
{
    private const string ProducerActivitySourceName = "DoraMate.Samples.Otel.Producer";
    private const string DoraNodeActivitySourceName = "DoraMate.DoraNode";
    private static readonly ActivitySource Source = new(ProducerActivitySourceName);

    private static void Main()
    {
        using var listener = CreateConsoleActivityListener();
        ActivitySource.AddActivityListener(listener);
        using var node = new DoraNode.DoraNode();
        var counter = 0;

        Console.WriteLine("OTel producer started.");

        while (node.Next() is { } ev)
        {
            using (ev)
            {
                if (ev.Type == EventType.Stop)
                {
                    return;
                }

                if (ev.Type != EventType.Input)
                {
                    continue;
                }

                counter++;
                using var activity = Source.StartActivity("producer.tick", ActivityKind.Producer);
                activity?.SetTag("dora.output.id", "data");

                var message = $"otel-message-{counter}";
                node.SendOutputOrThrow("data", Encoding.UTF8.GetBytes(message));

                Console.WriteLine(
                    $"PRODUCER trace={Activity.Current?.TraceId} span={Activity.Current?.SpanId} payload={message}");
            }
        }
    }

    private static ActivityListener CreateConsoleActivityListener() => new()
    {
        ShouldListenTo = static source =>
            source.Name == ProducerActivitySourceName ||
            source.Name == DoraNodeActivitySourceName,
        Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
    };
}
