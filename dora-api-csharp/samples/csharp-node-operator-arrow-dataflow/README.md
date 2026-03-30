# C# Node -> Operator -> Node Arrow Example

This example exercises a real Arrow `RecordBatch` path across all three managed surfaces:

- `Producer.cs` builds a `RecordBatch` and sends it from `DoraNode`.
- `RecordBatchForwardOperator.cs` receives the Arrow payload through `DoraOperator`, validates its schema with the managed helper API, and forwards it as a `RecordBatch`.
- `Consumer.cs` receives the forwarded Arrow payload through `DoraNode` and verifies that field names, row count, column count, types, and representative values survived the full hop.

## Build and Run

```powershell
dora run .\apis\csharp\examples\csharp-node-operator-arrow-dataflow\dataflow.yml
```

The expected consumer output contains:

```text
NODE_OPERATOR_NODE_ARROW_OK fields=name,count,active cols=3 rows=2 types=String,Int32,Boolean
```

## Node Async Error Handling

The producer and consumer are still `DoraNode` processes, so the same async read guidance applies if you migrate either side to `ReadAllEventsAsync(...)`:

```csharp
try
{
    await foreach (var ev in node.ReadAllEventsAsync(stoppingToken))
    {
        using (ev)
        {
            // Read or forward RecordBatch payloads...
        }
    }
}
catch (DoraException ex) when (ex.ErrorCode == DoraNodeErrorCode.InvalidNativeHandle)
{
    logger.LogError(ex, "Node/operator/node async read failed due to a native handle/runtime failure.");
    throw;
}
catch (DoraException ex) when (ex.ErrorCode == DoraNodeErrorCode.LifecycleViolation)
{
    logger.LogWarning(ex, "The node was disposed or sync/async reads were mixed on the same DoraNode instance.");
}
```

- `InvalidNativeHandle`: fail fast and investigate the native runtime/ABI boundary.
- `LifecycleViolation`: fix the managed read lifecycle, especially disposal timing and mixed sync/async access.
