# C# OpenTelemetry Dataflow Example

This sample shows Dora OpenTelemetry context propagation through C# nodes:

```text
producer -> transform -> consumer
```

It uses only `System.Diagnostics.Activity` and `ActivitySource`. No Jaeger, Tempo, or OpenTelemetry Collector is required for the local check.

## Run

From `dora-api-csharp`:

```powershell
pwsh ./scripts/build-native.ps1
dora run ./samples/csharp-otel-dataflow/dataflow.yml
```

Or run the automated smoke test:

```powershell
pwsh ./scripts/smoke-csharp-otel-dataflow.ps1
```

Use `-Restore` on a clean machine if NuGet assets have not been restored yet.

## What To Look For

The three nodes print trace identifiers:

```text
PRODUCER trace=... span=... payload=otel-message-1
TRANSFORM trace=... parent=... upstream_span=... span=... payload=otel-message-1 output=transformed:otel-message-1
CONSUMER trace=... parent=... upstream_trace=... payload=transformed:otel-message-1
```

For a single message:

- `PRODUCER trace`, `TRANSFORM trace`, and `CONSUMER trace` should match.
- `TRANSFORM parent` should match the producer span.
- `CONSUMER parent` should match the transform span.

The smoke script enforces these checks automatically by correlating a producer
payload with the transform output consumed by the final node.

## How It Works

- `Producer` starts a root `Activity` with a local `ActivitySource`.
- `node.SendOutput(...)` automatically injects `Activity.Current` into Dora metadata.
- `Transform` calls `ev.StartActivity(...)`, which uses the upstream Dora metadata as parent.
- `Transform` sends output while its activity is current, so Dora metadata is updated for `Consumer`.
- `Consumer` calls `ev.StartActivity(...)` and continues the same trace.

Automatic injection can be disabled:

```csharp
DoraTelemetry.AutoInjectCurrentActivity = false;
```

Explicit context can be passed when needed:

```csharp
node.SendOutput("data", payload, activity.Context);
```
