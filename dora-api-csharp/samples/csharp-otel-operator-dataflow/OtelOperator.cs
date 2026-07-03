using System.Diagnostics;
using System.Text;
using DoraOperator;

namespace OtelOperator;

public sealed class TraceOperator : DoraOperatorBase
{
    private const string DoraOperatorActivitySourceName = "DoraMate.DoraOperator";
    private static readonly ActivityListener Listener = CreateConsoleActivityListener();

    static TraceOperator()
    {
        ActivitySource.AddActivityListener(Listener);
    }

    protected override InitResult Init()
    {
        return InitResult.Ok();
    }

    protected override OnEventResult OnInput(InputEvent ev, OperatorOutput output)
    {
        ArgumentNullException.ThrowIfNull(ev);
        ArgumentNullException.ThrowIfNull(output);

        using var activity = ev.Input.StartActivity("operator.process", ActivityKind.Consumer);
        var input = ev.Input.GetUtf8String();
        var result = $"operator:{input}";

        output.SendOrThrow("out", Encoding.UTF8.GetBytes(result));

        var upstreamSpan = ev.Input.TryGetActivityContext(out var parentContext)
            ? parentContext.SpanId.ToString()
            : "<none>";
        Console.WriteLine(
            $"OPERATOR trace={activity?.TraceId} parent={activity?.ParentSpanId} upstream_span={upstreamSpan} span={activity?.SpanId} payload={input} output={result}");

        return OnEventResult.Continue();
    }

    private static ActivityListener CreateConsoleActivityListener() => new()
    {
        ShouldListenTo = static source => source.Name == DoraOperatorActivitySourceName,
        Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
    };
}
