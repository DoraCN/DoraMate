# C# Advanced Arrow Node-to-Node Dataflow Example

This example exercises higher-level Arrow types in `DoraNode` directly.

- `Producer.cs` builds a `RecordBatch` with `Int32`, `Date32`, `Timestamp`, and `Binary` columns.
- `Consumer.cs` reads the batch back with `TryReadExpectedRecordBatch(...)` and validates both schema shape and typed values.

Run it with:

```powershell
dora run .\apis\csharp\examples\csharp-advanced-arrow-node-dataflow\dataflow.yml
```

Expected output contains:

```text
NODE_ARROW_ADVANCED_OK fields=id,created,event_time,payload cols=4 rows=2 types=Int32,Date32,Timestamp,Binary
```

## Node Async Error Handling

If this consumer is rewritten to use `NextAsync(...)` or `ReadAllEventsAsync(...)`, the recommended error handling is:

```csharp
try
{
    await foreach (var ev in node.ReadAllEventsAsync(stoppingToken))
    {
        using (ev)
        {
            // Validate schema and typed values...
        }
    }
}
catch (DoraException ex) when (ex.ErrorCode == DoraNodeErrorCode.InvalidNativeHandle)
{
    logger.LogError(ex, "Advanced Arrow async read failed due to a native handle/runtime failure.");
    throw;
}
catch (DoraException ex) when (ex.ErrorCode == DoraNodeErrorCode.LifecycleViolation)
{
    logger.LogWarning(ex, "The node was disposed or sync/async reads were mixed on the same DoraNode instance.");
}
```

- `InvalidNativeHandle`: fail fast and treat it as a native runtime/ABI problem.
- `LifecycleViolation`: fix application logic rather than retrying.
