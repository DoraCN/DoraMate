param(
    [string]$Python = "python",
    [switch]$NoDeps
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$csharpRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$pythonApiRoot = Join-Path $csharpRoot "third_party\dora\apis\python\node"

if (-not (Test-Path -LiteralPath (Join-Path $pythonApiRoot "pyproject.toml"))) {
    throw "Python Dora API source was not found at $pythonApiRoot."
}

$installArgs = @(
    "-m", "pip", "install",
    "--force-reinstall",
    $pythonApiRoot
)

if ($NoDeps) {
    $installArgs = @(
        "-m", "pip", "install",
        "--force-reinstall",
        "--no-deps",
        $pythonApiRoot
    )
}

Write-Host "[install-python-dora-runtime] Installing dora-rs from $pythonApiRoot..." -ForegroundColor Cyan
& $Python @installArgs
if ($LASTEXITCODE -ne 0) {
    throw "Failed to install vendored Python dora-rs package with exit code $LASTEXITCODE."
}

& $Python -c "import dora; print('Python dora-rs:', dora.__version__, dora.__file__)"
if ($LASTEXITCODE -ne 0) {
    throw "Failed to import Python dora package after installation."
}
