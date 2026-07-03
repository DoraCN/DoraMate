# C# OpenTelemetry Operator Dataflow Example

This sample validates OpenTelemetry context propagation through a C# operator:

```text
producer node -> C# operator -> consumer node
```

Run the automated smoke test from `dora-api-csharp`:

```powershell
pwsh ./scripts/smoke-csharp-otel-operator-dataflow.ps1
```

Use `-Restore` on a clean machine if NuGet assets have not been restored yet.

The smoke script verifies that the producer, operator, and consumer share the
same trace id, that the operator parent span is the producer span, and that the
consumer parent span is the operator span.
