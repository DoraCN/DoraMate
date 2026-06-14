param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$doraRoot = Join-Path $repoRoot "third_party\dora"

if (-not (Test-Path $doraRoot)) {
    throw "Vendored Dora source not found at '$doraRoot'. Run ./scripts/bootstrap-dora.ps1 first."
}

function Get-RuntimeIdentifier {
    if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
        return "win-x64"
    }

    if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::OSX)) {
        return "osx-x64"
    }

    return "linux-x64"
}

function Get-NativeLibraryNames([string]$RuntimeIdentifier) {
    switch ($RuntimeIdentifier) {
        "win-x64" {
            return @{
                Node = "dora_node_api_c.dll"
                Operator = "dora_operator_api_c.dll"
            }
        }
        "osx-x64" {
            return @{
                Node = "libdora_node_api_c.dylib"
                Operator = "libdora_operator_api_c.dylib"
            }
        }
        default {
            return @{
                Node = "libdora_node_api_c.so"
                Operator = "libdora_operator_api_c.so"
            }
        }
    }
}

function Resolve-BuiltLibraryPath([string]$TargetDirectory, [string]$LibraryFileName) {
    $primaryCandidate = Join-Path $TargetDirectory $LibraryFileName
    if (Test-Path $primaryCandidate) {
        return $primaryCandidate
    }

    $depsDirectory = Join-Path $TargetDirectory "deps"
    if (-not (Test-Path $depsDirectory)) {
        return $null
    }

    $libraryBaseName = [System.IO.Path]::GetFileNameWithoutExtension($LibraryFileName)
    $libraryExtension = [System.IO.Path]::GetExtension($LibraryFileName)
    $pattern = "$libraryBaseName-*$libraryExtension"

    return Get-ChildItem -Path $depsDirectory -Filter $pattern -File |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

$runtimeIdentifier = Get-RuntimeIdentifier
$cargoProfile = if ($Configuration -eq "Release") { "release" } else { "debug" }
$packages = @(
    "dora-node-api-c",
    "dora-operator-api-c"
)

Write-Host "[build-native] Building Dora C ABI crates for $runtimeIdentifier ($Configuration)..." -ForegroundColor Cyan

Push-Location $doraRoot
try {
    foreach ($package in $packages) {
        $cargoArgs = @(
            "rustc",
            "--locked",
            "-p", $package
        )

        if ($Configuration -eq "Release") {
            $cargoArgs += "--release"
        }

        $cargoArgs += @(
            "--",
            "--crate-type", "cdylib"
        )

        Write-Host "[build-native] cargo $($cargoArgs -join ' ')" -ForegroundColor DarkCyan
        & cargo @cargoArgs
        if ($LASTEXITCODE -ne 0) {
            throw "cargo rustc failed for $package with exit code $LASTEXITCODE"
        }
    }
}
finally {
    Pop-Location
}

$targetDir = Join-Path $doraRoot "target\$cargoProfile"
$artifactDir = Join-Path $repoRoot "artifacts\native\$runtimeIdentifier"
$libraryNames = Get-NativeLibraryNames $runtimeIdentifier

New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

foreach ($library in @($libraryNames.Node, $libraryNames.Operator)) {
    $sourcePath = Resolve-BuiltLibraryPath -TargetDirectory $targetDir -LibraryFileName $library
    if (-not $sourcePath) {
        throw "Expected native library not found for '$library' under '$targetDir' or '$targetDir\\deps'."
    }

    $destinationPath = Join-Path $artifactDir $library
    Copy-Item -LiteralPath $sourcePath -Destination $destinationPath -Force
    Write-Host "[build-native] Copied $library -> $destinationPath" -ForegroundColor Green
}

Write-Host "[build-native] Native artifacts are ready in $artifactDir" -ForegroundColor Green
