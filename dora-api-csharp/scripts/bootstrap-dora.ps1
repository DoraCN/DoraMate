param(
    [string]$DoraRepo = "https://github.com/dora-rs/dora",
    [string]$DoraRef = "main"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$vendorRoot = Join-Path $root "third_party"
$vendorPath = Join-Path $vendorRoot "dora"
$tempPath = Join-Path $vendorRoot "dora.__bootstrap_tmp"

function Remove-DirectoryIfPresent([string]$PathToRemove) {
    if (-not (Test-Path $PathToRemove)) {
        return
    }

    $resolved = (Resolve-Path $PathToRemove).Path
    if (-not $resolved.StartsWith($vendorRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to delete path outside vendor root: $resolved"
    }

    Remove-Item -LiteralPath $resolved -Recurse -Force
}

New-Item -ItemType Directory -Path $vendorRoot -Force | Out-Null

Push-Location $root
try {
    Remove-DirectoryIfPresent $tempPath
    git clone $DoraRepo $tempPath | Out-Host

    git -C $tempPath checkout $DoraRef | Out-Host

    $tempGitDir = Join-Path $tempPath ".git"
    if (Test-Path $tempGitDir) {
        Remove-Item -LiteralPath $tempGitDir -Recurse -Force
    }

    Remove-DirectoryIfPresent $vendorPath
    Move-Item -LiteralPath $tempPath -Destination $vendorPath
}
catch {
    Remove-DirectoryIfPresent $tempPath
    throw
}
finally {
    Pop-Location
}

Write-Host "Vendored Dora snapshot is ready at: $vendorPath"
