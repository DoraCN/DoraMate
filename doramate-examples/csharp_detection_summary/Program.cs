using Apache.Arrow;
using DoraNode;

namespace CSharpDetectionSummary;

internal static class Program
{
    private static int _batchIndex;

    private static void Main()
    {
        Console.WriteLine("Starting C# detection summary node...");

        try
        {
            using var node = new DoraNode.DoraNode();

            while (true)
            {
                using var ev = node.Next();
                if (ev is null)
                {
                    Console.WriteLine("C# detection summary event stream closed.");
                    return;
                }

                switch (ev.Type)
                {
                    case EventType.Input:
                        HandleInput(ev);
                        break;
                    case EventType.InputClosed:
                        Console.WriteLine($"Input closed: {ev.InputClosedId}");
                        break;
                    case EventType.Stop:
                        Console.WriteLine("C# detection summary received stop event.");
                        return;
                    case EventType.Error:
                        Console.Error.WriteLine($"Dora error event: {ev.ErrorMessage}");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"C# detection summary failed: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }

    private static void HandleInput(DoraEvent ev)
    {
        if (!string.Equals(ev.Id, "detections", StringComparison.Ordinal))
        {
            Console.WriteLine($"Ignoring unexpected input '{ev.Id}'.");
            return;
        }

        if (!ev.TryReadRecordBatch(out var batch) || batch is null)
        {
            Console.Error.WriteLine("Expected an Arrow detections payload, but the input could not be materialized as a RecordBatch.");
            Environment.Exit(1);
            return;
        }

        using (batch)
        {
            if (!TryReadColumns(batch, out var detections, out var error))
            {
                Console.Error.WriteLine(error ?? "Failed to read detections RecordBatch.");
                Environment.Exit(1);
                return;
            }

            _batchIndex++;
            Console.WriteLine(FormatSummary(_batchIndex, detections));
        }
    }

    private static bool TryReadColumns(
        RecordBatch batch,
        out IReadOnlyList<DetectionRow> detections,
        out string? error)
    {
        detections = System.Array.Empty<DetectionRow>();
        error = null;

        if (!ArrowRecordBatchAssertions.TryGetStringColumn(batch, "class_name", out var classNames, out error) ||
            !ArrowRecordBatchAssertions.TryGetFloatColumn(batch, "confidence", out var confidences, out error) ||
            !ArrowRecordBatchAssertions.TryGetInt32Column(batch, "bbox_x", out var xs, out error) ||
            !ArrowRecordBatchAssertions.TryGetInt32Column(batch, "bbox_y", out var ys, out error) ||
            !ArrowRecordBatchAssertions.TryGetInt32Column(batch, "bbox_w", out var widths, out error) ||
            !ArrowRecordBatchAssertions.TryGetInt32Column(batch, "bbox_h", out var heights, out error))
        {
            return false;
        }

        if (classNames is null || confidences is null || xs is null || ys is null || widths is null || heights is null)
        {
            error = "Detections RecordBatch columns unexpectedly resolved to null arrays.";
            return false;
        }

        var rows = new List<DetectionRow>(checked((int)batch.Length));
        for (var index = 0; index < batch.Length; index++)
        {
            var className = classNames.GetString(index);
            var confidence = confidences.GetValue(index);
            var x = xs.GetValue(index);
            var y = ys.GetValue(index);
            var width = widths.GetValue(index);
            var height = heights.GetValue(index);

            if (className is null ||
                confidence is null ||
                x is null ||
                y is null ||
                width is null ||
                height is null)
            {
                error = $"Detections RecordBatch contained a null field at row {index}.";
                return false;
            }

            rows.Add(new DetectionRow(
                className,
                confidence.Value,
                x.Value,
                y.Value,
                width.Value,
                height.Value));
        }

        detections = rows;
        return true;
    }

    private static string FormatSummary(int batchIndex, IReadOnlyList<DetectionRow> detections)
    {
        if (detections.Count == 0)
        {
            return $"[CSharpDetectionSummary] batch={batchIndex} detections=0 classes=none";
        }

        var countsByClass = new Dictionary<string, int>(StringComparer.Ordinal);
        DetectionRow? strongest = null;
        foreach (var detection in detections)
        {
            countsByClass[detection.ClassName] = countsByClass.GetValueOrDefault(detection.ClassName) + 1;

            if (strongest is null || detection.Confidence > strongest.Confidence)
            {
                strongest = detection;
            }
        }

        var classSummary = string.Join(
            ", ",
            countsByClass
                .OrderByDescending(entry => entry.Value)
                .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => $"{entry.Key}:{entry.Value}"));

        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "[CSharpDetectionSummary] batch={0} detections={1} strongest={2}@{3:F2} bbox=({4},{5},{6},{7}) classes={8}",
            batchIndex,
            detections.Count,
            strongest!.ClassName,
            strongest.Confidence,
            strongest.X,
            strongest.Y,
            strongest.Width,
            strongest.Height,
            classSummary);
    }

    private sealed record DetectionRow(
        string ClassName,
        float Confidence,
        int X,
        int Y,
        int Width,
        int Height);
}
