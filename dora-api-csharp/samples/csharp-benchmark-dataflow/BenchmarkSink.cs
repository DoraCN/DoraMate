using System.Buffers.Binary;
using System.Diagnostics;
using DoraNode;

namespace BenchmarkSink;

internal static class Program
{
    private sealed class Bucket
    {
        public int Size { get; init; }
        public int Count { get; set; }
        public long StartTicks { get; set; }
        public List<double> LatenciesMicroseconds { get; } = [];
    }

    private static void Main()
    {
        using var node = new DoraNode.DoraNode();
        var latencyMode = true;
        var bucket = new Bucket { Size = -1, StartTicks = Stopwatch.GetTimestamp() };

        Console.WriteLine("Latency:");

        while (node.Next() is { } ev)
        {
            using (ev)
            {
                if (ev.Type == EventType.Stop)
                {
                    break;
                }

                if (ev.Type != EventType.Input)
                {
                    continue;
                }

                var data = ev.Data ?? [];
                if (string.Equals(ev.Id, "throughput", StringComparison.Ordinal) && latencyMode)
                {
                    Flush(bucket, latencyMode);
                    latencyMode = false;
                    bucket = new Bucket { Size = -1, StartTicks = Stopwatch.GetTimestamp() };
                    Console.WriteLine("Throughput:");
                }

                var size = GetLogicalSize(ev.Id, data, latencyMode);
                if (size != bucket.Size)
                {
                    Flush(bucket, latencyMode);
                    bucket = new Bucket { Size = size, StartTicks = Stopwatch.GetTimestamp() };
                }

                if (latencyMode)
                {
                    bucket.LatenciesMicroseconds.Add(ReadLatencyMicroseconds(data));
                }

                bucket.Count++;
            }
        }

        Flush(bucket, latencyMode);
    }

    private static int GetLogicalSize(string? inputId, byte[] data, bool latencyMode)
    {
        if (!latencyMode && string.Equals(inputId, "throughput", StringComparison.Ordinal) && data.Length == 1)
        {
            return 1;
        }

        return latencyMode ? Math.Max(0, data.Length - sizeof(long)) : data.Length;
    }

    private static double ReadLatencyMicroseconds(byte[] data)
    {
        if (data.Length < sizeof(long))
        {
            return 0;
        }

        var sentTicks = BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(0, sizeof(long)));
        return TicksToMicroseconds(Stopwatch.GetTimestamp() - sentTicks);
    }

    private static void Flush(Bucket bucket, bool latencyMode)
    {
        if (bucket.Count <= 0 || bucket.Size == 1)
        {
            return;
        }

        if (latencyMode)
        {
            var values = bucket.LatenciesMicroseconds.OrderBy(static value => value).ToArray();
            var avg = values.Length == 0 ? 0 : values.Average();
            var p50 = Percentile(values, 0.50);
            var p95 = Percentile(values, 0.95);
            var p99 = Percentile(values, 0.99);
            Console.WriteLine($"size 0x{bucket.Size,-6:x}: {avg:F1} us avg, p50 {p50:F1} us, p95 {p95:F1} us, p99 {p99:F1} us");
            Console.WriteLine($"BENCH,csharp,latency,{bucket.Size},{bucket.Count},{avg:F3},{p50:F3},{p95:F3},{p99:F3}");
            return;
        }

        var elapsedSeconds = TicksToSeconds(Stopwatch.GetTimestamp() - bucket.StartTicks);
        var messagesPerSecond = elapsedSeconds <= 0 ? 0 : bucket.Count / elapsedSeconds;
        Console.WriteLine($"size 0x{bucket.Size,-6:x}: {messagesPerSecond:F0} messages per second");
        Console.WriteLine($"BENCH,csharp,throughput,{bucket.Size},{bucket.Count},{messagesPerSecond:F3}");
    }

    private static double Percentile(double[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0)
        {
            return 0;
        }

        var index = (int)Math.Ceiling(percentile * sortedValues.Length) - 1;
        return sortedValues[Math.Clamp(index, 0, sortedValues.Length - 1)];
    }

    private static double TicksToMicroseconds(long ticks) => ticks * 1_000_000.0 / Stopwatch.Frequency;

    private static double TicksToSeconds(long ticks) => ticks / (double)Stopwatch.Frequency;
}
