# C# Async Node Dataflow Example

This example demonstrates the first-stage `DoraNode` async API:

- `Producer.cs` emits a single UTF-8 message
- `Consumer.cs` consumes it through `NextAsync(...)` or `ReadAllEventsAsync(...)`
- `dataflow.yml` builds `dora-node-api-c` and the two C# executables on demand

## Run

```powershell
dora run .\apis\csharp\examples\csharp-async-node-dataflow\dataflow.yml
```

## Covered Modes

The example doubles as the runnable smoke sample for the current async-read boundaries:

- `normal`
- `cancel-before-input`
- `mixed-read`
- `concurrent-read`
- `stream-close`
- `dispose-pending-read`
- `native-failure`

You can switch modes with:

```powershell
$env:DORA_CSHARP_ASYNC_TEST_MODE = "native-failure"
$env:DORA_CSHARP_SIMULATE_NODE_ASYNC_NATIVE_FAILURE = "invalid-native-handle"
dora run .\apis\csharp\examples\csharp-async-node-dataflow\dataflow.yml
```

`DORA_CSHARP_SIMULATE_NODE_ASYNC_NATIVE_FAILURE` is a smoke/example-only hook used to validate stable error-code mapping for the async read path. It is not intended as a production feature flag.

## Recommended Error Handling

Prefer branching on `DoraException.ErrorCode`, not on exception text:

```csharp
try
{
    await foreach (var ev in node.ReadAllEventsAsync(stoppingToken))
    {
        using (ev)
        {
            // Handle event...
        }
    }
}
catch (DoraException ex) when (ex.ErrorCode == DoraNodeErrorCode.InvalidNativeHandle)
{
    Console.Error.WriteLine($"runtime/ABI failure while reading async events: {ex.ErrorCode}");
    throw;
}
catch (DoraException ex) when (ex.ErrorCode == DoraNodeErrorCode.LifecycleViolation)
{
    Console.Error.WriteLine($"invalid async node lifecycle usage: {ex.ErrorCode}");
}
```

Interpretation:

- `InvalidNativeHandle`: the native event stream failed or surfaced an invalid handle while managed code was reading the next event. Treat this as a runtime/ABI failure.
- `LifecycleViolation`: the node was already disposed, or sync/async reads were mixed on the same `DoraNode` instance. Fix the caller logic instead of retrying.
