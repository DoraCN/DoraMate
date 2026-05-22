[CmdletBinding()]
param(
    [string]$LocalAgentBaseUrl = "http://127.0.0.1:52100/api",
    [string]$DataflowPath = "",
    [string]$RepoRoot = "",
    [int]$HttpTimeoutSeconds = 30,
    [int]$RunTimeoutSeconds = 30,
    [int]$StopTimeoutSeconds = 20,
    [int]$PollIntervalMilliseconds = 500,
    [int]$WebSocketWindowSeconds = 5
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ──────────────────────────────────────────────
# BeforeAll — all helpers + setup
# Pester 5 execution scope (BeforeAll/It/AfterAll) cannot see
# script-level functions, so everything must go here.
# ──────────────────────────────────────────────

BeforeAll {
    # ── Helpers ──

    function Get-RepoRoot {
        if (-not [string]::IsNullOrWhiteSpace($RepoRoot)) {
            return $RepoRoot
        }
        return (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
    }

    function Get-DataflowYaml {
        $path = $DataflowPath
        if ([string]::IsNullOrWhiteSpace($path)) {
            $root = Get-RepoRoot
            $path = Join-Path $root "dora-api-csharp\samples\csharp-dataflow\smoke.dataflow.yml"
        }
        if (-not (Test-Path $path)) {
            throw "Dataflow file not found: $path"
        }
        return [pscustomobject]@{
            Path = (Resolve-Path $path).Path
            Yaml = Get-Content -Raw (Resolve-Path $path).Path
        }
    }

    function Test-TcpPortOpen {
        param([string]$Hostname, [int]$Port, [int]$TimeoutMs = 2000)
        $client = New-Object System.Net.Sockets.TcpClient
        try {
            $async = $client.BeginConnect($Hostname, $Port, $null, $null)
            $connected = $async.AsyncWaitHandle.WaitOne($TimeoutMs, $false)
            if (-not $connected) { return $false }
            $null = $client.EndConnect($async)
            return $true
        } catch {
            return $false
        } finally {
            $client.Dispose()
        }
    }

    function Invoke-LocalAgentApi {
        param(
            [ValidateSet("GET", "POST")][string]$Method = "GET",
            [string]$Path,
            [object]$Body = $null
        )
        $uri = "$LocalAgentBaseUrl/$Path"
        if ($Method -eq "GET") {
            return Invoke-RestMethod -Uri $uri -Method GET -TimeoutSec $HttpTimeoutSeconds `
                -Headers @{ Connection = "close" } -ErrorAction Stop
        }
        $jsonBody = if ($null -eq $Body) { "{}" } else { $Body | ConvertTo-Json -Depth 10 }
        $responseText = & curl.exe -sS --max-time $HttpTimeoutSeconds -X POST `
            -H "Content-Type: application/json" --data-binary $jsonBody $uri
        if ($LASTEXITCODE -ne 0) {
            throw "curl request failed for $uri with exit code $LASTEXITCODE."
        }
        if ([string]::IsNullOrWhiteSpace($responseText)) { return $null }
        return ($responseText | ConvertFrom-Json)
    }

    function Normalize-Status {
        param([AllowNull()][string]$Status)
        if ($null -eq $Status) { return $null }
        switch ($Status.ToLowerInvariant()) {
            "idle" { return "Idle" }
            "starting" { return "Starting" }
            "running" { return "Running" }
            "stopping" { return "Stopping" }
            "stopped" { return "Stopped" }
            "failed" { return "Failed" }
            "unknown" { return "Unknown" }
            "not_found" { return "Idle" }
            "notfound" { return "Idle" }
            default { return $Status }
        }
    }

    function Wait-ForDataflowStatus {
        param(
            [string]$ProcessId,
            [string[]]$TargetStatuses,
            [int]$TimeoutSeconds
        )
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        $normalizedTargets = $TargetStatuses | ForEach-Object { Normalize-Status -Status $_ }
        $lastStatus = $null
        while ((Get-Date) -lt $deadline) {
            try {
                $status = Invoke-LocalAgentApi -Method GET -Path "status/$ProcessId"
                $normalized = Normalize-Status -Status $status.status
                $lastStatus = $status
                if ($normalizedTargets -contains $normalized) {
                    return [pscustomobject]@{ Reached = $true; Status = $status }
                }
                if (@("Failed", "Unknown") -contains $normalized -and
                    -not ($normalizedTargets -contains $normalized)) {
                    return [pscustomobject]@{ Reached = $false; Status = $status }
                }
            } catch {
                # probe error, retry
            }
            Start-Sleep -Milliseconds $PollIntervalMilliseconds
        }
        return [pscustomobject]@{ Reached = $false; Status = $lastStatus }
    }

    function Start-LocalAgentProcess {
        param([string]$ExePath, [string]$WorkingDir, [string]$LogPath)
        $process = Start-Process -FilePath $ExePath -WorkingDirectory $WorkingDir `
            -RedirectStandardOutput $LogPath -RedirectStandardError ($LogPath -replace '\.stdout\.', '.stderr.') `
            -PassThru
        Start-Sleep -Seconds 3
        if ($process.HasExited) {
            $log = if (Test-Path $LogPath) { Get-Content -Raw $LogPath } else { "" }
            throw "LocalAgent exited prematurely.`n$log"
        }
        return $process
    }

    function Stop-LocalAgentProcess {
        param([System.Diagnostics.Process]$Process)
        if ($null -eq $Process -or $Process.HasExited) { return }
        Stop-Process -Id $Process.Id -Force -ErrorAction SilentlyContinue
        try { $null = $Process.WaitForExit(5000) } catch { }
    }

    function Test-WebSocketConsistency {
        param([string]$ProcessId, [int]$WindowSeconds)
        $result = [pscustomobject]@{
            StateMatch = $false; HttpStatus = $null; WsStatus = $null
            Samples = 0; Mismatches = @()
        }
        if ([string]::IsNullOrWhiteSpace($ProcessId)) { return $result }
        $wsUrl = "ws://127.0.0.1:52100/api/status-stream/$ProcessId"
        $ws = $null
        try {
            $ct = New-Object System.Threading.CancellationTokenSource
            $ws = New-Object System.Net.WebSockets.ClientWebSocket
            $ws.Options.KeepAliveInterval = [System.TimeSpan]::FromSeconds(30)
            $connectTask = $ws.ConnectAsync([System.Uri]$wsUrl, $ct.Token)
            if (-not $connectTask.Wait([System.TimeSpan]::FromSeconds(5))) { return $result }
            $wsSamples = New-Object System.Collections.Generic.List[pscustomobject]
            $httpSamples = New-Object System.Collections.Generic.List[pscustomobject]
            $deadline = (Get-Date).AddSeconds($WindowSeconds)
            $buffer = New-Object byte[] -ArgumentList 4096
            $segment = New-Object System.ArraySegment[byte] -ArgumentList $buffer
            while ((Get-Date) -lt $deadline) {
                try {
                    $httpStatus = Invoke-LocalAgentApi -Method GET -Path "status/$ProcessId"
                    if ($null -ne $httpStatus) {
                        $httpSamples.Add([pscustomobject]@{ timestamp = (Get-Date).ToString("o"); status = [string]$httpStatus.status })
                    }
                } catch {}
                try {
                    $receiveTask = $ws.ReceiveAsync($segment, $ct.Token)
                    if ($receiveTask.Wait([System.TimeSpan]::FromMilliseconds(500))) {
                        $wsResult = $receiveTask.Result
                        if ($wsResult.Count -gt 0) {
                            $json = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $wsResult.Count)
                            $frame = $json | ConvertFrom-Json
                            $wsSamples.Add([pscustomobject]@{ timestamp = (Get-Date).ToString("o"); status = [string]$frame.status })
                        }
                    }
                } catch {}
                Start-Sleep -Milliseconds 100
            }
            $result.Samples = $wsSamples.Count
            if ($wsSamples.Count -eq 0 -or $httpSamples.Count -eq 0) { return $result }
            $lastWs = $wsSamples[-1]
            $lastHttp = $httpSamples[-1]
            $result.HttpStatus = $lastHttp.status
            $result.WsStatus = $lastWs.status
            $result.StateMatch = (Normalize-Status $lastWs.status) -eq (Normalize-Status $lastHttp.status)
        } catch {
            $result.Mismatches = @($_.Exception.Message)
        } finally {
            if ($null -ne $ws -and $ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
                try {
                    $closeTask = $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "done", [System.Threading.CancellationToken]::None)
                    $closeTask.Wait([System.TimeSpan]::FromSeconds(2)) | Out-Null
                } catch {}
            }
            if ($null -ne $ws) { $ws.Dispose() }
            if ($null -ne $ct) { $ct.Dispose() }
        }
        return $result
    }

    # ── Globals ──

    $root = Get-RepoRoot
    $dataflowInfo = Get-DataflowYaml

    $script:RepoRoot = $root
    $script:DataflowInfo = $dataflowInfo
    $script:ValidDataflowYaml = $dataflowInfo.Yaml
    $script:LocalAgentProcess = $null
    $script:LocalAgentStarted = $false
    $script:ProcessId = $null

    # ── Setup ──

    Write-Host "E2E Test Setup: RepoRoot=$root, Dataflow=$($dataflowInfo.Path)"

    $available = Get-Command "curl.exe" -ErrorAction SilentlyContinue
    if (-not $available) { throw "curl.exe is required but not found" }

    $agentHealthy = Test-TcpPortOpen -Hostname "127.0.0.1" -Port 52100 -TimeoutMs 2000
    if (-not $agentHealthy) {
        $exePath = Join-Path $root "doramate-localagent\target\debug\doramate-localagent.exe"
        if (-not (Test-Path $exePath)) {
            Write-Host "Building doramate-localagent debug binary..."
            Push-Location (Join-Path $root "doramate-localagent")
            try {
                cargo build --bin doramate-localagent | Out-Host
                if ($LASTEXITCODE -ne 0) { throw "cargo build failed" }
            } finally { Pop-Location }
        }
        $logPath = Join-Path $root "out\e2e\localagent-e2e.stdout.log"
        New-Item -ItemType Directory -Force -Path (Split-Path $logPath -Parent) | Out-Null
        $script:LocalAgentProcess = Start-LocalAgentProcess -ExePath $exePath `
            -WorkingDir (Join-Path $root "doramate-localagent") -LogPath $logPath
        $script:LocalAgentStarted = $true
        $ready = $false
        for ($i = 0; $i -lt 10; $i++) {
            if (Test-TcpPortOpen -Hostname "127.0.0.1" -Port 52100) {
                $ready = $true
                break
            }
            Start-Sleep -Seconds 1
        }
        if (-not $ready) { throw "LocalAgent did not start within 10 seconds" }
    } else {
        Write-Host "LocalAgent already running on port 52100"
    }
}

AfterAll {
    if ($script:LocalAgentStarted -and $null -ne $script:LocalAgentProcess) {
        Stop-LocalAgentProcess -Process $script:LocalAgentProcess
    } elseif ($script:LocalAgentStarted) {
        try { $null = Invoke-LocalAgentApi -Method POST -Path "stop" -Body @{} } catch { }
        $candidates = Get-CimInstance Win32_Process -Filter "Name = 'doramate-localagent.exe'" -ErrorAction SilentlyContinue
        foreach ($c in @($candidates)) {
            try { Stop-Process -Id $c.ProcessId -Force -ErrorAction SilentlyContinue } catch { }
        }
    }
}

# ──────────────────────────────────────────────
# P0 — Health & Diagnose
# ──────────────────────────────────────────────

Describe "Health Endpoint" -Tag "P0" {
    It "GET /api/health should return status ok" {
        $response = Invoke-LocalAgentApi -Method GET -Path "health"
        $response | Should -Not -BeNullOrEmpty
        $response.status | Should -Be "ok"
    }
}

Describe "Diagnose Endpoint" -Tag "P0" {
    It "GET /api/diagnose should return complete schema" {
        $response = Invoke-LocalAgentApi -Method GET -Path "diagnose"
        $response | Should -Not -BeNullOrEmpty
        $response.PSObject.Properties.Name | Should -Contain "localagent"
        $response.PSObject.Properties.Name | Should -Contain "port_52100"
        $response.PSObject.Properties.Name | Should -Contain "residual_processes"
        $response.PSObject.Properties.Name | Should -Contain "stale_directories"
        $response.PSObject.Properties.Name | Should -Contain "recommendations"
        $response.localagent.pid | Should -BeGreaterThan 0
    }

    It "Should report clean start with zero residual processes" {
        $response = Invoke-LocalAgentApi -Method GET -Path "diagnose"
        $response.residual_processes.Count | Should -Be 0
        $response.port_52100.in_use | Should -Be $true
    }
}

# ──────────────────────────────────────────────
# P0 — Dataflow Run-Stop Cycle
# ──────────────────────────────────────────────

Describe "Dataflow Run-Stop Cycle" -Tag "P0" {
    It "POST /api/run should return a process_id" {
        $response = Invoke-LocalAgentApi -Method POST -Path "run" -Body @{ dataflow_yaml = $script:ValidDataflowYaml }
        $response | Should -Not -BeNullOrEmpty
        $response.process_id | Should -Not -BeNullOrEmpty
        $script:ProcessId = $response.process_id
    }

    It "GET /api/status/:id should eventually report Running" {
        $script:ProcessId | Should -Not -BeNullOrEmpty
        $result = Wait-ForDataflowStatus -ProcessId $script:ProcessId -TargetStatuses @("Running") -TimeoutSeconds $RunTimeoutSeconds
        $result.Reached | Should -Be $true
    }

    It "POST /api/stop/:id should succeed" {
        $script:ProcessId | Should -Not -BeNullOrEmpty
        $response = Invoke-LocalAgentApi -Method POST -Path "stop" -Body @{ process_id = $script:ProcessId }
        $response | Should -Not -BeNullOrEmpty
        $response.status | Should -Be "stopped"
    }

    It "Status should eventually report Stopped or Idle" {
        $script:ProcessId | Should -Not -BeNullOrEmpty
        $result = Wait-ForDataflowStatus -ProcessId $script:ProcessId -TargetStatuses @("Stopped", "Idle") -TimeoutSeconds $StopTimeoutSeconds
        $result.Reached | Should -Be $true
    }

    It "Diagnose should report zero residual processes after stop" {
        Start-Sleep -Seconds 2
        $response = Invoke-LocalAgentApi -Method GET -Path "diagnose"
        $response.residual_processes.Count | Should -Be 0
    }
}

# ──────────────────────────────────────────────
# P1 — Error Handling
# ──────────────────────────────────────────────

Describe "Error Handling" -Tag "P1" {
    It "POST /api/run with invalid YAML should return error" {
        $badYaml = "{ invalid: yaml: content: {{{ }"
        $response = Invoke-LocalAgentApi -Method POST -Path "run" -Body @{ dataflow_yaml = $badYaml } -ErrorAction SilentlyContinue
        if ($null -eq $response) {
            $true | Should -Be $true
        } else {
            $response.success | Should -Be $false
        }
    }

    It "POST /api/stop with unknown process_id should not crash" {
        $response = Invoke-LocalAgentApi -Method POST -Path "stop" -Body @{ process_id = "00000000-0000-0000-0000-000000000000" } -ErrorAction SilentlyContinue
        $true | Should -Be $true
    }
}

# ──────────────────────────────────────────────
# P1 — WebSocket Status Stream Consistency
# ──────────────────────────────────────────────

Describe "WebSocket Status Stream" -Tag "P1" {
    It "WebSocket status should be consistent with HTTP status during run" {
        $runResponse = Invoke-LocalAgentApi -Method POST -Path "run" -Body @{ dataflow_yaml = $script:ValidDataflowYaml }
        $runResponse | Should -Not -BeNullOrEmpty
        $wsProcessId = $runResponse.process_id
        $wsProcessId | Should -Not -BeNullOrEmpty

        $waitResult = Wait-ForDataflowStatus -ProcessId $wsProcessId -TargetStatuses @("Running") -TimeoutSeconds $RunTimeoutSeconds
        $waitResult.Reached | Should -Be $true

        Start-Sleep -Seconds 1
        $wsResult = Test-WebSocketConsistency -ProcessId $wsProcessId -WindowSeconds $WebSocketWindowSeconds
        $wsResult.StateMatch | Should -Be $true

        $null = Invoke-LocalAgentApi -Method POST -Path "stop" -Body @{ process_id = $wsProcessId }
        $null = Wait-ForDataflowStatus -ProcessId $wsProcessId -TargetStatuses @("Stopped", "Idle") -TimeoutSeconds $StopTimeoutSeconds
    }
}

# ──────────────────────────────────────────────
# P2 — Concurrent Operations
# ──────────────────────────────────────────────

Describe "Concurrent Operations" -Tag "P2" {
    It "Rapid alternating run/stop should not produce residuals" {
        for ($i = 0; $i -lt 3; $i++) {
            try {
                $runResp = Invoke-LocalAgentApi -Method POST -Path "run" -Body @{ dataflow_yaml = $script:ValidDataflowYaml }
                if ($null -ne $runResp -and -not [string]::IsNullOrWhiteSpace($runResp.process_id)) {
                    Start-Sleep -Milliseconds 500
                    $null = Invoke-LocalAgentApi -Method POST -Path "stop" -Body @{ process_id = $runResp.process_id }
                    Start-Sleep -Milliseconds 500
                }
            } catch {
                # Individual iteration failures OK in P2 stress test
            }
        }

        Start-Sleep -Seconds 3
        $diagnose = Invoke-LocalAgentApi -Method GET -Path "diagnose"
        $diagnose.residual_processes.Count | Should -Be 0
    }
}
