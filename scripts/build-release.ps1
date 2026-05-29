[CmdletBinding()]
param(
    [switch]$SkipTests,
    [switch]$SkipPackaging,
    [string]$Version = "",
    [string]$OutputDir = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Get-Content (Join-Path $repoRoot "VERSION") -Raw).Trim()
}
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "out\dist"
}

Write-Host "╔══════════════════════════════════════════════╗"
Write-Host "║       DoraMate v$Version Release Build       ║"
Write-Host "╚══════════════════════════════════════════════╝"
Write-Host ""

# ── Step 1: Version consistency check ──
Write-Host "=== Step 1: Version Consistency Check ==="

function Get-CargoVersion {
    param([string]$ProjectDir)
    $cargoToml = Join-Path $ProjectDir "Cargo.toml"
    $versionLine = Select-String '^version = "([^"]+)"' (Get-Content $cargoToml -Raw) | Select-Object -First 1
    if ($versionLine) {
        return $versionLine.Matches[0].Groups[1].Value
    }
    return $null
}

$laVersion = Get-CargoVersion -ProjectDir (Join-Path $repoRoot "doramate-localagent")
$feVersion = Get-CargoVersion -ProjectDir (Join-Path $repoRoot "doramate-frontend")

$errors = @()
if ($laVersion -ne $Version) {
    $errors += "LocalAgent version mismatch: VERSION=$Version, Cargo.toml=$laVersion"
}
if ($feVersion -ne $Version) {
    $errors += "Frontend version mismatch: VERSION=$Version, Cargo.toml=$feVersion"
}
# Check C# SDK versions
$csharpVersions = @("DoraNode", "DoraOperator") | ForEach-Object {
    $csproj = Join-Path $repoRoot "dora-api-csharp\src\$_\$_.csproj"
    if (Test-Path $csproj) {
        $content = Get-Content $csproj -Raw
        if ($content -match '<Version>([^<]+)</Version>') {
            if ($matches[1] -ne $Version) {
                $errors += "$_ version mismatch: VERSION=$Version, csproj=$($matches[1])"
            }
        }
    }
}

if ($errors.Count -gt 0) {
    Write-Host "Version check FAILED:"
    $errors | ForEach-Object { Write-Host "  - $_" }
    Write-Host ""
    $answer = Read-Host "Continue anyway? (y/N)"
    if ($answer -ne "y") {
        throw "Version consistency check failed. Aborting."
    }
} else {
    Write-Host "  [OK] All components at v$Version"
}

# ── Step 2: Run standard release gate ──
if (-not $SkipTests) {
    Write-Host ""
    Write-Host "=== Step 2: Standard Release Gate ==="
    $gateScript = Join-Path $PSScriptRoot "release-gate-local-runtime-standard.ps1"
    if (Test-Path $gateScript) {
        & $gateScript -Rounds 20
        if ($LASTEXITCODE -ne 0) {
            throw "Release gate failed (exit code $LASTEXITCODE)."
        }
        Write-Host "  [OK] Standard release gate passed (20 rounds)"
    } else {
        Write-Warning "Gate script not found at $gateScript. Skipping."
    }
}

# ── Step 3: Build LocalAgent (release) ──
Write-Host ""
Write-Host "=== Step 3: Build LocalAgent (release) ==="
Push-Location (Join-Path $repoRoot "doramate-localagent")
try {
    cargo build --bin doramate-localagent --release
    if ($LASTEXITCODE -ne 0) {
        throw "cargo build (release) failed with exit code $LASTEXITCODE"
    }
    Write-Host "  [OK] target/release/doramate-localagent.exe"
} finally {
    Pop-Location
}

# ── Step 4: Build Frontend (release) ──
Write-Host ""
Write-Host "=== Step 4: Build Frontend (release WASM) ==="
Push-Location (Join-Path $repoRoot "doramate-frontend")
try {
    trunk build --release
    if ($LASTEXITCODE -ne 0) {
        throw "trunk build (release) failed with exit code $LASTEXITCODE"
    }
    Write-Host "  [OK] dist/ (WASM frontend)"
} finally {
    Pop-Location
}

# ── Step 5: Build Dora CLI (release) ──
Write-Host ""
Write-Host "=== Step 5: Build Dora CLI (release) ==="
$doraDir = Join-Path $repoRoot "dora-api-csharp\third_party\dora"
if (Test-Path $doraDir) {
    Push-Location $doraDir
    try {
        cargo build -p dora-cli --release
        if ($LASTEXITCODE -ne 0) {
            throw "Dora CLI build failed with exit code $LASTEXITCODE"
        }
        Write-Host "  [OK] target/release/dora.exe"
    } finally {
        Pop-Location
    }
} else {
    Write-Warning "Dora source not found at $doraDir. Run bootstrap-dora.ps1 first."
}

# ── Step 6: Package ──
if (-not $SkipPackaging) {
    Write-Host ""
    Write-Host "=== Step 6: Package ==="
    $zipScript = Join-Path $PSScriptRoot "package-zip.ps1"
    if (Test-Path $zipScript) {
        & $zipScript -Version $Version -OutputDir $OutputDir
        if ($LASTEXITCODE -ne 0) {
            throw "ZIP packaging failed with exit code $LASTEXITCODE"
        }
    } else {
        Write-Warning "ZIP script not found at $zipScript. Skipping packaging."
    }
}

Write-Host ""
Write-Host "╔══════════════════════════════════════════════╗"
Write-Host "║     DoraMate v$Version Build Complete        ║"
Write-Host "╚══════════════════════════════════════════════╝"
Write-Host ""
Write-Host "Output artifacts:"
Write-Host "  - bin:      target/release/doramate-localagent.exe"
Write-Host "  - frontend: doramate-frontend/dist/"
Write-Host "  - dora CLI: dora-api-csharp/third_party/dora/target/release/dora.exe"
if (-not $SkipPackaging) {
    Write-Host "  - ZIP:      $OutputDir/doramate-$Version-win-x64.zip"
}
