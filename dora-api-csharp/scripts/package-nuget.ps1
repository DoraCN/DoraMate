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

# Build solution first unless skipped
if (-not $SkipBuild) {
    Write-Host "[package-nuget] Building solution ($Configuration)..."
    dotnet build (Join-Path $repoRoot "dora-api-csharp.sln") -c $Configuration -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) {
        throw "Solution build failed with exit code $LASTEXITCODE"
    }
}

$noBuildArg = if ($SkipBuild) { "-p:NoBuild=true" } else { "" }

# Pack DoraNode SDK
Write-Host "[package-nuget] Packing DoraNode..."
dotnet pack (Join-Path $repoRoot "src\DoraNode\DoraNode.csproj") `
    -c $Configuration `
    -o $outDir `
    $noBuildArg `
    -p:NuGetAudit=false `
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
    -p:Authors="DoraMate" `
    -p:RepositoryUrl=https://github.com/dora-rs/doramate
if ($LASTEXITCODE -ne 0) {
    throw "dotnet pack failed for DoraOperator with exit code $LASTEXITCODE"
}

Write-Host "[package-nuget] Packages created in $outDir"
Get-ChildItem $outDir -Filter "*.nupkg" | ForEach-Object {
    Write-Host "  $($_.Name) ($([math]::Round($_.Length / 1KB)) KB)"
}
