[CmdletBinding()]
param(
    [string]$InputDir = "",
    [string]$OutputDir = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-RepoRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Get-DateValue {
    param([AllowNull()]$Value)

    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) {
        return $null
    }

    try {
        return [DateTimeOffset]::Parse([string]$Value)
    } catch {
        return $null
    }
}

function Get-OptionalPropertyValue {
    param(
        $Object,
        [Parameter(Mandatory = $true)][string]$PropertyName,
        $DefaultValue = $null
    )

    if ($null -eq $Object) {
        return $DefaultValue
    }

    $property = $Object.PSObject.Properties[$PropertyName]
    if ($null -eq $property) {
        return $DefaultValue
    }

    return $property.Value
}

function Normalize-ResultEntry {
    param($Value)

    if ($null -eq $Value) {
        return $null
    }

    if ($Value -is [System.Array]) {
        $objectEntry = @($Value | Where-Object { $_ -isnot [string] } | Select-Object -Last 1)
        if ($objectEntry.Count -gt 0) {
            return $objectEntry[0]
        }
        return $null
    }

    return $Value
}

function Get-ShortDataflowLabel {
    param([AllowNull()][string]$PathValue)

    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        return "unknown"
    }

    $segments = $PathValue -split "[\\/]"
    if ($segments.Length -ge 2) {
        return ($segments[($segments.Length - 2)..($segments.Length - 1)] -join "/")
    }

    return $PathValue
}

