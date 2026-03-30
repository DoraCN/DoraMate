# C# Arrow Node-to-Node Dataflow Example

This example validates the new Arrow support in `DoraNode` directly, without going through operators.

- `Producer.cs` builds an Apache Arrow `RecordBatch` and sends it with `DoraNode.SendRecordBatch`.
- `Consumer.cs` receives the input and reads it back with `DoraEvent.TryReadRecordBatch`.

The consumer validates:

- field names
- column count
- row count
- basic types
- representative values

Run it with:

```powershell
dora run .\apis\csharp\examples\csharp-arrow-node-dataflow\dataflow.yml
```

Expected output contains:

```text
NODE_ARROW_ROUNDTRIP_OK fields=name,count,active cols=3 rows=2 types=String,Int32,Boolean
```

## Node Async Error Handling

If you consume this example through `ReadAllEventsAsync(...)` instead of `Next()`, use stable error codes:

```csharp
try
{
    await foreach (var ev in node.ReadAllEventsAsync(stoppingToken))
    {
        using (ev)
        {
            // Read Arrow payload...
        }
    }
}
catch (DoraException ex) when (ex.ErrorCode == DoraNodeErrorCode.InvalidNativeHandle)
{
    logger.LogError(ex, "Arrow node read failed due to a native handle/runtime failure.");
    throw;
}
catch (DoraException ex) when (ex.ErrorCode == DoraNodeErrorCode.LifecycleViolation)
{
    logger.LogWarning(ex, "The node was disposed or sync/async reads were mixed on the same instance.");
}
```

- `InvalidNativeHandle` means the native event stream or payload handle failed while managed code was reading the next event.
- `LifecycleViolation` means the caller used the `DoraNode` outside its valid lifecycle.
