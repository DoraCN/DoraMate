param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Source = "https://api.nuget.org/v3/index.json",
    [string]$ApiKey = $env:NUGET_API_KEY,
    [switch]$SkipBuild,
    [switch]$SkipPack
)

Set-StrictMode -Version Latest
$ErrorActionPreState = $ErrorActionPreference
$ErrorActionPreference = "Stop"

try {
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
    $versionFile = Join-Path $repoRoot "..\VERSION"
    $outDir = Join-Path $repoRoot "artifacts\nuget"

    if (-not (Test-Path $versionFile)) {
        throw "VERSION file not found at '$versionFile'."
    }

    $version = (Get-Content $versionFile -Raw).Trim()
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "VERSION file is empty."
    }

    if ([string]::IsNullOrWhiteSpace($ApiKey)) {
        throw "NuGet API key is required. Pass -ApiKey or set NUGET_API_KEY."
    }

    Write-Host "[publish-nuget] Version: $version"
    Write-Host "[publish-nuget] Source: $Source"

    if (-not $SkipPack) {
        $packArgs = @(
            (Join-Path $PSScriptRoot "package-nuget.ps1")
            "-Configuration", $Configuration
        )

        if ($SkipBuild) {
            $packArgs += "-SkipBuild"
        }

        Write-Host "[publish-nuget] Packaging SDKs and templates..."
        & pwsh @packArgs
        if ($LASTEXITCODE -ne 0) {
            throw "NuGet packaging failed with exit code $LASTEXITCODE"
        }
    }

    $packages = @(
        "DoraMate.DoraNode.$version.nupkg",
        "DoraMate.DoraOperator.$version.nupkg",
        "DoraMate.Templates.$version.nupkg"
    ) | ForEach-Object { Join-Path $outDir $_ }

    foreach ($package in $packages) {
        if (-not (Test-Path $package)) {
            throw "Expected package not found: $package"
        }
    }

    foreach ($package in $packages) {
        $packageName = Split-Path $package -Leaf
        Write-Host "[publish-nuget] Pushing $packageName..."
        dotnet nuget push $package `
            --api-key $ApiKey `
            --source $Source `
            --skip-duplicate `
            --timeout 600

        if ($LASTEXITCODE -ne 0) {
            throw "dotnet nuget push failed for $packageName with exit code $LASTEXITCODE"
        }
    }

    Write-Host "[publish-nuget] Publish completed."
}
finally {
    $ErrorActionPreference = $ErrorActionPreState
}
