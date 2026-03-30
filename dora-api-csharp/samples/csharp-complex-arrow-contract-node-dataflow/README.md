# C# Complex Arrow Contract Dataflow

This example exercises Dora's higher-level C# Arrow data path with:

- `Decimal128`
- `List<String>`
- `Struct { source: String, priority: Int32 }`
- schema-contract based model projection via `TryReadModel(...)`

Run it with:

```powershell
dora run .\apis\csharp\examples\csharp-complex-arrow-contract-node-dataflow\dataflow.yml
```

Expected success output:

```text
NODE_ARROW_COMPLEX_OK fields=id,amount,tags,meta cols=4 rows=2 types=Int32,Decimal128,List,Struct
```

## Node Async Error Handling

For async contract readers, branch on `DoraNodeErrorCode` instead of inspecting exception text:

```csharp
try
{
    await foreach (var ev in node.ReadAllEventsAsync(stoppingToken))
    {
        using (ev)
        {
            // TryReadModel(...), TryProjectRows(...), etc.
        }
    }
}
catch (DoraException ex) when (ex.ErrorCode == DoraNodeErrorCode.InvalidNativeHandle)
{
    logger.LogError(ex, "Contract projection failed because the native event stream surfaced an invalid handle.");
    throw;
}
catch (DoraException ex) when (ex.ErrorCode == DoraNodeErrorCode.LifecycleViolation)
{
    logger.LogWarning(ex, "The node was disposed or sync/async reads were mixed on the same node instance.");
}
```

- `InvalidNativeHandle`: native event-stream/runtime failure, not a contract validation failure.
- `LifecycleViolation`: caller misuse of the node/event lifecycle.
