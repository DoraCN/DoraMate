param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$DoraPath = "dora",
    [string]$OutputDir = "",
    [int]$TimeoutSeconds = 90,
    [int]$ThroughputMessages = 100,
    [switch]$SkipBuild,
    [switch]$IncludeRust
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-LogText {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return ""
    }

    $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
    try {
        $reader = [System.IO.StreamReader]::new($stream)
        try {
            return $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Stop-BenchmarkProcess {
    param([System.Diagnostics.Process]$Process)

    if ($null -ne $Process -and -not $Process.HasExited) {
        Stop-Process -Id $Process.Id -Force -ErrorAction SilentlyContinue
        $Process.WaitForExit(5000) | Out-Null
    }
}

function Invoke-DoraRun {
    param(
        [string]$Name,
        [string]$WorkingDirectory,
        [string]$Dataflow,
        [hashtable]$Environment
    )

    $stdoutPath = Join-Path $OutputDir "$Name.stdout.log"
    $stderrPath = Join-Path $OutputDir "$Name.stderr.log"
    $process = $null
    try {
        Write-Host "[benchmark-csharp] Running $Name..." -ForegroundColor Cyan
        $startInfo = @{
            FilePath = $DoraPath
            ArgumentList = @("run", $Dataflow)
            WorkingDirectory = $WorkingDirectory
            RedirectStandardOutput = $stdoutPath
            RedirectStandardError = $stderrPath
            PassThru = $true
        }
        if ($IsWindows) {
            $startInfo.WindowStyle = "Hidden"
        }
        if ($Environment.Count -gt 0) {
            $startInfo.Environment = $Environment
        }

        $process = Start-Process @startInfo
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            throw "$Name benchmark timed out after $TimeoutSeconds second(s)."
        }
        if ($process.ExitCode -ne 0) {
            throw "$Name benchmark failed with exit code $($process.ExitCode)."
        }
    }
    finally {
        Stop-BenchmarkProcess -Process $process
    }

    [pscustomobject]@{
        StdoutPath = $stdoutPath
        StderrPath = $stderrPath
        Stdout = Get-LogText -Path $stdoutPath
        Stderr = Get-LogText -Path $stderrPath
    }
}

function Parse-CSharpResults {
    param([string]$Text)

    $results = @()
    $latencyPattern = [regex]'BENCH,csharp,latency,(?<size>\d+),(?<count>\d+),(?<avg>[\d.]+),(?<p50>[\d.]+),(?<p95>[\d.]+),(?<p99>[\d.]+)'
    foreach ($match in $latencyPattern.Matches($Text)) {
        $results += [pscustomobject]@{
            Runtime = "csharp"
            Metric = "latency_us"
            SizeBytes = [int]$match.Groups["size"].Value
            Count = [int]$match.Groups["count"].Value
            Average = [double]$match.Groups["avg"].Value
            P50 = [double]$match.Groups["p50"].Value
            P95 = [double]$match.Groups["p95"].Value
            P99 = [double]$match.Groups["p99"].Value
        }
    }

    $throughputPattern = [regex]'BENCH,csharp,throughput,(?<size>\d+),(?<count>\d+),(?<mps>[\d.]+)'
    foreach ($match in $throughputPattern.Matches($Text)) {
        $results += [pscustomobject]@{
            Runtime = "csharp"
            Metric = "messages_per_second"
            SizeBytes = [int]$match.Groups["size"].Value
            Count = [int]$match.Groups["count"].Value
            Average = [double]$match.Groups["mps"].Value
            P50 = $null
            P95 = $null
            P99 = $null
        }
    }

    return $results
}

function Parse-RustResults {
    param([string]$Text)

    $results = @()
    $phase = ""
    foreach ($line in ($Text -split "`r?`n")) {
        if ($line -match 'Latency:') {
            $phase = "latency"
            continue
        }
        if ($line -match 'Throughput:') {
            $phase = "throughput"
            continue
        }
        if ($line -notmatch 'size\s+0x(?<size>[0-9a-fA-F]+)\s*:\s*(?<value>[\d.]+)(?<unit>[^`r`n]*)') {
            continue
        }

        $size = [Convert]::ToInt32($Matches["size"], 16)
        $value = [double]$Matches["value"]
        $unit = $Matches["unit"]
        if ($phase -eq "latency") {
            if ($unit -match 'ms') {
                $value *= 1000
            }
            elseif ($unit -match 'ns') {
                $value /= 1000
            }

            $results += [pscustomobject]@{
                Runtime = "rust"
                Metric = "latency_us"
                SizeBytes = $size
                Count = $null
                Average = $value
                P50 = $null
                P95 = $null
                P99 = $null
            }
        }
        elseif ($phase -eq "throughput") {
            $results += [pscustomobject]@{
                Runtime = "rust"
                Metric = "messages_per_second"
                SizeBytes = $size
                Count = $null
                Average = $value
                P50 = $null
                P95 = $null
                P99 = $null
            }
        }
    }

    return $results
}

function Write-MarkdownReport {
    param(
        [object[]]$Results,
        [string]$Path,
        [string]$CsvPath
    )

    $lines = @(
        "# Dora C# Binding Benchmark Results",
        "",
        "- Timestamp: $(Get-Date -Format o)",
        "- Throughput messages per size: $ThroughputMessages",
        "- CSV: $CsvPath",
        "",
        "## Latency",
        "",
        "| Runtime | Size bytes | Avg us | P50 us | P95 us | P99 us |",
        "| ------- | ---------- | ------ | ------ | ------ | ------ |"
    )

    foreach ($result in ($Results | Where-Object { $_.Metric -eq "latency_us" } | Sort-Object SizeBytes, Runtime)) {
        $lines += "| $($result.Runtime) | $($result.SizeBytes) | $($result.Average) | $($result.P50) | $($result.P95) | $($result.P99) |"
    }

    $lines += @(
        "",
        "## Throughput",
        "",
        "| Runtime | Size bytes | Messages/s | Count |",
        "| ------- | ---------- | ---------- | ----- |"
    )

    foreach ($result in ($Results | Where-Object { $_.Metric -eq "messages_per_second" } | Sort-Object SizeBytes, Runtime)) {
        $lines += "| $($result.Runtime) | $($result.SizeBytes) | $($result.Average) | $($result.Count) |"
    }

    Set-Content -LiteralPath $Path -Value $lines -Encoding UTF8
}

$csharpRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$sampleDir = Join-Path $csharpRoot "samples\csharp-benchmark-dataflow"
$rustBenchmarkDir = Join-Path $csharpRoot "third_party\dora\examples\benchmark"

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputDir = Join-Path $csharpRoot "artifacts\benchmark\benchmark-$timestamp"
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

if (-not $SkipBuild) {
    foreach ($project in @("BenchmarkProducer.csproj", "BenchmarkSink.csproj")) {
        Write-Host "[benchmark-csharp] Building $project..." -ForegroundColor Cyan
        dotnet build (Join-Path $sampleDir $project) -c $Configuration -p:NuGetAudit=false
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed for $project with exit code $LASTEXITCODE."
        }
    }
}

