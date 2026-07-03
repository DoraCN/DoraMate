param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$samples = @(
    "samples/csharp-dataflow/CSharpNode.csproj",
    "samples/csharp-multi-node/Producer.csproj",
    "samples/csharp-multi-node/Consumer.csproj",
    "samples/csharp-async-node-dataflow/Producer.csproj",
    "samples/csharp-async-node-dataflow/Consumer.csproj",
    "samples/csharp-arrow-node-dataflow/Producer.csproj",
    "samples/csharp-arrow-node-dataflow/Consumer.csproj",
    "samples/csharp-advanced-arrow-node-dataflow/Producer.csproj",
    "samples/csharp-advanced-arrow-node-dataflow/Consumer.csproj",
    "samples/csharp-complex-arrow-contract-node-dataflow/Producer.csproj",
    "samples/csharp-complex-arrow-contract-node-dataflow/Consumer.csproj",
    "samples/csharp-operator-dataflow/CSharpCounterOperator.csproj",
    "samples/csharp-operator-dataflow/Producer.csproj",
    "samples/csharp-operator-dataflow/Sink.csproj",
    "samples/csharp-operator-arrow-roundtrip/RecordBatchProducerOperator.csproj",
    "samples/csharp-operator-arrow-roundtrip/RecordBatchVerifierOperator.csproj",
    "samples/csharp-operator-arrow-roundtrip/Producer.csproj",
    "samples/csharp-operator-arrow-roundtrip/Sink.csproj",
    "samples/csharp-operator-contract-arrow-dataflow/ContractBatchProducerOperator.csproj",
    "samples/csharp-operator-contract-arrow-dataflow/ContractBatchVerifierOperator.csproj",
    "samples/csharp-operator-contract-arrow-dataflow/Producer.csproj",
    "samples/csharp-operator-contract-arrow-dataflow/Sink.csproj",
    "samples/csharp-node-operator-arrow-dataflow/RecordBatchForwardOperator.csproj",
    "samples/csharp-node-operator-arrow-dataflow/Producer.csproj",
    "samples/csharp-node-operator-arrow-dataflow/Consumer.csproj",
    "samples/csharp-otel-dataflow/Producer.csproj",
    "samples/csharp-otel-dataflow/Transform.csproj",
    "samples/csharp-otel-dataflow/Consumer.csproj",
    "samples/csharp-otel-operator-dataflow/Producer.csproj",
    "samples/csharp-otel-operator-dataflow/OtelOperator.csproj",
    "samples/csharp-otel-operator-dataflow/Consumer.csproj",
    "samples/csharp-benchmark-dataflow/BenchmarkProducer.csproj",
    "samples/csharp-benchmark-dataflow/BenchmarkSink.csproj"
)

Push-Location $repoRoot
try {
    $failed = @()
    foreach ($proj in $samples) {
        Write-Host "  Building: $proj" -ForegroundColor Cyan
        dotnet build $proj -c $Configuration -p:NuGetAudit=false 2>&1 | Out-Host
        if ($LASTEXITCODE -ne 0) {
            $failed += $proj
        }
    }

    if ($failed.Count -gt 0) {
        throw "Build failed for $($failed.Count) project(s):`n$($failed -join "`n")"
    }
}
finally {
    Pop-Location
}
