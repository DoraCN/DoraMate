param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreState = $ErrorActionPreference
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$outDir = Join-Path $repoRoot "artifacts\nuget"

# Read version from VERSION file
$versionFile = Join-Path $repoRoot "..\VERSION"
if (Test-Path $versionFile) {
    $Version = (Get-Content $versionFile -Raw).Trim()
} else {
    $Version = "0.0.0"
    Write-Warning "VERSION file not found at '$versionFile'. Using '$Version' as fallback."
}
Write-Host "[package-nuget] Version: $Version (from VERSION)"

# Build solution first unless skipped
if (-not $SkipBuild) {
    Write-Host "[package-nuget] Building solution ($Configuration)..."
    dotnet build (Join-Path $repoRoot "dora-api-csharp.sln") -c $Configuration -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) {
        throw "Solution build failed with exit code $LASTEXITCODE"
    }
}

$noBuildArg = if ($SkipBuild) { "--no-build" } else { "" }

# Pack DoraNode SDK
Write-Host "[package-nuget] Packing DoraNode..."
dotnet pack (Join-Path $repoRoot "src\DoraNode\DoraNode.csproj") `
    -c $Configuration `
    -o $outDir `
    $noBuildArg `
    -p:NuGetAudit=false `
    -p:Version=$Version `
    -p:Authors="DoraMate" `
    -p:RepositoryUrl=https://github.com/dora-rs/doramate
if ($LASTEXITCODE -ne 0) {
    throw "dotnet pack failed for DoraNode with exit code $LASTEXITCODE"
}

# Pack DoraOperator SDK
Write-Host "[package-nuget] Packing DoraOperator..."
dotnet pack (Join-Path $repoRoot "src\DoraOperator\DoraOperator.csproj") `
    -c $Configuration `
    -o $outDir `
    $noBuildArg `
    -p:NuGetAudit=false `
    -p:Version=$Version `
    -p:Authors="DoraMate" `
    -p:RepositoryUrl=https://github.com/dora-rs/doramate
if ($LASTEXITCODE -ne 0) {
    throw "dotnet pack failed for DoraOperator with exit code $LASTEXITCODE"
}

# Pack dotnet new template package
Write-Host "[package-nuget] Packing DoraMate.Templates..."
dotnet pack (Join-Path $repoRoot "templates\DoraMate.Templates.csproj") `
    -c $Configuration `
    -o $outDir `
    $noBuildArg `
    -p:NuGetAudit=false `
    -p:Version=$Version `
    -p:Authors="DoraMate" `
    -p:RepositoryUrl=https://github.com/dora-rs/doramate
if ($LASTEXITCODE -ne 0) {
    throw "dotnet pack failed for DoraMate.Templates with exit code $LASTEXITCODE"
}

Write-Host "[package-nuget] Packages created in $outDir"
Get-ChildItem $outDir -Filter "*.nupkg" | ForEach-Object {
    Write-Host "  $($_.Name) ($([math]::Round($_.Length / 1KB)) KB)"
}
