[CmdletBinding()]
param(
    [ValidateSet("baseline", "standard")][string]$Profile = "baseline",
    [string[]]$DataflowPaths = @(),
    [int]$Rounds = 3,
    [int]$RunTimeoutSeconds = 30,
    [int]$StopTimeoutSeconds = 20,
    [int]$HttpTimeoutSeconds = 60,
    [int]$RunRequestRetries = 1,
    [double]$MinRunSuccessRate = 100,
    [double]$MinStatusConfirmationRate = 100,
    [double]$MinStopSuccessRate = 100,
    [int]$MaxResidualFailures = 0,
    [int]$MaxCleanupFailures = 0,
    [string]$OutputDir = "",
    [switch]$KeepWorkingDirOut,
    [switch]$KeepLocalAgentAlive
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-RepoRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Test-LocalAgentHealthy {
    try {
        $response = Invoke-RestMethod -Uri "http://127.0.0.1:52100/api/health" -Method GET -TimeoutSec 5 -Headers @{ Connection = "close" } -ErrorAction Stop
        return ($response.status -eq "ok")
    } catch {
        return $false
    }
}

function Stop-RepoLocalAgentProcesses {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $repoLocalAgentPrefix = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot "doramate-localagent"))
    $candidateProcesses = Get-CimInstance Win32_Process -Filter "Name = 'doramate-localagent.exe'" -ErrorAction SilentlyContinue
    foreach ($candidate in @($candidateProcesses)) {
        $executablePath = $candidate.ExecutablePath
        if ([string]::IsNullOrWhiteSpace($executablePath)) {
            continue
        }

        $normalizedPath = [System.IO.Path]::GetFullPath($executablePath)
        if ($normalizedPath.StartsWith($repoLocalAgentPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            try {
                Stop-Process -Id $candidate.ProcessId -Force -ErrorAction Stop
            } catch {
            }
        }
    }
}

function Get-DefaultSampleCatalog {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    return @(
        [pscustomobject]@{
            Name         = "csharp-dataflow-smoke"
            RelativePath = "dora-api-csharp\samples\csharp-dataflow\smoke.dataflow.yml"
            Profiles     = @("baseline", "standard")
            BuildSteps   = @()
        }
        [pscustomobject]@{
            Name         = "csharp-multi-node"
            RelativePath = "dora-api-csharp\samples\csharp-multi-node\dataflow.yml"
            Profiles     = @("baseline", "standard")
            BuildSteps   = @(
                "dotnet build Producer.csproj -c Release -p:NuGetAudit=false",
                "dotnet build Consumer.csproj -c Release -p:NuGetAudit=false"
            )
        }
        [pscustomobject]@{
            Name         = "csharp-async-node-dataflow"
            RelativePath = "dora-api-csharp\samples\csharp-async-node-dataflow\dataflow.yml"
            Profiles     = @("baseline", "standard")
            BuildSteps   = @(
                "dotnet build Producer.csproj -c Release -p:NuGetAudit=false",
                "dotnet build Consumer.csproj -c Release -p:NuGetAudit=false"
            )
        }
        [pscustomobject]@{
            Name         = "csharp-arrow-node-dataflow"
            RelativePath = "dora-api-csharp\samples\csharp-arrow-node-dataflow\dataflow.yml"
            Profiles     = @("standard")
            BuildSteps   = @(
                "dotnet build Producer.csproj -c Release -p:NuGetAudit=false",
                "dotnet build Consumer.csproj -c Release -p:NuGetAudit=false"
            )
        }
        [pscustomobject]@{
            Name         = "csharp-operator-dataflow"
            RelativePath = "dora-api-csharp\samples\csharp-operator-dataflow\dataflow.yml"
            Profiles     = @("standard")
            BuildSteps   = @(
                "dotnet build Producer.csproj -c Release -p:NuGetAudit=false",
                "dotnet publish CSharpCounterOperator.csproj -c Release -p:NuGetAudit=false",
                "dotnet build Sink.csproj -c Release -p:NuGetAudit=false"
            )
        }
    ) | ForEach-Object {
        [pscustomobject]@{
            Name         = $_.Name
            RelativePath = $_.RelativePath
            Profiles     = @($_.Profiles)
            DataflowPath = (Join-Path $RepoRoot $_.RelativePath)
            BuildSteps   = @($_.BuildSteps)
        }
    }
}

function Get-SafePathSegment {
    param([Parameter(Mandatory = $true)][string]$Value)

    return (($Value -replace '[^A-Za-z0-9._-]', '-') -replace '-+', '-').Trim('-')
}

