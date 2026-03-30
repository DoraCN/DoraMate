using System.Text;
using DoraNode;

namespace CSharpAsyncNodeConsumer;

internal static class Program
{
    private const string ExpectedPayload = "async-message";

    private static async Task Main(string[] args)
    {
        Console.WriteLine("Starting C# async node consumer...");

        try
        {
            using var node = new DoraNode.DoraNode();
            var mode = GetTestMode();

            switch (mode)
            {
                case AsyncNodeTestMode.Normal:
                    await RunNormalAsync(node);
                    return;
                case AsyncNodeTestMode.CancelBeforeInput:
                    await RunCancellationAsync(node);
                    return;
                case AsyncNodeTestMode.MixedRead:
                    await RunMixedReadAsync(node);
                    return;
                case AsyncNodeTestMode.ConcurrentRead:
                    await RunConcurrentReadAsync(node);
                    return;
                case AsyncNodeTestMode.StreamClose:
                    await RunStreamCloseAsync(node);
                    return;
                case AsyncNodeTestMode.DisposePendingRead:
                    await RunDisposePendingReadAsync(node);
                    return;
                case AsyncNodeTestMode.NativeFailure:
                    await RunNativeFailureAsync(node);
                    return;
                default:
                    throw new InvalidOperationException($"Unsupported async node test mode '{mode}'.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Consumer error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task RunNormalAsync(DoraNode.DoraNode node)
    {
        await foreach (var ev in node.ReadAllEventsAsync())
        {
            using (ev)
            {
                if (ev.Type == EventType.Input)
                {
                    var payload = ReadPayload(ev);
                    ValidatePayload(payload);
                    Console.WriteLine($"NODE_ASYNC_OK payload={payload}");
                    return;
                }

                if (ev.Type == EventType.Stop)
                {
                    Console.WriteLine("Consumer received stop event");
                    return;
                }
            }
        }
    }

    private static async Task RunCancellationAsync(DoraNode.DoraNode node)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        try
        {
            using var unexpectedEvent = await node.NextAsync(cts.Token);
            Console.Error.WriteLine("Expected NextAsync cancellation before the first input arrived.");
            Environment.Exit(1);
        }
        catch (OperationCanceledException)
        {
            using var ev = await node.NextAsync();
            if (ev is null)
            {
                Console.Error.WriteLine("Expected an input event after cancellation, but the stream closed.");
                Environment.Exit(1);
            }

            var payload = ReadPayload(ev);
            ValidatePayload(payload);
            Console.WriteLine($"NODE_ASYNC_CANCEL_OK payload={payload}");
        }
    }

    private static async Task RunMixedReadAsync(DoraNode.DoraNode node)
    {
        using var ev = await node.NextAsync();
        if (ev is null)
        {
            Console.Error.WriteLine("Expected an input event for async/sync mixed read validation.");
            Environment.Exit(1);
        }

        var payload = ReadPayload(ev);
        ValidatePayload(payload);

        try
        {
            using var unexpected = node.Next();
            Console.Error.WriteLine("Expected mixing NextAsync and Next to throw.");
            Environment.Exit(1);
        }
        catch (DoraException ex) when (ex.ErrorCode == DoraNodeErrorCode.LifecycleViolation)
        {
            Console.WriteLine($"NODE_ASYNC_MIXED_READ_OK code={ex.ErrorCode} payload={payload}");
        }
    }

    private static async Task RunConcurrentReadAsync(DoraNode.DoraNode node)
    {
        var pendingRead = node.NextAsync().AsTask();

        try
        {
            using var unexpected = await node.NextAsync();
            Console.Error.WriteLine("Expected concurrent NextAsync calls to throw.");
            Environment.Exit(1);
        }
        catch (DoraException ex) when (ex.ErrorCode == DoraNodeErrorCode.LifecycleViolation)
        {
            using var ev = await pendingRead;
            if (ev is null)
            {
                Console.Error.WriteLine("Expected the first NextAsync call to receive an input event.");
                Environment.Exit(1);
            }

            var payload = ReadPayload(ev);
            ValidatePayload(payload);
            Console.WriteLine($"NODE_ASYNC_CONCURRENT_READ_OK code={ex.ErrorCode} payload={payload}");
        }
    }

    private static async Task RunStreamCloseAsync(DoraNode.DoraNode node)
    {
        var sawInput = false;
        var sawStop = false;

        await foreach (var ev in node.ReadAllEventsAsync())
        {
            using (ev)
            {
                switch (ev.Type)
                {
                    case EventType.Input:
                    {
                        var payload = ReadPayload(ev);
                        ValidatePayload(payload);
                        sawInput = true;
                        break;
                    }
                    case EventType.Stop:
                        sawStop = true;
                        break;
                }
            }
        }

        if (!sawInput)
        {
            Console.Error.WriteLine("Expected the async stream-close scenario to observe at least one input event.");
            Environment.Exit(1);
        }

        Console.WriteLine($"NODE_ASYNC_STREAM_CLOSE_OK sawInput={sawInput} sawStop={sawStop}");
    }

    private static async Task RunDisposePendingReadAsync(DoraNode.DoraNode node)
    {
        var pendingRead = node.NextAsync().AsTask();
        await Task.Delay(100);
        node.Dispose();

        var completedTask = await Task.WhenAny(pendingRead, Task.Delay(TimeSpan.FromSeconds(3)));
        if (!ReferenceEquals(completedTask, pendingRead))
        {
            Console.Error.WriteLine("Expected pending NextAsync to complete after disposing the node.");
            Environment.Exit(1);
        }

        var ev = await pendingRead;
        if (ev is not null)
        {
            using (ev)
            {
            }

            Console.Error.WriteLine("Expected pending NextAsync to complete with null after node disposal.");
            Environment.Exit(1);
        }

        Console.WriteLine("NODE_ASYNC_DISPOSE_OK result=null");
    }

    private static async Task RunNativeFailureAsync(DoraNode.DoraNode node)
    {
        try
        {
            using var unexpected = await node.NextAsync();
            Console.Error.WriteLine("Expected NextAsync to fail with a simulated native read error.");
            Environment.Exit(1);
        }
        catch (DoraException ex) when (ex.ErrorCode == DoraNodeErrorCode.InvalidNativeHandle)
        {
            Console.WriteLine($"NODE_ASYNC_NATIVE_FAILURE_OK code={ex.ErrorCode} operation={ex.Operation}");
        }
    }

    private static string ReadPayload(DoraEvent ev)
    {
        var data = ev.Data;
        if (data is null || data.Length == 0)
        {
            Console.Error.WriteLine("Expected non-empty input payload.");
            Environment.Exit(1);
        }

        return Encoding.UTF8.GetString(data);
    }

    private static void ValidatePayload(string payload)
    {
        if (!string.Equals(payload, ExpectedPayload, StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Expected payload '{ExpectedPayload}' but got '{payload}'.");
            Environment.Exit(1);
        }
    }

    private static AsyncNodeTestMode GetTestMode()
    {
        var rawMode = Environment.GetEnvironmentVariable("DORA_CSHARP_ASYNC_TEST_MODE");
        return rawMode?.Trim().ToLowerInvariant() switch
        {
            null or "" or "normal" => AsyncNodeTestMode.Normal,
            "cancel-before-input" => AsyncNodeTestMode.CancelBeforeInput,
            "mixed-read" => AsyncNodeTestMode.MixedRead,
            "concurrent-read" => AsyncNodeTestMode.ConcurrentRead,
            "stream-close" => AsyncNodeTestMode.StreamClose,
            "dispose-pending-read" => AsyncNodeTestMode.DisposePendingRead,
            "native-failure" => AsyncNodeTestMode.NativeFailure,
            _ => throw new InvalidOperationException($"Unsupported async node test mode '{rawMode}'.")
        };
    }

    private enum AsyncNodeTestMode
    {
        Normal,
        CancelBeforeInput,
        MixedRead,
        ConcurrentRead,
        StreamClose,
        DisposePendingRead,
        NativeFailure
    }
}
