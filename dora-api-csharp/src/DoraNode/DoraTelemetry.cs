using System.Diagnostics;
using System.Globalization;

namespace DoraNode;

/// <summary>
/// Helpers for bridging Dora OpenTelemetry metadata with .NET activities.
/// </summary>
public static class DoraTelemetry
{
    private const string TraceParentKey = "traceparent";
    private const string TraceStateKey = "tracestate";

    /// <summary>
    /// Gets the activity source used by Dora node spans.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new("DoraMate.DoraNode", typeof(DoraTelemetry).Assembly.GetName().Version?.ToString());

    /// <summary>
    /// Gets or sets whether existing send APIs should inject <see cref="Activity.Current"/> into Dora metadata.
    /// </summary>
    public static bool AutoInjectCurrentActivity { get; set; } = true;

    /// <summary>
    /// Attempts to parse Dora's serialized OpenTelemetry context into a .NET activity context.
    /// </summary>
    public static bool TryParseContext(string? openTelemetryContext, out ActivityContext context)
    {
        context = default;
        if (string.IsNullOrWhiteSpace(openTelemetryContext))
        {
            return false;
        }

        var values = ParseContextValues(openTelemetryContext);
        if (!values.TryGetValue(TraceParentKey, out var traceParent) || string.IsNullOrWhiteSpace(traceParent))
        {
            return false;
        }

        values.TryGetValue(TraceStateKey, out var traceState);

        try
        {
            context = ActivityContext.Parse(traceParent, string.IsNullOrWhiteSpace(traceState) ? null : traceState);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            context = default;
            return false;
        }
    }

    /// <summary>
    /// Serializes an activity context to Dora's OpenTelemetry metadata format.
    /// </summary>
    public static string SerializeContext(ActivityContext context)
    {
        var traceParent = string.Create(
            CultureInfo.InvariantCulture,
            $"00-{context.TraceId}-{context.SpanId}-{(byte)context.TraceFlags:x2}");

        return string.IsNullOrWhiteSpace(context.TraceState)
            ? $"{TraceParentKey}:{traceParent};"
            : $"{TraceParentKey}:{traceParent};{TraceStateKey}:{context.TraceState};";
    }

    /// <summary>
    /// Serializes the current activity context when automatic injection is enabled.
    /// </summary>
    public static string? SerializeCurrentActivityContext()
    {
        var activity = Activity.Current;
        return AutoInjectCurrentActivity && activity is not null
            ? SerializeContext(activity.Context)
            : null;
    }

    /// <summary>
    /// Starts an activity using the provided Dora OpenTelemetry context as parent when available.
    /// </summary>
    public static Activity? StartActivityFromContext(
        string? openTelemetryContext,
        string? name = null,
        ActivityKind kind = ActivityKind.Consumer)
    {
        var activityName = string.IsNullOrWhiteSpace(name) ? "dora.node.process" : name;
        return TryParseContext(openTelemetryContext, out var parentContext)
            ? ActivitySource.StartActivity(activityName, kind, parentContext)
            : ActivitySource.StartActivity(activityName, kind);
    }

    internal static void ApplyInputTags(Activity? activity, DoraEvent ev)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag("dora.event.type", ev.Type.ToString());
        if (!string.IsNullOrEmpty(ev.Id))
        {
            activity.SetTag("dora.input.id", ev.Id);
        }

        if (ev.HasBytePayload())
        {
            activity.SetTag("dora.payload.kind", "bytes");
        }
    }

    private static Dictionary<string, string> ParseContextValues(string openTelemetryContext)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in openTelemetryContext.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = segment.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = segment[..separatorIndex].Trim();
            var value = segment[(separatorIndex + 1)..].Trim();
            if (key.Length > 0 && value.Length > 0)
            {
                values[key] = value;
            }
        }

        return values;
    }
}