function Get-SelectedSamples {
    param(
        [Parameter(Mandatory = $true)][string]$RepoRoot,
        [Parameter(Mandatory = $true)][string]$SelectedProfile,
        [string[]]$ExplicitDataflowPaths = @()
    )

    if ($ExplicitDataflowPaths.Count -gt 0) {
        $samples = @()
        foreach ($path in $ExplicitDataflowPaths) {
            if (-not (Test-Path -LiteralPath $path)) {
                throw "Dataflow file not found: $path"
            }

            $resolvedPath = (Resolve-Path $path).Path
            $sampleName = Split-Path -LeafBase $resolvedPath
            if ([string]::IsNullOrWhiteSpace($sampleName)) {
                $sampleName = Split-Path -Leaf (Split-Path -Parent $resolvedPath)
            }

            $samples += [pscustomobject]@{
                Name         = $sampleName
                RelativePath = $null
                Profiles     = @("custom")
                DataflowPath = $resolvedPath
                BuildSteps   = @()
            }
        }

        return @($samples)
    }

    $catalog = Get-DefaultSampleCatalog -RepoRoot $RepoRoot
    return @($catalog | Where-Object { $_.Profiles -contains $SelectedProfile })
}

function Invoke-PrebuildSteps {
    param(
        [Parameter(Mandatory = $true)][pscustomobject]$Sample,
        [Parameter(Mandatory = $true)][string]$ResolvedDataflowPath,
        [Parameter(Mandatory = $true)][string]$RepoRoot
    )

    if ($null -eq $Sample.BuildSteps -or $Sample.BuildSteps.Count -eq 0) {
        return
    }

    $sampleWorkingDir = Split-Path -Parent $ResolvedDataflowPath
    $dotnetCliHome = Join-Path $RepoRoot "dora-api-csharp\.dotnet-cli"
    New-Item -ItemType Directory -Force -Path $dotnetCliHome | Out-Null

    foreach ($buildStep in $Sample.BuildSteps) {
        Write-Host "  prebuild: $buildStep"
        Push-Location $sampleWorkingDir
        try {
            $previousDotnetCliHome = $env:DOTNET_CLI_HOME
            $previousDotnetSkipFirstTime = $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE
            $previousDotnetTelemetry = $env:DOTNET_CLI_TELEMETRY_OPTOUT
            $env:DOTNET_CLI_HOME = $dotnetCliHome
            $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
            $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

            Invoke-Expression $buildStep | Out-Host
            if ($LASTEXITCODE -ne 0) {
                throw "Prebuild step failed with exit code ${LASTEXITCODE}: $buildStep"
            }
        } finally {
            $env:DOTNET_CLI_HOME = $previousDotnetCliHome
            $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = $previousDotnetSkipFirstTime
            $env:DOTNET_CLI_TELEMETRY_OPTOUT = $previousDotnetTelemetry
            Pop-Location
        }
    }
}

