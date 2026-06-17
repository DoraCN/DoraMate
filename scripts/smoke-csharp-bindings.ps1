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

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$csharpRoot = Join-Path $repoRoot "dora-api-csharp"

if (-not $SkipBuild) {
    New-Item -ItemType Directory -Force -Path (Join-Path $csharpRoot "artifacts\packages") | Out-Null

    Push-Location $csharpRoot
    try {
        dotnet build src/DoraNode/DoraNode.csproj -c $Configuration -p:NuGetAudit=false
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to build DoraNode."
        }

        dotnet build src/DoraOperator/DoraOperator.csproj -c $Configuration -p:NuGetAudit=false
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to build DoraOperator."
        }

        dotnet build templates/DoraMate.Templates.csproj -c $Configuration -p:NuGetAudit=false
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to build DoraMate.Templates."
        }

        & pwsh (Join-Path $csharpRoot "scripts\build-csharp-sample-projects.ps1") -Configuration $Configuration
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to build C# sample projects."
        }
    }
    finally {
        Pop-Location
    }
}

$smokeArgs = @(
    (Join-Path $repoRoot "scripts\smoke-localagent-multi-dataflow.ps1"),
    "-Profile", $Profile,
    "-Rounds", $Rounds
)

if (-not [string]::IsNullOrWhiteSpace($OutputDir)) {
    $smokeArgs += @("-OutputDir", $OutputDir)
}
if ($KeepWorkingDirOut) {
    $smokeArgs += "-KeepWorkingDirOut"
}
if ($KeepLocalAgentAlive) {
    $smokeArgs += "-KeepLocalAgentAlive"
}

& pwsh @smokeArgs
if ($LASTEXITCODE -ne 0) {
    throw "C# bindings smoke failed with exit code $LASTEXITCODE."
}
