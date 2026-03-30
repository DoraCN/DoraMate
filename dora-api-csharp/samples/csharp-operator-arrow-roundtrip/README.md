# Standalone C# Arrow RecordBatch Round-Trip

This sample exercises the standalone `DoraOperator` Arrow bridge with a full Dora runtime flow:

- `Producer.cs` emits a single trigger message.
- `RecordBatchProducerOperator.cs` builds an Apache Arrow `RecordBatch` and sends it through `DoraOutputPayload.RecordBatchPayload(...)`.
- `RecordBatchVerifierOperator.cs` validates that payload using `TryReadExpectedRecordBatch(...)` and `ArrowRecordBatchAssertions`.
- `Sink.cs` prints the verification summary.

## Run

```powershell
dora run .\samples\csharp-operator-arrow-roundtrip\dataflow.yml
```

Expected sink output:

```text
Sink received: ARROW_ROUNDTRIP_OK fields=name,count,active,total,ratio,score cols=6 rows=2 types=String,Int32,Boolean,Int64,Float,Double
```

## Smoke

```powershell
pwsh .\scripts\smoke-doraoperator-arrow-roundtrip.ps1
```
