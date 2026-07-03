param(
    [string]$Python = "python",
    [switch]$NoDeps
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$args = @(
    (Join-Path $repoRoot "dora-api-csharp\scripts\install-python-dora-runtime.ps1"),
    "-Python", $Python
)

if ($NoDeps) {
    $args += "-NoDeps"
}

& pwsh @args
if ($LASTEXITCODE -ne 0) {
    throw "Delegated install-python-dora-runtime failed with exit code $LASTEXITCODE."
}
