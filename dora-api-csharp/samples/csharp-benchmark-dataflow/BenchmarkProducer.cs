using System.Buffers.Binary;
using System.Diagnostics;
using DoraNode;

namespace BenchmarkProducer;

internal static class Program
{
    private static readonly int[] Sizes =
    [
        0,
        8,
        64,
        512,
        2_048,
        4_096,
        4 * 4_096,
        10 * 4_096,
        100 * 4_096,
        1_000 * 4_096,
    ];

    private static void Main()
    {
        var throughputMessages = ReadIntEnv("DORA_CSHARP_BENCH_THROUGHPUT_MESSAGES", 100);
        var latencyDelayMs = ReadIntEnv("DORA_CSHARP_BENCH_LATENCY_DELAY_MS", 10);
        var phaseDelayMs = ReadIntEnv("DORA_CSHARP_BENCH_PHASE_DELAY_MS", 2_000);
        var random = new Random(42);

        using var node = new DoraNode.DoraNode();
        var latencyBuffers = Sizes.Select(size => CreatePayload(size, includeTimestamp: true, random)).ToArray();
        var throughputBuffers = Sizes.Select(size => CreatePayload(size, includeTimestamp: false, random)).ToArray();

        Console.WriteLine($"C# benchmark producer started. throughput_messages={throughputMessages}");

        foreach (var buffer in latencyBuffers)
        {
            WriteTimestamp(buffer);
            node.SendOutputOrThrow("latency", buffer);
            Thread.Sleep(latencyDelayMs);
        }

        Thread.Sleep(phaseDelayMs);

        foreach (var buffer in throughputBuffers)
        {
            for (var i = 0; i < throughputMessages; i++)
            {
                node.SendOutputOrThrow("throughput", buffer);
            }

            node.SendOutputOrThrow("throughput", [1]);
            Thread.Sleep(phaseDelayMs);
        }

        Console.WriteLine("C# benchmark producer finished.");
    }

    private static byte[] CreatePayload(int logicalSize, bool includeTimestamp, Random random)
    {
        var prefix = includeTimestamp ? sizeof(long) : 0;
        var payload = new byte[prefix + logicalSize];
        random.NextBytes(payload.AsSpan(prefix));
        return payload;
    }

    private static void WriteTimestamp(byte[] payload)
    {
        BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(0, sizeof(long)), Stopwatch.GetTimestamp());
    }

    private static int ReadIntEnv(string name, int fallback)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return int.TryParse(raw, out var value) && value > 0 ? value : fallback;
    }
}
