using System.Diagnostics;
using System.Text;
using DoraNode;

namespace Transform;

internal static class Program
{
    private const string DoraNodeActivitySourceName = "DoraMate.DoraNode";

    private static void Main()
    {
        using var listener = CreateConsoleActivityListener();
        ActivitySource.AddActivityListener(listener);
        using var node = new DoraNode.DoraNode();

        Console.WriteLine("OTel transform started.");

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

                using var activity = ev.StartActivity("transform.process", ActivityKind.Consumer);
                var input = Encoding.UTF8.GetString(ev.Data ?? Array.Empty<byte>());
                var output = $"transformed:{input}";

                node.SendOutputOrThrow("out", Encoding.UTF8.GetBytes(output));

                var upstreamParent = ev.TryGetActivityContext(out var parentContext)
                    ? parentContext.SpanId.ToString()
                    : "<none>";
                Console.WriteLine(
                    $"TRANSFORM trace={activity?.TraceId} parent={activity?.ParentSpanId} upstream_span={upstreamParent} span={activity?.SpanId} payload={input} output={output}");
            }
        }
    }

    private static ActivityListener CreateConsoleActivityListener() => new()
    {
        ShouldListenTo = static source => source.Name == DoraNodeActivitySourceName,
        Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
    };
}
