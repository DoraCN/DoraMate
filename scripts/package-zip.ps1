[CmdletBinding()]
param(
    [string]$Version = (Get-Content (Join-Path $PSScriptRoot "..\VERSION") -Raw).Trim(),
    [string]$OutputDir = (Join-Path $PSScriptRoot "..\out\dist"),
    [string]$RepoRoot = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = "0.0.0"
}

$distDir = Join-Path $OutputDir "doramate-$Version"
Write-Host "=== DoraMate v$Version ZIP Packaging ==="
Write-Host "Distribution directory: $distDir"

# Clean any previous dist
if (Test-Path $distDir) {
    Remove-Item -Path $distDir -Recurse -Force
}

# Create directory structure
@("bin", "frontend", "examples", "tools") | ForEach-Object {
    $null = New-Item -ItemType Directory -Path (Join-Path $distDir $_) -Force
}

# 1. LocalAgent binary
$laSource = Join-Path $RepoRoot "target\release\doramate-localagent.exe"
if (-not (Test-Path $laSource)) {
    $laSource = Join-Path $RepoRoot "doramate-localagent\target\release\doramate-localagent.exe"
}
if (Test-Path $laSource) {
    Copy-Item $laSource (Join-Path $distDir "bin\doramate-localagent.exe")
    Write-Host "  [OK] bin/doramate-localagent.exe"
} else {
    Write-Warning "LocalAgent release binary not found. Build with: cargo build --manifest-path doramate-localagent\Cargo.toml --release"
}

# 2. Frontend WASM dist
$feDist = Join-Path $RepoRoot "doramate-frontend\dist"
if (Test-Path $feDist) {
    Copy-Item "$feDist\*" (Join-Path $distDir "frontend\") -Recurse
    Write-Host "  [OK] frontend/ (WASM dist)"
} else {
    Write-Warning "Frontend dist not found at $feDist. Build with: cd doramate-frontend && trunk build --release"
}

# 3. Dora CLI
$doraCliSource = Join-Path $RepoRoot "dora-api-csharp\third_party\dora\target\release\dora.exe"
if (Test-Path $doraCliSource) {
    Copy-Item $doraCliSource (Join-Path $distDir "bin\dora.exe")
    Write-Host "  [OK] bin/dora.exe"
} else {
    Write-Warning "Dora CLI release binary not found at $doraCliSource."
}

# 4. Example dataflows
$exampleDir = Join-Path $RepoRoot "doramate-examples"
if (Test-Path $exampleDir) {
    Get-ChildItem "$exampleDir\*.yml" -File | ForEach-Object {
        Copy-Item $_.FullName (Join-Path $distDir "examples\$($_.Name)")
    }
    Get-ChildItem "$exampleDir\*.layout.json" -File -ErrorAction SilentlyContinue | ForEach-Object {
        Copy-Item $_.FullName (Join-Path $distDir "examples\$($_.Name)")
    }
    Write-Host "  [OK] examples/ (dataflow YAMLs)"
}

# 5. Start script
@"
@echo off
title DoraMate v$Version
echo Starting DoraMate LocalAgent v$Version...
echo.
echo   Web UI: http://127.0.0.1:52100
echo   API:    http://127.0.0.1:52100/api
echo.
start /B "" "%~dp0bin\doramate-localagent.exe"
echo Waiting for LocalAgent to start...
:wait
timeout /t 1 /nobreak >nul
netstat -an 2>nul | findstr ":52100 " >nul
if errorlevel 1 goto wait
echo.
echo DoraMate is ready. Opening browser...
start http://127.0.0.1:52100
echo.
echo Press any key to stop DoraMate...
pause >nul
echo Stopping LocalAgent...
taskkill /F /IM doramate-localagent.exe >nul 2>&1
echo DoraMate stopped.
pause
"@ | Out-File (Join-Path $distDir "start.cmd") -Encoding ascii

# 6. Quick start script (no wait, just launch)
@"
@echo off
title DoraMate
start /B "" "%~dp0bin\doramate-localagent.exe"
start http://127.0.0.1:52100
"@ | Out-File (Join-Path $distDir "start-quick.cmd") -Encoding ascii

# 7. Stop script
@"
@echo off
echo Stopping DoraMate...
taskkill /F /IM doramate-localagent.exe >nul 2>&1
taskkill /F /IM dora.exe >nul 2>&1
echo DoraMate stopped.
pause
"@ | Out-File (Join-Path $distDir "stop.cmd") -Encoding ascii

# 8. README
@"
DoraMate v$Version
==================
Visual editor and runtime for DORA (Dataflow-Oriented Robotic Architecture) dataflows.

Quick Start
-----------
  1. Run start.cmd (or start-quick.cmd to launch immediately)
  2. Open http://127.0.0.1:52100 in your browser
  3. Create or load a dataflow YAML
  4. Click "Run" to execute

Directory Layout
----------------
  bin/         LocalAgent and Dora CLI binaries
  frontend/    Web UI (WASM)
  examples/    Sample dataflow YAML files
  tools/       Utility scripts

System Requirements
-------------------
  - Windows 10/11 or Windows Server 2022+
  - .NET 8 Runtime (for C# nodes; install from https://dotnet.microsoft.com/download)

Links
-----
  Repository: https://github.com/dora-rs/doramate
  DORA:       https://github.com/dora-rs/dora
"@ | Out-File (Join-Path $distDir "README.txt") -Encoding ascii

# Create ZIP
$zipPath = Join-Path $OutputDir "doramate-$Version-win-x64.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Compress-Archive -Path "$distDir\*" -DestinationPath $zipPath -Force
Write-Host ""
Write-Host "=== Package created ==="
Write-Host "  ZIP: $zipPath"
Write-Host "  Size: $([math]::Round((Get-Item $zipPath).Length / 1MB, 2)) MB"

return @{
    Version = $Version
    ZipPath = $zipPath
    DistDir = $distDir
}