$envVars = @{
    DORA_CSHARP_BENCH_THROUGHPUT_MESSAGES = "$ThroughputMessages"
}

$allResults = @()
$csharpRun = Invoke-DoraRun -Name "csharp" -WorkingDirectory $sampleDir -Dataflow "dataflow.yml" -Environment $envVars
$allResults += Parse-CSharpResults -Text $csharpRun.Stdout

if ($IncludeRust) {
    Write-Host "[benchmark-csharp] Building Rust benchmark examples..." -ForegroundColor Cyan
    Push-Location (Join-Path $csharpRoot "third_party\dora")
    try {
        cargo build -p benchmark-example-node -p benchmark-example-sink --release
        if ($LASTEXITCODE -ne 0) {
            throw "cargo build failed for Rust benchmark examples with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }

    $rustRun = Invoke-DoraRun -Name "rust" -WorkingDirectory $rustBenchmarkDir -Dataflow "dataflow.yml" -Environment @{}
    $allResults += Parse-RustResults -Text $rustRun.Stdout
}

if ($allResults.Count -eq 0) {
    throw "No benchmark results were parsed. Logs: $OutputDir"
}

$csvPath = Join-Path $OutputDir "benchmark-results.csv"
$reportPath = Join-Path $OutputDir "benchmark-report.md"
$allResults | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8
Write-MarkdownReport -Results $allResults -Path $reportPath -CsvPath $csvPath

Write-Host "[benchmark-csharp] Results: $csvPath" -ForegroundColor Green
Write-Host "[benchmark-csharp] Report: $reportPath" -ForegroundColor Green
Write-Host "[benchmark-csharp] Logs: $OutputDir"
