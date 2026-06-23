param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreState = $ErrorActionPreference
$ErrorActionPreference = "Stop"

try {
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
    $versionFile = Join-Path $repoRoot "..\VERSION"
    $outDir = Join-Path $repoRoot "artifacts\templates"
    $templateProject = Join-Path $repoRoot "templates\DoraMate.Templates.csproj"

    if (-not (Test-Path $versionFile)) {
        throw "VERSION file not found at '$versionFile'."
    }

    $version = (Get-Content $versionFile -Raw).Trim()
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "VERSION file is empty."
    }

    Write-Host "[build-templates] Version: $version"
    Write-Host "[build-templates] Packing template package..."

    dotnet pack $templateProject `
        -c $Configuration `
        -o $outDir `
        -p:NuGetAudit=false `
        -p:Version=$version `
        -p:Authors="DoraMate" `
        -p:RepositoryUrl=https://github.com/dora-rs/doramate
    if ($LASTEXITCODE -ne 0) {
        throw "Template pack failed with exit code $LASTEXITCODE"
    }

    $package = Join-Path $outDir "DoraMate.Templates.$version.nupkg"
    if (-not (Test-Path $package)) {
        throw "Expected template package not found: $package"
    }

    if ($Force) {
        Write-Host "[build-templates] Uninstalling previous DoraMate template pack..."
        dotnet new uninstall DoraMate.Templates 2>$null
    }

    Write-Host "[build-templates] Installing $([IO.Path]::GetFileName($package))..."
    dotnet new install $package --force
    if ($LASTEXITCODE -ne 0) {
        throw "Template install failed with exit code $LASTEXITCODE"
    }

    Write-Host "[build-templates] Installed templates:"
    dotnet new list | Select-String "dora"
}
finally {
    $ErrorActionPreference = $ErrorActionPreState
}
