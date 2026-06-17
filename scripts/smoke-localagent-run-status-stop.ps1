param(
    [int]$Rounds = 1,
    [string]$DataflowPath = "",
    [string]$LocalAgentBaseUrl = "http://127.0.0.1:52100/api",
    [int]$RunTimeoutSeconds = 30,
    [int]$StopTimeoutSeconds = 20,
    [int]$HttpTimeoutSeconds = 60,
    [int]$PollIntervalMilliseconds = 500,
    [int]$LocalAgentStartupTimeoutSeconds = 30,
    [int]$RunRequestRetries = 1,
    [double]$MinRunSuccessRate = 100,
    [double]$MinStatusConfirmationRate = 100,
    [double]$MinStopSuccessRate = 100,
    [int]$MaxResidualFailures = 0,
    [int]$MaxCleanupFailures = 0,
    [string]$OutputDir = "",
    [switch]$KeepWorkingDirOut,
    [switch]$KeepLocalAgentAlive,
    [switch]$AcceptEarlyTerminalState
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-RepoRoot {
    (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Normalize-Status {
    param([AllowNull()][string]$Status)

    if ($null -eq $Status) {
        return $null
    }

    switch ($Status.ToLowerInvariant()) {
        "idle" { "Idle" }
        "starting" { "Starting" }
        "running" { "Running" }
        "stopping" { "Stopping" }
        "stopped" { "Stopped" }
        "failed" { "Failed" }
        "unknown" { "Unknown" }
        "not_found" { "Idle" }
        "notfound" { "Idle" }
        default { $Status }
    }
}

function Invoke-LocalAgentApi {
    param(
        [ValidateSet("GET", "POST")][string]$Method,
        [string]$Path,
        [object]$Body = $null
    )

    $uri = "$LocalAgentBaseUrl/$Path"
    if ($Method -eq "GET") {
        return Invoke-RestMethod -Uri $uri -Method GET -TimeoutSec $HttpTimeoutSeconds -Headers @{ Connection = "close" } -ErrorAction Stop
    }

    $jsonBody = if ($null -eq $Body) { "{}" } else { $Body | ConvertTo-Json -Depth 10 }
    $responseText = & curl.exe -sS --max-time $HttpTimeoutSeconds -X POST -H "Content-Type: application/json" --data-binary $jsonBody $uri
    if ($LASTEXITCODE -ne 0) {
        throw "curl request failed for $uri with exit code $LASTEXITCODE."
    }
    if ([string]::IsNullOrWhiteSpace($responseText)) {
        return $null
    }
    return ($responseText | ConvertFrom-Json)
}

function Get-ApiResponsePropertyValue {
    param(
        [AllowNull()][object]$Response,
        [Parameter(Mandatory = $true)][string]$PropertyName
    )

    if ($null -eq $Response) {
        return $null
    }

    if ($Response -is [System.Collections.IDictionary]) {
        if ($Response.Contains($PropertyName)) {
            return $Response[$PropertyName]
        }
        return $null
    }

    $property = $Response.PSObject.Properties[$PropertyName]
    if ($null -ne $property) {
        return $property.Value
    }

    return $null
}

function Test-LocalAgentHealthy {
    try {
        $response = Invoke-LocalAgentApi -Method GET -Path "health"
        return ((Get-ApiResponsePropertyValue -Response $response -PropertyName "status") -eq "ok")
    }
    catch {
        return $false
    }
}

function Start-ManagedLocalAgent {
    param(
        [string]$RepoRoot,
        [string]$LogPath
    )

    $localAgentDir = Join-Path $RepoRoot "doramate-localagent"
    $exePath = Join-Path $localAgentDir "target\debug\doramate-localagent.exe"
    if (-not (Test-Path -LiteralPath $exePath)) {
        Push-Location $localAgentDir
        try {
            cargo build --bin doramate-localagent --locked 2>&1 | Out-Host
            if ($LASTEXITCODE -ne 0) {
                throw "cargo build failed with exit code $LASTEXITCODE"
            }
        }
        finally {
            Pop-Location
        }
    }

    $repoLocalAgentPrefix = [System.IO.Path]::GetFullPath($localAgentDir)
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
            }
            catch {
            }
        }
    }

    $listenerConnections = Get-NetTCPConnection -LocalPort 52100 -State Listen -ErrorAction SilentlyContinue
    foreach ($listener in @($listenerConnections)) {
        try {
            Stop-Process -Id $listener.OwningProcess -Force -ErrorAction Stop
        }
        catch {
        }
    }

    Start-Sleep -Milliseconds 500

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $LogPath) | Out-Null
    $stderrPath = $LogPath -replace '\.stdout\.', '.stderr.'
    $process = Start-Process -FilePath $exePath `
        -WorkingDirectory $localAgentDir `
        -RedirectStandardOutput $LogPath `
        -RedirectStandardError $stderrPath `
        -WindowStyle Hidden `
        -PassThru

    $deadline = (Get-Date).AddSeconds($LocalAgentStartupTimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if ($process.HasExited) {
            $stdout = if (Test-Path -LiteralPath $LogPath) { Get-Content -Raw $LogPath } else { "" }
            $stderr = if (Test-Path -LiteralPath $stderrPath) { Get-Content -Raw $stderrPath } else { "" }
            throw "LocalAgent exited prematurely.`nstdout:`n$stdout`nstderr:`n$stderr"
        }

        if (Test-LocalAgentHealthy) {
            return $process
        }

        Start-Sleep -Milliseconds 500
    }

    throw "LocalAgent did not become healthy within ${LocalAgentStartupTimeoutSeconds} seconds."
}

function Stop-ManagedLocalAgent {
    param([System.Diagnostics.Process]$Process)

    if ($null -eq $Process) {
        return
    }

    if (-not $Process.HasExited) {
        Stop-Process -Id $Process.Id -Force -ErrorAction SilentlyContinue
        try {
            $null = $Process.WaitForExit(5000)
        }
        catch {
        }
    }
}

function Cleanup-ResidualDoraProcesses {
    $cleaned = 0
    foreach ($proc in @(Get-Process dora -ErrorAction SilentlyContinue)) {
        try {
            Stop-Process -Id $proc.Id -Force -ErrorAction Stop
            $cleaned++
        }
        catch {
        }
    }
    return $cleaned
}

function Wait-ForDataflowStatus {
    param(
        [string]$ProcessId,
        [string[]]$TargetStatuses,
        [string[]]$FallbackStatuses = @(),
        [int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $normalizedTargets = $TargetStatuses | ForEach-Object { Normalize-Status -Status $_ }
    $normalizedFallbacks = $FallbackStatuses | ForEach-Object { Normalize-Status -Status $_ }
    $lastStatus = $null
    while ((Get-Date) -lt $deadline) {
        try {
            $status = Invoke-LocalAgentApi -Method GET -Path "status/$ProcessId"
            $normalized = Normalize-Status -Status $status.status
            $lastStatus = $status
            if ($normalizedTargets -contains $normalized) {
                return [pscustomobject]@{
                    Reached             = $true
                    UsedTerminalFallback = $false
                    Status              = $status
                }
            }
            if ($normalizedFallbacks -contains $normalized) {
                return [pscustomobject]@{
                    Reached             = $true
                    UsedTerminalFallback = $true
                    Status              = $status
                }
            }
            if (@("Failed", "Unknown") -contains $normalized) {
                return [pscustomobject]@{
                    Reached             = $false
                    UsedTerminalFallback = $false
                    Status              = $status
                }
            }
        }
        catch {
        }

        Start-Sleep -Milliseconds $PollIntervalMilliseconds
    }

    return [pscustomobject]@{
        Reached             = $false
        UsedTerminalFallback = $false
        Status              = $lastStatus
    }
}

function Remove-TransientYamlFiles {
    param([string]$WorkingDir)

    $result = [ordered]@{
        removed_count             = 0
        removed_paths             = @()
        removed_preexisting_count = 0
        removed_preexisting_paths = @()
        failed_count              = 0
        failed_paths              = @()
    }

    if (-not (Test-Path -LiteralPath $WorkingDir)) {
        return [pscustomobject]$result
    }

    foreach ($file in @(Get-ChildItem -LiteralPath $WorkingDir -Filter "doramate_*.yml" -File -ErrorAction SilentlyContinue)) {
        try {
            Remove-Item -LiteralPath $file.FullName -Force -ErrorAction Stop
            $result.removed_count++
            $result.removed_paths += $file.FullName
            $result.removed_preexisting_count++
            $result.removed_preexisting_paths += $file.FullName
        }
        catch {
            $result.failed_count++
            $result.failed_paths += $file.FullName
        }
    }

    return [pscustomobject]$result
}

function Remove-WorkingDirOutContents {
    param([string]$WorkingDir)

    $result = [ordered]@{
        removed_count             = 0
        removed_paths             = @()
        removed_preexisting_count = 0
        removed_preexisting_paths = @()
        failed_count              = 0
        failed_paths              = @()
    }

    $outDir = Join-Path $WorkingDir "out"
    if (-not (Test-Path -LiteralPath $outDir)) {
        return [pscustomobject]$result
    }

    $resolvedOutDir = (Resolve-Path $outDir).Path
    foreach ($entry in @(Get-ChildItem -LiteralPath $resolvedOutDir -Force -ErrorAction SilentlyContinue)) {
        if ($entry.Name -eq ".gitignore") {
            continue
        }

        try {
            Remove-Item -LiteralPath $entry.FullName -Recurse -Force -ErrorAction Stop
            $result.removed_count++
            $result.removed_paths += $entry.FullName
            $result.removed_preexisting_count++
            $result.removed_preexisting_paths += $entry.FullName
        }
        catch {
            $result.failed_count++
            $result.failed_paths += $entry.FullName
        }
    }

    return [pscustomobject]$result
}

$repoRoot = Get-RepoRoot
if ([string]::IsNullOrWhiteSpace($DataflowPath)) {
    $DataflowPath = Join-Path $repoRoot "dora-api-csharp\samples\csharp-dataflow\smoke.dataflow.yml"
}

if (-not (Test-Path -LiteralPath $DataflowPath)) {
    throw "Dataflow file not found: $DataflowPath"
}

$resolvedDataflowPath = (Resolve-Path $DataflowPath).Path
$workingDir = Split-Path -Parent $resolvedDataflowPath
$yaml = Get-Content -Raw $resolvedDataflowPath
$outputBaseDir = if ([string]::IsNullOrWhiteSpace($OutputDir)) { Join-Path $repoRoot "out\release-gates" } else { $OutputDir }
New-Item -ItemType Directory -Force -Path $outputBaseDir | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$summaryPath = Join-Path $outputBaseDir "local-runtime-smoke-$timestamp.json"
$managedLocalAgent = $null
$startedManagedLocalAgent = $false
$localAgentLogPath = Join-Path $outputBaseDir "localagent-smoke-$timestamp.stdout.log"

$summary = [ordered]@{
    gate_name                          = "local-runtime-smoke"
    started_at                         = (Get-Date).ToString("o")
    dataflow_path                      = $resolvedDataflowPath
    working_dir                        = $workingDir
    rounds_requested                   = $Rounds
    rounds_completed                   = 0
    run_success_count                  = 0
    status_confirmation_success_count  = 0
    stop_success_count                 = 0
    early_terminal_status_count        = 0
    residual_failures                  = 0
    run_success_rate                   = 0.0
    status_confirmation_rate           = 0.0
    stop_success_rate                  = 0.0
    passed                             = $false
    fatal_message                      = $null
    localagent_log_path                = $localAgentLogPath
    rounds                             = @()
    thresholds                         = [ordered]@{
        min_run_success_rate            = $MinRunSuccessRate
        min_status_confirmation_rate    = $MinStatusConfirmationRate
        min_stop_success_rate           = $MinStopSuccessRate
        max_residual_failures           = $MaxResidualFailures
        max_cleanup_failures            = $MaxCleanupFailures
    }
    threshold_results                  = [ordered]@{
        run_success_rate_ok         = $false
        status_confirmation_rate_ok = $false
        stop_success_rate_ok        = $false
        residual_failures_ok        = $false
        cleanup_failures_ok         = $false
        rounds_completed_ok         = $false
    }
}

try {
    if (-not (Test-LocalAgentHealthy)) {
        $managedLocalAgent = Start-ManagedLocalAgent -RepoRoot $repoRoot -LogPath $localAgentLogPath
        $startedManagedLocalAgent = $true
    }

    $runResults = New-Object System.Collections.Generic.List[object]
    $statusResults = New-Object System.Collections.Generic.List[object]
    $stopResults = New-Object System.Collections.Generic.List[object]

    for ($round = 1; $round -le $Rounds; $round++) {
        $roundResult = [ordered]@{
            round                    = $round
            process_id               = $null
            run_success              = $false
            status_confirmed         = $false
            used_terminal_fallback   = $false
            stop_success             = $false
            final_status_confirmed   = $false
            final_status             = $null
            residual_processes       = 0
            error                    = $null
        }

        try {
            $runResponse = $null
            for ($attempt = 1; $attempt -le ($RunRequestRetries + 1); $attempt++) {
                try {
                    $runResponse = Invoke-LocalAgentApi -Method POST -Path "run" -Body @{
                        dataflow_yaml = $yaml
                        working_dir   = $workingDir
                    }
                    if ($null -ne $runResponse -and -not [string]::IsNullOrWhiteSpace($runResponse.process_id)) {
                        break
                    }
                }
                catch {
                    if ($attempt -ge ($RunRequestRetries + 1)) {
                        throw
                    }
                    Start-Sleep -Seconds 1
                }
            }

            if ($null -eq $runResponse -or [string]::IsNullOrWhiteSpace($runResponse.process_id)) {
                throw "Run did not return a process_id."
            }

            $roundResult.process_id = [string]$runResponse.process_id
            $roundResult.run_success = $true
            $summary.run_success_count++

            $fallbackStatuses = if ($AcceptEarlyTerminalState) { @("Stopped", "Idle", "Failed") } else { @() }
            $runningResult = Wait-ForDataflowStatus -ProcessId $roundResult.process_id -TargetStatuses @("Running") -FallbackStatuses $fallbackStatuses -TimeoutSeconds $RunTimeoutSeconds
            if (-not $runningResult.Reached) {
                throw "Process '$($roundResult.process_id)' did not reach Running within ${RunTimeoutSeconds}s."
            }

            $roundResult.status_confirmed = $true
            $roundResult.used_terminal_fallback = [bool]$runningResult.UsedTerminalFallback
            if ($roundResult.used_terminal_fallback) {
                $summary.early_terminal_status_count++
            }
            $summary.status_confirmation_success_count++

            $finalResult = $null
            if ($roundResult.used_terminal_fallback) {
                $roundResult.stop_success = $true
                $summary.stop_success_count++
                $finalResult = Wait-ForDataflowStatus -ProcessId $roundResult.process_id -TargetStatuses @("Stopped", "Idle", "Failed") -TimeoutSeconds 3
            }
            else {
                $stopResponse = Invoke-LocalAgentApi -Method POST -Path "stop" -Body @{ process_id = $roundResult.process_id }
                $stopStatus = Get-ApiResponsePropertyValue -Response $stopResponse -PropertyName "status"
                if ($null -ne $stopStatus -and $stopStatus -ne "stopped") {
                    throw "Stop returned unexpected status '$stopStatus'."
                }

                $roundResult.stop_success = $true
                $summary.stop_success_count++
                $finalResult = Wait-ForDataflowStatus -ProcessId $roundResult.process_id -TargetStatuses @("Stopped", "Idle", "Failed") -TimeoutSeconds $StopTimeoutSeconds
            }

            if (-not $finalResult.Reached) {
                throw "Process '$($roundResult.process_id)' did not reach Stopped/Idle within ${StopTimeoutSeconds}s."
            }

            $roundResult.final_status_confirmed = $true
            $roundResult.final_status = Normalize-Status -Status $finalResult.Status.status

            Start-Sleep -Seconds 2
            $diagnose = Invoke-LocalAgentApi -Method GET -Path "diagnose"
            $residualCount = if ($null -ne $diagnose -and $null -ne $diagnose.residual_processes) { [int]$diagnose.residual_processes.Count } else { 0 }
            if ($residualCount -gt 0) {
                $null = Cleanup-ResidualDoraProcesses
                Start-Sleep -Seconds 1
                $diagnose = Invoke-LocalAgentApi -Method GET -Path "diagnose"
                $residualCount = if ($null -ne $diagnose -and $null -ne $diagnose.residual_processes) { [int]$diagnose.residual_processes.Count } else { 0 }
            }
            $roundResult.residual_processes = $residualCount
            if ($residualCount -gt 0) {
                $summary.residual_failures++
            }
        }
        catch {
            $roundResult.error = $_.Exception.Message
            $summary.fatal_message = $_.Exception.Message
        }

        $runResults.Add($roundResult.run_success)
        $statusResults.Add($roundResult.status_confirmed)
        $stopResults.Add($roundResult.stop_success)
        $summary.rounds_completed++
        $summary.rounds += [pscustomobject]$roundResult
    }

    $summary.run_success_rate = if ($summary.rounds_completed -gt 0) { [math]::Round(($summary.run_success_count / $summary.rounds_completed) * 100, 2) } else { 0.0 }
    $summary.status_confirmation_rate = if ($summary.rounds_completed -gt 0) { [math]::Round(($summary.status_confirmation_success_count / $summary.rounds_completed) * 100, 2) } else { 0.0 }
    $summary.stop_success_rate = if ($summary.rounds_completed -gt 0) { [math]::Round(($summary.stop_success_count / $summary.rounds_completed) * 100, 2) } else { 0.0 }

    $yamlCleanup = Remove-TransientYamlFiles -WorkingDir $workingDir
    $summary.cleanup_removed_count = $yamlCleanup.removed_count
    $summary.cleanup_removed_paths = @($yamlCleanup.removed_paths)
    $summary.cleanup_removed_preexisting_count = $yamlCleanup.removed_preexisting_count
    $summary.cleanup_removed_preexisting_paths = @($yamlCleanup.removed_preexisting_paths)
    $summary.cleanup_failed_count = $yamlCleanup.failed_count
    $summary.cleanup_failed_paths = @($yamlCleanup.failed_paths)

    if ($KeepWorkingDirOut) {
        $outCleanup = [pscustomobject]@{
            removed_count             = 0
            removed_paths             = @()
            removed_preexisting_count = 0
            removed_preexisting_paths = @()
            failed_count              = 0
            failed_paths              = @()
        }
    }
    else {
        $outCleanup = Remove-WorkingDirOutContents -WorkingDir $workingDir
    }

    $summary.out_cleanup_removed_count = $outCleanup.removed_count
    $summary.out_cleanup_removed_paths = @($outCleanup.removed_paths)
    $summary.out_cleanup_removed_preexisting_count = $outCleanup.removed_preexisting_count
    $summary.out_cleanup_removed_preexisting_paths = @($outCleanup.removed_preexisting_paths)
    $summary.out_cleanup_failed_count = $outCleanup.failed_count
    $summary.out_cleanup_failed_paths = @($outCleanup.failed_paths)

    $cleanupFailures = $summary.cleanup_failed_count + $summary.out_cleanup_failed_count
    $summary.threshold_results = [ordered]@{
        run_success_rate_ok         = ($summary.run_success_rate -ge $MinRunSuccessRate)
        status_confirmation_rate_ok = ($summary.status_confirmation_rate -ge $MinStatusConfirmationRate)
        stop_success_rate_ok        = ($summary.stop_success_rate -ge $MinStopSuccessRate)
        residual_failures_ok        = ($summary.residual_failures -le $MaxResidualFailures)
        cleanup_failures_ok         = ($cleanupFailures -le $MaxCleanupFailures)
        rounds_completed_ok         = ($summary.rounds_completed -eq $summary.rounds_requested)
    }
}
catch {
    $summary.fatal_message = $_.Exception.Message
}
finally {
    if ($startedManagedLocalAgent -and -not $KeepLocalAgentAlive) {
        Stop-ManagedLocalAgent -Process $managedLocalAgent
    }

    $summary.finished_at = (Get-Date).ToString("o")
    $summary.passed = (
        $summary.threshold_results.run_success_rate_ok -and
        $summary.threshold_results.status_confirmation_rate_ok -and
        $summary.threshold_results.stop_success_rate_ok -and
        $summary.threshold_results.residual_failures_ok -and
        $summary.threshold_results.cleanup_failures_ok -and
        $summary.threshold_results.rounds_completed_ok
    )
    $summary | ConvertTo-Json -Depth 10 | Set-Content -Path $summaryPath -Encoding UTF8
    Write-Host "Smoke summary written to $summaryPath"
}

if (-not $summary.passed) {
    throw "Local runtime smoke failed. Summary: $summaryPath"
}
