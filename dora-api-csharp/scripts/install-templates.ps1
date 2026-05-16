param([switch]$Force)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$templatesRoot = Join-Path $repoRoot "templates"

# Uninstall any previous versions
if ($Force) {
    Write-Host "[install-templates] Uninstalling previous versions..."
    dotnet new uninstall DoraMate.DoraNode.Template 2>$null
    dotnet new uninstall DoraMate.DoraOperator.Template 2>$null
}

# Install from local paths
Write-Host "[install-templates] Installing dora-node template..."
dotnet new install (Join-Path $templatesRoot "dora-node")
if ($LASTEXITCODE -ne 0) { throw "Failed to install dora-node template" }

Write-Host "[install-templates] Installing dora-operator template..."
dotnet new install (Join-Path $templatesRoot "dora-operator")
if ($LASTEXITCODE -ne 0) { throw "Failed to install dora-operator template" }

# Verify
Write-Host "[install-templates] Installed templates:"
dotnet new list | Select-String "dora"

Write-Host "[install-templates] Success."
