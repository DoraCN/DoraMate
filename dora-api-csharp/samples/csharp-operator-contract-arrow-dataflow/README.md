# Standalone C# Operator Contract Arrow Dataflow

This sample exercises the higher-level `DoraOperator` contract API in the standalone repository:

- `ContractBatchProducerOperator.cs` emits a complex Arrow `RecordBatch`.
- `ContractBatchVerifierOperator.cs` validates it with `Input.TryReadModel(...)`.
- The payload includes `Decimal256`, `List<Int32>`, `Map<String, Int32>`, and nested `Struct` content.

## Run

```powershell
dora run .\samples\csharp-operator-contract-arrow-dataflow\dataflow.yml
```

Expected sink output:

```text
Sink received: OPERATOR_ARROW_CONTRACT_OK fields=id,budget,scores,metrics,details cols=5 rows=2 types=Int32,Decimal256,List,Map,Struct
```

## Smoke

```powershell
pwsh .\scripts\smoke-doraoperator-contract-arrow.ps1
```
