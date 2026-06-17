param(
    [int]$Rounds = 20
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($Rounds -lt 20 -or $Rounds -gt 50) {
    throw "Standard release gate requires Rounds between 20 and 50."
}

& pwsh (Join-Path $PSScriptRoot "release-gate-local-runtime.ps1") -Rounds $Rounds -UseStandardReleaseProfile
if ($LASTEXITCODE -ne 0) {
    throw "Standard release gate failed with exit code $LASTEXITCODE."
}
