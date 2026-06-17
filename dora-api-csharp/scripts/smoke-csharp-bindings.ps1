param(
    [ValidateSet("baseline", "standard", "complete")]
    [string]$Profile = "complete",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [int]$Rounds = 1,
    [string]$OutputDir = "",
    [switch]$SkipBuild,
    [switch]$KeepWorkingDirOut,
    [switch]$KeepLocalAgentAlive
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$args = @(
    (Join-Path $repoRoot "scripts\smoke-csharp-bindings.ps1"),
    "-Profile", $Profile,
    "-Configuration", $Configuration,
    "-Rounds", $Rounds
)

if (-not [string]::IsNullOrWhiteSpace($OutputDir)) {
    $args += @("-OutputDir", $OutputDir)
}
if ($SkipBuild) {
    $args += "-SkipBuild"
}
if ($KeepWorkingDirOut) {
    $args += "-KeepWorkingDirOut"
}
if ($KeepLocalAgentAlive) {
    $args += "-KeepLocalAgentAlive"
}

& pwsh @args
if ($LASTEXITCODE -ne 0) {
    throw "Delegated smoke-csharp-bindings failed with exit code $LASTEXITCODE."
}
