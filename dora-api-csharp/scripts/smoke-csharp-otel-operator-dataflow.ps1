param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [int]$TimeoutSeconds = 45,
    [string]$DoraPath = "dora",
    [string]$OutputDir = "",
    [switch]$SkipBuild,
    [switch]$Restore
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

function Stop-SmokeProcess {
    param([System.Diagnostics.Process]$Process)

    if ($null -ne $Process -and -not $Process.HasExited) {
        Stop-Process -Id $Process.Id -Force -ErrorAction SilentlyContinue
        $Process.WaitForExit(5000) | Out-Null
    }
}

function Get-MatchObjects {
    param(
        [string]$Text,
        [string]$Pattern
    )

    $regex = [regex]::new($Pattern, [System.Text.RegularExpressions.RegexOptions]::Multiline)
    $groupNames = $regex.GetGroupNames() | Where-Object { $_ -notmatch '^\d+$' }
    $matches = $regex.Matches($Text)
    $objects = @()
    foreach ($match in $matches) {
        $item = [ordered]@{}
        foreach ($name in $groupNames) {
            $item[$name] = $match.Groups[$name].Value
        }

        $objects += [pscustomobject]$item
    }

    return $objects
}

function Test-TraceContinuity {
    param([string]$Text)

    $producerPattern = 'PRODUCER trace=(?<trace>[0-9a-f]{32}) span=(?<span>[0-9a-f]{16}) payload=(?<payload>\S+)'
    $operatorPattern = 'OPERATOR trace=(?<trace>[0-9a-f]{32}) parent=(?<parent>[0-9a-f]{16}) upstream_span=(?<upstream>[0-9a-f]{16}) span=(?<span>[0-9a-f]{16}) payload=(?<payload>\S+) output=(?<output>\S+)'
    $consumerPattern = 'CONSUMER trace=(?<trace>[0-9a-f]{32}) parent=(?<parent>[0-9a-f]{16}) upstream_trace=(?<upstream>[0-9a-f]{32}) payload=(?<payload>\S+)'

    $producers = @(Get-MatchObjects -Text $Text -Pattern $producerPattern)
    $operators = @(Get-MatchObjects -Text $Text -Pattern $operatorPattern)
    $consumers = @(Get-MatchObjects -Text $Text -Pattern $consumerPattern)

    foreach ($producer in $producers) {
        $operator = $operators | Where-Object { $_.payload -eq $producer.payload } | Select-Object -First 1
        if ($null -eq $operator) {
            continue
        }

        $consumer = $consumers | Where-Object { $_.payload -eq $operator.output } | Select-Object -First 1
        if ($null -eq $consumer) {
            continue
        }

        $sameTrace = $producer.trace -eq $operator.trace -and $producer.trace -eq $consumer.trace -and $consumer.upstream -eq $producer.trace
        $producerParent = $operator.parent -eq $producer.span -and $operator.upstream -eq $producer.span
        $operatorParent = $consumer.parent -eq $operator.span

        if ($sameTrace -and $producerParent -and $operatorParent) {
            return [pscustomobject]@{
                Success = $true
                TraceId = $producer.trace
                ProducerSpanId = $producer.span
                OperatorSpanId = $operator.span
                ConsumerParentSpanId = $consumer.parent
                Payload = $producer.payload
                Output = $operator.output
            }
        }
    }

    return [pscustomobject]@{
        Success = $false
        ProducerCount = $producers.Count
        OperatorCount = $operators.Count
        ConsumerCount = $consumers.Count
    }
}

$csharpRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$sampleDir = Join-Path $csharpRoot "samples\csharp-otel-operator-dataflow"

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputDir = Join-Path $csharpRoot "artifacts\smoke\csharp-otel-operator-dataflow-$timestamp"
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$stdoutPath = Join-Path $OutputDir "dora-run.stdout.log"
$stderrPath = Join-Path $OutputDir "dora-run.stderr.log"

if (-not $SkipBuild) {
    Write-Host "[smoke-csharp-otel-operator] Building native bridge..." -ForegroundColor Cyan
    & pwsh (Join-Path $PSScriptRoot "build-native.ps1")
    if ($LASTEXITCODE -ne 0) {
        throw "build-native.ps1 failed with exit code $LASTEXITCODE."
    }

    foreach ($project in @("Producer.csproj", "OtelOperator.csproj", "Consumer.csproj")) {
        if ($Restore) {
            Write-Host "[smoke-csharp-otel-operator] Restoring $project..." -ForegroundColor Cyan
            dotnet restore (Join-Path $sampleDir $project) -p:NuGetAudit=false
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet restore failed for $project with exit code $LASTEXITCODE."
            }
        }

        $verb = if ($project -eq "OtelOperator.csproj") { "publish" } else { "build" }
        Write-Host "[smoke-csharp-otel-operator] Running dotnet $verb $project..." -ForegroundColor Cyan
        dotnet $verb (Join-Path $sampleDir $project) -c $Configuration -p:NuGetAudit=false --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet $verb failed for $project with exit code $LASTEXITCODE."
        }
    }
}

$process = $null
try {
    Write-Host "[smoke-csharp-otel-operator] Running Dora dataflow..." -ForegroundColor Cyan
    $startInfo = @{
        FilePath = $DoraPath
        ArgumentList = @("run", "dataflow.yml")
        WorkingDirectory = $sampleDir
        RedirectStandardOutput = $stdoutPath
        RedirectStandardError = $stderrPath
        PassThru = $true
    }
    if ($IsWindows) {
        $startInfo.WindowStyle = "Hidden"
    }

    $process = Start-Process @startInfo

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        Start-Sleep -Milliseconds 500
        $stdout = Get-LogText -Path $stdoutPath
        $result = Test-TraceContinuity -Text $stdout
        if ($result.Success) {
            Stop-SmokeProcess -Process $process
            Write-Host "[smoke-csharp-otel-operator] OK trace=$($result.TraceId) producer_span=$($result.ProducerSpanId) operator_span=$($result.OperatorSpanId) consumer_parent=$($result.ConsumerParentSpanId) payload=$($result.Payload)" -ForegroundColor Green
            Write-Host "[smoke-csharp-otel-operator] Logs: $OutputDir"
            exit 0
        }
    } while ((Get-Date) -lt $deadline -and -not $process.HasExited)

    $stdout = Get-LogText -Path $stdoutPath
    $stderr = Get-LogText -Path $stderrPath
    $result = Test-TraceContinuity -Text $stdout
    $stdoutTail = (($stdout -split "`r?`n") | Select-Object -Last 80) -join [Environment]::NewLine
    $stderrTail = (($stderr -split "`r?`n") | Select-Object -Last 80) -join [Environment]::NewLine

    throw @"
Operator trace continuity smoke failed within $TimeoutSeconds second(s).
Observed: producers=$($result.ProducerCount), operators=$($result.OperatorCount), consumers=$($result.ConsumerCount)
Logs: $OutputDir

stdout tail:
$stdoutTail

stderr tail:
$stderrTail
"@
}
finally {
    Stop-SmokeProcess -Process $process
}
