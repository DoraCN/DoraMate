param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [int]$TimeoutSeconds = 30,
    [string]$DoraPath = "dora",
    [string]$OutputDir = "",
    [switch]$SkipBuild,
    [switch]$Restore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$args = @(
    (Join-Path $repoRoot "dora-api-csharp\scripts\smoke-csharp-otel-dataflow.ps1"),
    "-Configuration", $Configuration,
    "-TimeoutSeconds", $TimeoutSeconds,
    "-DoraPath", $DoraPath
)

if (-not [string]::IsNullOrWhiteSpace($OutputDir)) {
    $args += @("-OutputDir", $OutputDir)
}
if ($SkipBuild) {
    $args += "-SkipBuild"
}
if ($Restore) {
    $args += "-Restore"
}

& pwsh @args
if ($LASTEXITCODE -ne 0) {
    throw "Delegated smoke-csharp-otel-dataflow failed with exit code $LASTEXITCODE."
}