$repoRoot = Get-RepoRoot
$smokeScript = Join-Path $repoRoot "scripts\smoke-localagent-run-status-stop.ps1"
if (-not (Test-Path -LiteralPath $smokeScript)) {
    throw "Smoke script not found: $smokeScript"
}
$initialLocalAgentHealthy = Test-LocalAgentHealthy
$reuseManagedLocalAgent = (-not $initialLocalAgentHealthy)

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "out\release-gates\multi-dataflow"
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$selectedSamples = Get-SelectedSamples -RepoRoot $repoRoot -SelectedProfile $Profile -ExplicitDataflowPaths $DataflowPaths
if ($selectedSamples.Count -eq 0) {
    throw "No dataflow samples selected for profile '$Profile'."
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$summaryPath = Join-Path $OutputDir "local-runtime-multi-dataflow-smoke-$timestamp.json"
$summary = [ordered]@{
    gate_name              = "localagent-multi-dataflow-smoke"
    started_at             = (Get-Date).ToString("o")
    profile                = if ($DataflowPaths.Count -gt 0) { "custom" } else { $Profile }
    rounds_per_sample      = $Rounds
    sample_count_requested = $selectedSamples.Count
    sample_count_completed = 0
    sample_pass_count      = 0
    sample_fail_count      = 0
    samples                = @()
    passed                 = $false
    fatal_message          = $null
}

try {
    foreach ($sample in $selectedSamples) {
        $resolvedPath = if ([System.IO.Path]::IsPathRooted($sample.DataflowPath)) {
            $sample.DataflowPath
        } else {
            (Resolve-Path (Join-Path $repoRoot $sample.DataflowPath)).Path
        }

        $sampleKey = Get-SafePathSegment -Value $sample.Name
        if ([string]::IsNullOrWhiteSpace($sampleKey)) {
            $sampleKey = "sample-$($summary.sample_count_completed + 1)"
        }
        $sampleOutputDir = Join-Path $OutputDir $sampleKey
        New-Item -ItemType Directory -Force -Path $sampleOutputDir | Out-Null

        Write-Host "Running multi-dataflow smoke sample '$($sample.Name)'"
        $sampleResult = [ordered]@{
            name                     = $sample.Name
            dataflow_path            = $resolvedPath
            output_dir               = $sampleOutputDir
            passed                   = $false
            summary_path             = $null
            fatal_message            = $null
            rounds_completed         = 0
            run_success_rate         = 0
            status_confirmation_rate = 0
            stop_success_rate        = 0
            residual_failures        = 0
            cleanup_failures         = 0
        }

        try {
            Invoke-PrebuildSteps -Sample $sample -ResolvedDataflowPath $resolvedPath -RepoRoot $repoRoot
        } catch {
            $sampleResult.fatal_message = $_.Exception.Message
        }

        $smokeArgs = @(
            "-NoProfile",
            "-ExecutionPolicy", "Bypass",
            "-File", $smokeScript,
            "-Rounds", $Rounds,
            "-RunTimeoutSeconds", $RunTimeoutSeconds,
            "-StopTimeoutSeconds", $StopTimeoutSeconds,
            "-HttpTimeoutSeconds", $HttpTimeoutSeconds,
            "-RunRequestRetries", $RunRequestRetries,
            "-MinRunSuccessRate", $MinRunSuccessRate,
            "-MinStatusConfirmationRate", $MinStatusConfirmationRate,
            "-MinStopSuccessRate", $MinStopSuccessRate,
            "-MaxResidualFailures", $MaxResidualFailures,
            "-MaxCleanupFailures", $MaxCleanupFailures,
            "-DataflowPath", $resolvedPath,
            "-OutputDir", $sampleOutputDir
        )
        if ($KeepWorkingDirOut) {
            $smokeArgs += "-KeepWorkingDirOut"
        }
        if ($KeepLocalAgentAlive -or $reuseManagedLocalAgent) {
            $smokeArgs += "-KeepLocalAgentAlive"
        }

        if ([string]::IsNullOrWhiteSpace($sampleResult.fatal_message)) {
            try {
                & pwsh @smokeArgs
                if ($LASTEXITCODE -ne 0) {
                    throw "Smoke sample '$($sample.Name)' failed with exit code $LASTEXITCODE."
                }
            } catch {
                $sampleResult.fatal_message = $_.Exception.Message
            }
        }

        $latestSummary = Get-ChildItem -LiteralPath $sampleOutputDir -Filter "local-runtime-smoke-*.json" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 1
        if ($latestSummary) {
            $sampleResult.summary_path = $latestSummary.FullName
            try {
                $sampleSummary = Get-Content -Raw $latestSummary.FullName | ConvertFrom-Json
                $sampleResult.passed = [bool]$sampleSummary.passed
                $sampleResult.rounds_completed = [int]$sampleSummary.rounds_completed
                $sampleResult.run_success_rate = [double]$sampleSummary.run_success_rate
                $sampleResult.status_confirmation_rate = [double]$sampleSummary.status_confirmation_rate
                $sampleResult.stop_success_rate = [double]$sampleSummary.stop_success_rate
                $sampleResult.residual_failures = [int]$sampleSummary.residual_failures
                $sampleResult.cleanup_failures = [int]($sampleSummary.cleanup_failed_count + $sampleSummary.out_cleanup_failed_count)
                if ([string]::IsNullOrWhiteSpace($sampleResult.fatal_message) -and -not [string]::IsNullOrWhiteSpace($sampleSummary.fatal_message)) {
                    $sampleResult.fatal_message = $sampleSummary.fatal_message
                }
            } catch {
                if ([string]::IsNullOrWhiteSpace($sampleResult.fatal_message)) {
                    $sampleResult.fatal_message = "Failed to parse sample summary: $($_.Exception.Message)"
                }
            }
        } elseif ([string]::IsNullOrWhiteSpace($sampleResult.fatal_message)) {
            $sampleResult.fatal_message = "Smoke sample '$($sample.Name)' did not produce a summary JSON."
        }

        if ($sampleResult.passed) {
            $summary.sample_pass_count++
        } else {
            $summary.sample_fail_count++
        }

        $summary.samples += [pscustomobject]$sampleResult
        $summary.sample_count_completed++
    }
} catch {
    $summary.fatal_message = $_.Exception.Message
} finally {
    if ($reuseManagedLocalAgent -and -not $KeepLocalAgentAlive) {
        Stop-RepoLocalAgentProcesses -RepoRoot $repoRoot
    }

    $summary.finished_at = (Get-Date).ToString("o")
    $summary.passed = (
        $summary.sample_count_completed -eq $summary.sample_count_requested -and
        $summary.sample_fail_count -eq 0 -and
        [string]::IsNullOrWhiteSpace($summary.fatal_message)
    )

    $summary | ConvertTo-Json -Depth 10 | Set-Content -Path $summaryPath -Encoding UTF8

    Write-Host "Multi-dataflow smoke summary written to $summaryPath"
    Write-Host "Samples completed: $($summary.sample_count_completed)/$($summary.sample_count_requested)"
    Write-Host "Samples passed: $($summary.sample_pass_count)"
    Write-Host "Samples failed: $($summary.sample_fail_count)"
}

if (-not $summary.passed) {
    throw "Multi-dataflow smoke failed. Summary: $summaryPath"
}