function Convert-SmokeRecord {
    param(
        [Parameter(Mandatory = $true)]$Json,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $cleanupFailures =
        [int](Get-OptionalPropertyValue -Object $Json -PropertyName "cleanup_failed_count" -DefaultValue 0) +
        [int](Get-OptionalPropertyValue -Object $Json -PropertyName "out_cleanup_failed_count" -DefaultValue 0)
    return [pscustomobject]@{
        kind                       = "smoke"
        path                       = $Path
        started_at                 = Get-DateValue $Json.started_at
        finished_at                = Get-DateValue $Json.finished_at
        dataflow_path              = [string]$Json.dataflow_path
        dataflow_label             = Get-ShortDataflowLabel -PathValue ([string]$Json.dataflow_path)
        rounds_requested           = [int]$Json.rounds_requested
        rounds_completed           = [int]$Json.rounds_completed
        run_success_rate           = [double]$Json.run_success_rate
        status_confirmation_rate   = [double]$Json.status_confirmation_rate
        stop_success_rate          = [double]$Json.stop_success_rate
        residual_failures          = [int]$Json.residual_failures
        cleanup_failures           = $cleanupFailures
        passed                     = [bool]$Json.passed
        fatal_message              = [string](Get-OptionalPropertyValue -Object $Json -PropertyName "fatal_message" -DefaultValue "")
    }
}

function Convert-ReleaseGateRecord {
    param(
        [Parameter(Mandatory = $true)]$Json,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $localAgentResult = Normalize-ResultEntry (Get-OptionalPropertyValue -Object $Json.unit_tests -PropertyName "localagent" -DefaultValue $null)
    $frontendResult = Normalize-ResultEntry (Get-OptionalPropertyValue -Object $Json.unit_tests -PropertyName "frontend" -DefaultValue $null)
    $liveSmokeResult = Normalize-ResultEntry (Get-OptionalPropertyValue -Object $Json -PropertyName "live_smoke" -DefaultValue $null)

    return [pscustomobject]@{
        kind                    = "release_gate"
        path                    = $Path
        started_at              = Get-DateValue $Json.started_at
        finished_at             = Get-DateValue $Json.finished_at
        profile                 = [string](Get-OptionalPropertyValue -Object $Json -PropertyName "profile" -DefaultValue "custom")
        rounds_required         = [int](Get-OptionalPropertyValue -Object (Get-OptionalPropertyValue -Object $Json -PropertyName "thresholds" -DefaultValue $null) -PropertyName "rounds_required" -DefaultValue 0)
        localagent_tests_passed = [bool](Get-OptionalPropertyValue -Object $localAgentResult -PropertyName "passed" -DefaultValue $false)
        frontend_tests_passed   = [bool](Get-OptionalPropertyValue -Object $frontendResult -PropertyName "passed" -DefaultValue $false)
        live_smoke_passed       = [bool](Get-OptionalPropertyValue -Object $liveSmokeResult -PropertyName "passed" -DefaultValue $false)
        live_smoke_summary_path = [string](Get-OptionalPropertyValue -Object $liveSmokeResult -PropertyName "summary_path" -DefaultValue "")
        passed                  = [bool]$Json.passed
    }
}

function Convert-MultiDataflowRecord {
    param(
        [Parameter(Mandatory = $true)]$Json,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $sampleNames = @()
    foreach ($sample in @($Json.samples)) {
        $sampleNames += [string]$sample.name
    }

    return [pscustomobject]@{
        kind                   = "multi_dataflow"
        path                   = $Path
        started_at             = Get-DateValue $Json.started_at
        finished_at            = Get-DateValue $Json.finished_at
        profile                = [string]$Json.profile
        rounds_per_sample      = [int]$Json.rounds_per_sample
        sample_count_requested = [int]$Json.sample_count_requested
        sample_count_completed = [int]$Json.sample_count_completed
        sample_pass_count      = [int]$Json.sample_pass_count
        sample_fail_count      = [int]$Json.sample_fail_count
        sample_names           = @($sampleNames)
        samples                = @($Json.samples)
        passed                 = [bool]$Json.passed
        fatal_message          = [string](Get-OptionalPropertyValue -Object $Json -PropertyName "fatal_message" -DefaultValue "")
    }
}

function Get-LatestRecord {
    param([AllowEmptyCollection()][object[]]$Records)

    return @($Records | Where-Object { $null -ne $_.started_at } | Sort-Object started_at -Descending | Select-Object -First 1)
}

function Get-Recommendations {
    param(
        [AllowEmptyCollection()][object[]]$SmokeRecords,
        [AllowEmptyCollection()][object[]]$ReleaseRecords,
        [AllowEmptyCollection()][object[]]$MultiRecords
    )

    $latestSmoke = Get-LatestRecord -Records $SmokeRecords
    $latestRelease = Get-LatestRecord -Records $ReleaseRecords
    $latestMulti = Get-LatestRecord -Records $MultiRecords
    $failedSmokeRuns = @($SmokeRecords | Where-Object { -not $_.passed }).Count
    $failedMultiRuns = @($MultiRecords | Where-Object { -not $_.passed }).Count

    $coreRecommendation = if (
        $latestSmoke.Count -gt 0 -and $latestSmoke[0].passed -and
        $latestRelease.Count -gt 0 -and $latestRelease[0].passed -and
        $latestMulti.Count -gt 0 -and $latestMulti[0].passed
    ) {
        "keep_strict_100"
    } else {
        "collect_more_data_before_adjusting"
    }

    $coreReason = if ($coreRecommendation -eq "keep_strict_100") {
        "Latest smoke, release gate, and multi-dataflow summaries all pass; current failures are better treated as regressions to fix, not threshold candidates to relax."
    } else {
        "Latest summaries are not all green yet; do not relax thresholds while trend data is still unstable."
    }

    $cleanupRecommendation = if (@($SmokeRecords | Where-Object { $_.cleanup_failures -gt 0 }).Count -eq 0) {
        "keep_zero_cleanup_failures"
    } else {
        "investigate_cleanup_noise_before_relaxing"
    }

    $multiRecommendation = if ($latestMulti.Count -gt 0 -and $latestMulti[0].passed) {
        if ($failedMultiRuns -gt 0) {
            "baseline_profile_is_working_after_fix_collect_more_repetitions"
        } else {
            "baseline_profile_ready_for_more_runs"
        }
    } else {
        "stabilize_multi_dataflow_before_expanding_scope"
    }

    return [ordered]@{
        core_thresholds = [ordered]@{
            recommendation = $coreRecommendation
            reason         = $coreReason
        }
        cleanup_thresholds = [ordered]@{
            recommendation = $cleanupRecommendation
            reason         = if ($cleanupRecommendation -eq "keep_zero_cleanup_failures") {
                "Current smoke summaries do not show cleanup noise, so cleanup thresholds should stay strict."
            } else {
                "Cleanup failures have appeared in trend data; investigate root cause before changing the threshold."
            }
        }
        multi_dataflow = [ordered]@{
            recommendation = $multiRecommendation
            reason         = if ($latestMulti.Count -gt 0 -and $latestMulti[0].passed) {
                "Latest baseline multi-dataflow smoke passes, but there are still only a small number of aggregate runs."
            } else {
                "Latest multi-dataflow summary is not yet green; hold threshold changes and continue stabilizing."
            }
        }
        sample_size_next_step = [ordered]@{
            recommendation = "collect_more_samples"
            reason         = "Before tuning any threshold downward, collect at least 5 green baseline multi-dataflow runs and at least 3 green standard-profile runs."
        }
        current_failures_interpretation = [ordered]@{
            recommendation = if ($failedSmokeRuns -gt 0 -or $failedMultiRuns -gt 0) {
                "treat_recent_failures_as_fixable_regressions"
            } else {
                "no_recent_failures_in_current_dataset"
            }
            reason         = if ($failedSmokeRuns -gt 0 -or $failedMultiRuns -gt 0) {
                "Existing failures in the dataset are associated with earlier implementation gaps; they should not be used alone to justify threshold relaxation."
            } else {
                "Current dataset is fully green."
            }
        }
    }
}

$repoRoot = Get-RepoRoot
if ([string]::IsNullOrWhiteSpace($InputDir)) {
    $InputDir = Join-Path $repoRoot "out\release-gates"
}
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $InputDir "trends"
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$allJsonFiles = @(Get-ChildItem -LiteralPath $InputDir -Recurse -File -Filter "*.json" |
    Where-Object { -not $_.FullName.StartsWith($OutputDir, [System.StringComparison]::OrdinalIgnoreCase) })

$smokeRecords = @()
$releaseRecords = @()
$multiRecords = @()
$parseErrors = @()

foreach ($file in $allJsonFiles) {
    try {
        $json = Get-Content -Raw -LiteralPath $file.FullName | ConvertFrom-Json
        switch ([string]$json.gate_name) {
            "localagent-run-status-stop-smoke" {
                $smokeRecords += Convert-SmokeRecord -Json $json -Path $file.FullName
            }
            "release-gate-local-runtime" {
                $releaseRecords += Convert-ReleaseGateRecord -Json $json -Path $file.FullName
            }
            "localagent-multi-dataflow-smoke" {
                $multiRecords += Convert-MultiDataflowRecord -Json $json -Path $file.FullName
            }
            default {
            }
        }
    } catch {
        $parseErrors += [pscustomobject]@{
            path    = $file.FullName
            message = $_.Exception.Message
        }
    }
}

$latestSmoke = Get-LatestRecord -Records $smokeRecords
$latestRelease = Get-LatestRecord -Records $releaseRecords
$latestMulti = Get-LatestRecord -Records $multiRecords
$smokeByDataflow = @()
foreach ($group in ($smokeRecords | Group-Object dataflow_label | Sort-Object Name)) {
    $records = @($group.Group | Sort-Object started_at)
    $latest = @($records | Sort-Object started_at -Descending | Select-Object -First 1)
    $smokeByDataflow += [pscustomobject]@{
        dataflow_label            = $group.Name
        total_runs                = $records.Count
        passed_runs               = @($records | Where-Object { $_.passed }).Count
        failed_runs               = @($records | Where-Object { -not $_.passed }).Count
        latest_started_at         = if ($latest.Count -gt 0) { $latest[0].started_at.ToString("o") } else { $null }
        latest_passed             = if ($latest.Count -gt 0) { [bool]$latest[0].passed } else { $false }
        latest_rounds_requested   = if ($latest.Count -gt 0) { [int]$latest[0].rounds_requested } else { 0 }
        latest_run_success_rate   = if ($latest.Count -gt 0) { [double]$latest[0].run_success_rate } else { 0.0 }
        latest_status_success_rate = if ($latest.Count -gt 0) { [double]$latest[0].status_confirmation_rate } else { 0.0 }
        latest_stop_success_rate  = if ($latest.Count -gt 0) { [double]$latest[0].stop_success_rate } else { 0.0 }
    }
}

$recommendations = Get-Recommendations -SmokeRecords $smokeRecords -ReleaseRecords $releaseRecords -MultiRecords $multiRecords
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$summaryPath = Join-Path $OutputDir "local-runtime-trend-summary-$timestamp.json"
$markdownPath = Join-Path $OutputDir "local-runtime-trend-summary-$timestamp.md"

$summary = [ordered]@{
    generated_at = (Get-Date).ToString("o")
    input_dir    = $InputDir
    counts       = [ordered]@{
        json_files_found         = $allJsonFiles.Count
        smoke_runs               = $smokeRecords.Count
        release_gate_runs        = $releaseRecords.Count
        multi_dataflow_runs      = $multiRecords.Count
        parse_error_count        = $parseErrors.Count
    }
    smoke = [ordered]@{
        total_runs       = $smokeRecords.Count
        passed_runs      = @($smokeRecords | Where-Object { $_.passed }).Count
        failed_runs      = @($smokeRecords | Where-Object { -not $_.passed }).Count
        latest_run       = if ($latestSmoke.Count -gt 0) { $latestSmoke[0] } else { $null }
        by_dataflow      = @($smokeByDataflow)
    }
    release_gate = [ordered]@{
        total_runs       = $releaseRecords.Count
        passed_runs      = @($releaseRecords | Where-Object { $_.passed }).Count
        failed_runs      = @($releaseRecords | Where-Object { -not $_.passed }).Count
        latest_run       = if ($latestRelease.Count -gt 0) { $latestRelease[0] } else { $null }
    }
    multi_dataflow = [ordered]@{
        total_runs       = $multiRecords.Count
        passed_runs      = @($multiRecords | Where-Object { $_.passed }).Count
        failed_runs      = @($multiRecords | Where-Object { -not $_.passed }).Count
        latest_run       = if ($latestMulti.Count -gt 0) { $latestMulti[0] } else { $null }
    }
    recommendations = $recommendations
    parse_errors    = @($parseErrors)
}

$summary | ConvertTo-Json -Depth 10 | Set-Content -Path $summaryPath -Encoding UTF8

$markdown = @()
$markdown += "# Local Runtime Trend Summary"
$markdown += ""
$markdown += "- Generated at: $($summary.generated_at)"
$markdown += "- Input directory: $($summary.input_dir)"
$markdown += "- JSON files parsed: $($summary.counts.json_files_found)"
$markdown += ""
$markdown += "## Snapshot"
$markdown += ""
$markdown += "- Smoke runs: $($summary.smoke.passed_runs)/$($summary.smoke.total_runs) passed"
$markdown += "- Release gate runs: $($summary.release_gate.passed_runs)/$($summary.release_gate.total_runs) passed"
$markdown += "- Multi-dataflow runs: $($summary.multi_dataflow.passed_runs)/$($summary.multi_dataflow.total_runs) passed"
$markdown += ""
if ($null -ne $summary.smoke.latest_run) {
    $markdown += "## Latest Smoke"
    $markdown += ""
    $markdown += "- Dataflow: $($summary.smoke.latest_run.dataflow_label)"
    $markdown += "- Started at: $($summary.smoke.latest_run.started_at.ToString("o"))"
    $markdown += "- Passed: $($summary.smoke.latest_run.passed)"
    $markdown += "- Rates: run=$($summary.smoke.latest_run.run_success_rate)%, status=$($summary.smoke.latest_run.status_confirmation_rate)%, stop=$($summary.smoke.latest_run.stop_success_rate)%"
    $markdown += ""
}
if ($null -ne $summary.release_gate.latest_run) {
    $markdown += "## Latest Release Gate"
    $markdown += ""
    $markdown += "- Profile: $($summary.release_gate.latest_run.profile)"
    $markdown += "- Started at: $($summary.release_gate.latest_run.started_at.ToString("o"))"
    $markdown += "- Passed: $($summary.release_gate.latest_run.passed)"
    $markdown += "- Live smoke passed: $($summary.release_gate.latest_run.live_smoke_passed)"
    $markdown += ""
}
if ($null -ne $summary.multi_dataflow.latest_run) {
    $markdown += "## Latest Multi-Dataflow"
    $markdown += ""
    $markdown += "- Profile: $($summary.multi_dataflow.latest_run.profile)"
    $markdown += "- Started at: $($summary.multi_dataflow.latest_run.started_at.ToString("o"))"
    $markdown += "- Passed: $($summary.multi_dataflow.latest_run.passed)"
    $markdown += "- Sample pass count: $($summary.multi_dataflow.latest_run.sample_pass_count)/$($summary.multi_dataflow.latest_run.sample_count_requested)"
    $markdown += ""
}
$markdown += "## Recommendations"
$markdown += ""
$markdown += "- Core thresholds: $($summary.recommendations.core_thresholds.recommendation)"
$markdown += "- Core rationale: $($summary.recommendations.core_thresholds.reason)"
$markdown += "- Cleanup thresholds: $($summary.recommendations.cleanup_thresholds.recommendation)"
$markdown += "- Cleanup rationale: $($summary.recommendations.cleanup_thresholds.reason)"
$markdown += "- Multi-dataflow: $($summary.recommendations.multi_dataflow.recommendation)"
$markdown += "- Multi-dataflow rationale: $($summary.recommendations.multi_dataflow.reason)"
$markdown += "- Next sample-size step: $($summary.recommendations.sample_size_next_step.reason)"
$markdown += "- Failure interpretation: $($summary.recommendations.current_failures_interpretation.reason)"
$markdown += ""
$markdown += "## Smoke By Dataflow"
$markdown += ""
foreach ($entry in $summary.smoke.by_dataflow) {
    $markdown += "- $($entry.dataflow_label): passed $($entry.passed_runs)/$($entry.total_runs), latest passed=$($entry.latest_passed), latest rates run/status/stop=$($entry.latest_run_success_rate)%/$($entry.latest_status_success_rate)%/$($entry.latest_stop_success_rate)%"
}
if ($summary.parse_errors.Count -gt 0) {
    $markdown += ""
    $markdown += "## Parse Errors"
    $markdown += ""
    foreach ($errorItem in $summary.parse_errors) {
        $markdown += "- $($errorItem.path): $($errorItem.message)"
    }
}

$markdown -join [Environment]::NewLine | Set-Content -Path $markdownPath -Encoding UTF8

Write-Host "Trend summary JSON written to $summaryPath"
Write-Host "Trend summary Markdown written to $markdownPath"
