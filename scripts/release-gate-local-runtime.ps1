param(
    [int]$Rounds = 3,
    [switch]$SkipLocalAgentTests,
    [switch]$SkipFrontendTests,
    [switch]$UseStandardReleaseProfile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$outputDir = Join-Path $repoRoot "out\release-gates"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$summaryPath = Join-Path $outputDir "release-gate-local-runtime-$timestamp.json"
$summary = [ordered]@{
    gate_name = "release-gate-local-runtime"
    started_at = (Get-Date).ToString("o")
    rounds = $Rounds
    use_standard_release_profile = [bool]$UseStandardReleaseProfile
    localagent_tests_passed = $SkipLocalAgentTests
    frontend_tests_passed = $SkipFrontendTests
    smoke_passed = $false
    passed = $false
    fatal_message = $null
}

try {
    if (-not $SkipLocalAgentTests) {
        Push-Location (Join-Path $repoRoot "doramate-localagent")
        try {
            cargo test --locked 2>&1 | Out-Host
            if ($LASTEXITCODE -ne 0) {
                throw "doramate-localagent cargo test failed."
            }
            $summary.localagent_tests_passed = $true
        }
        finally {
            Pop-Location
        }
    }

    if (-not $SkipFrontendTests) {
        Push-Location (Join-Path $repoRoot "doramate-frontend")
        try {
            cargo test --locked 2>&1 | Out-Host
            if ($LASTEXITCODE -ne 0) {
                throw "doramate-frontend cargo test failed."
            }
            $summary.frontend_tests_passed = $true
        }
        finally {
            Pop-Location
        }
    }

    $smokeArgs = @(
        (Join-Path $PSScriptRoot "smoke-localagent-run-status-stop.ps1"),
        "-Rounds", $Rounds,
        "-OutputDir", $outputDir
    )

    if ($UseStandardReleaseProfile) {
        $smokeArgs += @(
            "-RunTimeoutSeconds", 60,
            "-StopTimeoutSeconds", 30,
            "-RunRequestRetries", 2,
            "-PollIntervalMilliseconds", 300
        )
    }

    & pwsh @smokeArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Local runtime smoke failed."
    }

    $summary.smoke_passed = $true
    $summary.passed = $summary.localagent_tests_passed -and $summary.frontend_tests_passed -and $summary.smoke_passed
}
catch {
    $summary.fatal_message = $_.Exception.Message
}
finally {
    $summary.finished_at = (Get-Date).ToString("o")
    $summary | ConvertTo-Json -Depth 10 | Set-Content -Path $summaryPath -Encoding UTF8
}

if (-not $summary.passed) {
    throw "Release gate failed. Summary: $summaryPath"
}
