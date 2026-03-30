# C# Operator Dataflow Example

This example demonstrates the minimum shared-library operator flow for the C# bindings:

- `Producer.cs` is a standalone C# node that emits UTF-8 messages on a timer.
- `CSharpCounterOperator.cs` is a C# operator compiled as a native shared library with NativeAOT.
- `Sink.cs` is a standalone C# node that prints the operator output.

## Files

- `Producer.csproj` / `Producer.cs`
- `CSharpCounterOperator.csproj` / `CSharpCounterOperator.cs`
- `Sink.csproj` / `Sink.cs`
- `dataflow.yml`

## Build and Run

Before running it, make sure these prerequisites are available:

- .NET SDK 8.0+
- Rust toolchain
- NativeAOT prerequisites for your platform

With the updated `dora run` local-build flow in this repo, the example can be started directly with:

```powershell
dora run .\apis\csharp\examples\csharp-operator-dataflow\dataflow.yml
```

If you are using an older `dora` binary that does not auto-build on `run`, use:

```powershell
dora build --local .\apis\csharp\examples\csharp-operator-dataflow\dataflow.yml
dora run .\apis\csharp\examples\csharp-operator-dataflow\dataflow.yml
```

## Notes

- The operator build step publishes a native shared library, not a normal managed DLL.
- `dataflow.yml` now builds both `dora-node-api-c` and `dora-operator-api-c`, so producer/sink/operator can all resolve the expected native ABI without manual pre-copy steps.
- The operator runtime helper library `dora_operator_api_c` is copied into the publish output by the project file when it is available under `target/release`.
- `Directory.Build.props` assigns each example project its own `obj/<ProjectName>/` directory so `Producer`, `Sink`, and `CSharpCounterOperator` do not overwrite each other's intermediate outputs.
- `dataflow.yml` sets `DOTNET_SKIP_FIRST_TIME_EXPERIENCE`, `DOTNET_CLI_HOME`, and `DOTNET_CLI_TELEMETRY_OPTOUT`, and each `dotnet build/publish` command uses `--packages ../../../../.dotnet-cli/.nuget/packages` so Dora-triggered local builds use the repository-local .NET/NuGet state instead of depending on a writable user profile.
- The current `dataflow.yml` is verified for Windows and uses the `win-x64` NativeAOT publish output.

## Error-Code Samples

This example is also the runnable negative-test sample for two stable `DoraOperatorErrorCode` categories:

- `LifecycleViolation`
- `InvalidNativeHandle`

The recommended consumption pattern is to catch `DoraOperatorException` and branch on `ErrorCode`, not on exception text:

```csharp
try
{
    _ = savedInput.GetUtf8String();
}
catch (DoraOperatorException ex) when (ex.ErrorCode == DoraOperatorErrorCode.LifecycleViolation)
{
    output.SendOrThrow("counter", $"EXPECTED_LIFECYCLE_VIOLATION_OK code={ex.ErrorCode}");
}
```

```csharp
try
{
    _ = invalidInput.GetUtf8String();
}
catch (DoraOperatorException ex) when (ex.ErrorCode == DoraOperatorErrorCode.InvalidNativeHandle)
{
    output.SendOrThrow("counter", $"EXPECTED_INVALID_NATIVE_HANDLE_OK code={ex.ErrorCode}");
}
```

These two smoke modes can be triggered with:

```powershell
$env:DORA_CSHARP_OPERATOR_TEST_MODE = "lifecycle-violation"
dora run .\apis\csharp\examples\csharp-operator-dataflow\dataflow.yml

$env:DORA_CSHARP_OPERATOR_TEST_MODE = "invalid-native-handle"
dora run .\apis\csharp\examples\csharp-operator-dataflow\dataflow.yml
```

Interpretation guidance:

- `LifecycleViolation` means managed code tried to use Dora-native event/input state after the callback lifetime ended. The fix is to materialize bytes or copy the managed data you need during `OnInput`, not later.
- `InvalidNativeHandle` means the wrapper ended up with a missing or invalid native payload handle. Treat it as a runtime/ABI failure, not a recoverable business validation error.
