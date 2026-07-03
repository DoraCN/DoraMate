using System.Diagnostics;
using System.Text;
using DoraNode;

namespace Consumer;

internal static class Program
{
    private const string DoraNodeActivitySourceName = "DoraMate.DoraNode";

    private static void Main()
    {
        using var listener = CreateConsoleActivityListener();
        ActivitySource.AddActivityListener(listener);
        using var node = new DoraNode.DoraNode();

        Console.WriteLine("OTel consumer started.");

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

                using var activity = ev.StartActivity("consumer.process", ActivityKind.Consumer);
                var payload = Encoding.UTF8.GetString(ev.Data ?? Array.Empty<byte>());
                var upstreamTrace = ev.TryGetActivityContext(out var parentContext)
                    ? parentContext.TraceId.ToString()
                    : "<none>";

                Console.WriteLine(
                    $"CONSUMER trace={activity?.TraceId} parent={activity?.ParentSpanId} upstream_trace={upstreamTrace} payload={payload}");
            }
        }
    }

    private static ActivityListener CreateConsoleActivityListener() => new()
    {
        ShouldListenTo = static source => source.Name == DoraNodeActivitySourceName,
        Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
    };
}
