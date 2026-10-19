#requires -Version 7.2
[CmdletBinding()]
param(
    [switch] $UseRealOpenAI,
    [switch] $CheckOnly,
    [ValidateRange(600, 3600)] [int] $TimeoutSeconds = 1200
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$runId = 'sdverify' + [Guid]::NewGuid().ToString('N')
$resultDirectory = Join-Path $repositoryRoot "test-results/LiveIntegration/$runId"
$null = New-Item -ItemType Directory -Path $resultDirectory -Force
$reportPath = Join-Path $resultDirectory 'report.json'
$composePath = Join-Path $PSScriptRoot 'compose.yaml'
$names = @('Infrastructure', 'Ticket creation', 'Atomic Outbox', 'RabbitMQ publication',
    'AI processing', 'Durable result', 'RabbitMQ acknowledgement', 'Duplicate delivery',
    'Notification / Mailpit', 'AI failure recovery', 'Persistence failure', 'Broker outage',
    'Worker shutdown', 'Real OpenAI smoke test', 'Cleanup')
$report = [ordered]@{ RunId = $runId; Overall = 'NOT RUN'; Scenarios = @($names | ForEach-Object {
    [ordered]@{ Name = $_; Status = 'NOT RUN'; Detail = '' }
}) }
$originalEnvironment = @{}
$ownedInfrastructure = $false
$exitCode = 2

function Set-RunEnvironment([string] $Name, [string] $Value) {
    if (-not $originalEnvironment.ContainsKey($Name)) {
        $originalEnvironment[$Name] = [Environment]::GetEnvironmentVariable($Name, 'Process')
    }
    [Environment]::SetEnvironmentVariable($Name, $Value, 'Process')
}
function Set-Scenario([string] $Name, [string] $Status, [string] $Detail) {
    $scenario = $report.Scenarios | Where-Object { $_.Name -eq $Name }
    $scenario.Status = $Status
    $scenario.Detail = $Detail
}
function Invoke-Tool([string] $Command, [string[]] $Arguments, [int] $LimitSeconds) {
    $start = [Diagnostics.ProcessStartInfo]::new($Command)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $output = $process.StandardOutput.ReadToEndAsync()
        $errorOutput = $process.StandardError.ReadToEndAsync()
        try { $null = $process.WaitForExitAsync().WaitAsync([TimeSpan]::FromSeconds($LimitSeconds)).GetAwaiter().GetResult() }
        catch {
            if (-not $process.HasExited) { $process.Kill($true) }
            throw 'The owned verification command exceeded its deadline.'
        }
        $null = $errorOutput.GetAwaiter().GetResult() # Never persist raw configuration/provider diagnostics.
        return @{ ExitCode = $process.ExitCode; Output = $output.GetAwaiter().GetResult() }
    }
    finally { $process.Dispose() }
}
function Invoke-Compose([string[]] $Arguments) {
    # Every command is scoped to a newly generated project; there are no shared/external volumes.
    $result = Invoke-Tool 'docker' (@('compose', '--project-name', $runId, '--file', $composePath) + $Arguments) 360
    if ($result.ExitCode -ne 0) { throw 'The test-owned Docker Compose operation failed.' }
    return $result.Output
}
function Get-Port([string] $Service, [int] $Port) {
    $binding = (Invoke-Compose @('port', $Service, [string]$Port) | Out-String).Trim()
    if ($binding -notmatch '^127\.0\.0\.1:(\d+)$') { throw 'Expected a dedicated loopback Docker port.' }
    return $Matches[1]
}

try {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'BLOCKED: .NET 10 SDK is required.' }
    $sdkVersions = Invoke-Tool 'dotnet' @('--list-sdks') 30
    if ($sdkVersions.ExitCode -ne 0 -or $sdkVersions.Output -notmatch '(?m)^10\.') { throw 'BLOCKED: .NET 10 SDK is required.' }
    if (-not (Get-Command docker -CommandType Application -ErrorAction SilentlyContinue)) {
        # A terminal opened before Docker Desktop installation can have a stale PATH.
        foreach ($directory in @((Join-Path $env:ProgramFiles 'Docker\Docker\resources\bin'),
            (Join-Path $env:LOCALAPPDATA 'Programs\DockerDesktop\resources\bin'))) {
            if (Test-Path -LiteralPath (Join-Path $directory 'docker.exe') -ErrorAction SilentlyContinue) {
                Set-RunEnvironment 'PATH' ($directory + [IO.Path]::PathSeparator + $env:PATH)
                break
            }
        }
    }
    if (-not (Get-Command docker -CommandType Application -ErrorAction SilentlyContinue)) { throw 'BLOCKED: Docker CLI and a Linux Docker engine are required.' }
    $engine = Invoke-Tool 'docker' @('info', '--format', '{{.OSType}}') 30
    if ($engine.ExitCode -ne 0 -or $engine.Output.Trim() -ne 'linux') { throw 'BLOCKED: A running Linux Docker engine is required.' }
    $composeVersion = Invoke-Tool 'docker' @('compose', 'version', '--short') 30
    if ($composeVersion.ExitCode -ne 0) { throw 'BLOCKED: Docker Compose v2 is required.' }
    if ($CheckOnly) {
        Set-Scenario 'Infrastructure' 'PASS' 'Prerequisite checks passed; no services started.'
        $report.Overall = 'SKIPPED'
        $exitCode = 0
    }
    else {
        Set-RunEnvironment 'SDV_RUN_ID' $runId
        Set-RunEnvironment 'SDV_SQL_PASSWORD' ([Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24)) + 'aA!9')
        Set-RunEnvironment 'SDV_BROKER_PASSWORD' ([Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24)))
        Set-RunEnvironment 'SDV_REPO_ROOT' $repositoryRoot
        Set-RunEnvironment 'SDV_COMPOSE_PATH' $composePath
        Set-RunEnvironment 'SDV_REPORT_PATH' $reportPath
        Set-RunEnvironment 'SDV_REAL_OPENAI' ([string][bool]$UseRealOpenAI)
        Set-RunEnvironment 'SDV_TIMEOUT_SECONDS' ([string]$TimeoutSeconds)
        $build = Invoke-Tool 'dotnet' @('build', (Join-Path $PSScriptRoot 'ServiceDesk.LiveIntegration.csproj'), '-v', 'quiet', '-clp:ErrorsOnly') 300
        if ($build.ExitCode -ne 0) { throw 'Building the verification runner failed.' }
        # Docker can reassign an automatically published port after stop/start. Select distinct
        # loopback ports up front and explicitly bind them for the lifetime of this project.
        $portReservations = @()
        try {
            foreach ($name in @('SDV_SQL_PORT', 'SDV_AMQP_PORT', 'SDV_MANAGEMENT_PORT', 'SDV_SMTP_PORT', 'SDV_MAILPIT_PORT')) {
                $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
                $listener.Start()
                $portReservations += $listener
                Set-RunEnvironment $name ([string]$listener.LocalEndpoint.Port)
            }
        }
        finally { foreach ($listener in $portReservations) { $listener.Stop() } }
        # Track ownership before up: partial startup must also be cleaned up.
        $ownedInfrastructure = $true
        $null = Invoke-Compose @('up', '--detach')
        foreach ($mapping in @(@('sql',1433,'SDV_SQL_PORT'), @('rabbitmq',5672,'SDV_AMQP_PORT'),
            @('rabbitmq',15672,'SDV_MANAGEMENT_PORT'), @('mailpit',1025,'SDV_SMTP_PORT'), @('mailpit',8025,'SDV_MAILPIT_PORT'))) {
            Set-RunEnvironment $mapping[2] (Get-Port $mapping[0] $mapping[1])
        }
        $runner = Join-Path $PSScriptRoot 'bin/Debug/net10.0/ServiceDesk.LiveIntegration.dll'
        Write-Host 'Running isolated verification. Broker outage recovery honors the five-minute Outbox lease.'
        $run = Invoke-Tool 'dotnet' @($runner) ($TimeoutSeconds + 120)
        $exitCode = $run.ExitCode
        if (-not (Test-Path -LiteralPath $reportPath)) { throw 'The runner did not produce a report.' }
        $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json -AsHashtable
        if ($report.Overall -notin @('PASS', 'FAIL', 'BLOCKED')) { throw 'The runner did not finalize its outcome.' }
    }
}
catch {
    # Preserve the last atomic runner report if the CLI deadline or an unexpected exit interrupts it.
    if (Test-Path -LiteralPath $reportPath) {
        try { $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json -AsHashtable } catch { }
    }
    $isBlocked = $_.Exception.Message.StartsWith('BLOCKED:')
    $detail = if ($isBlocked) { $_.Exception.Message } else { 'Verification setup failed (' + $_.Exception.GetType().Name + '). No application secrets are included in this report.' }
    $interrupted = @($report.Scenarios | Where-Object { $_.Status -eq 'RUNNING' })
    if ($interrupted.Count -gt 0) {
        foreach ($scenario in $interrupted) { $scenario.Status = 'FAIL'; $scenario.Detail = $detail }
    }
    else { Set-Scenario 'Infrastructure' $(if ($isBlocked) { 'BLOCKED' } else { 'FAIL' }) $detail }
    $report.Overall = if ($isBlocked) { 'BLOCKED' } else { 'FAIL' }
    $exitCode = if ($isBlocked) { 2 } else { 1 }
}
finally {
    try {
        if ($ownedInfrastructure) {
            $null = Invoke-Compose @('down', '--volumes', '--timeout', '30')
            $cleanup = $report.Scenarios | Where-Object { $_.Name -eq 'Cleanup' }
            if ($cleanup.Status -ne 'FAIL') { Set-Scenario 'Cleanup' 'PASS' 'Removed only this run''s Compose project and containers.' }
        }
        else { Set-Scenario 'Cleanup' 'SKIPPED' 'No infrastructure was started.' }
    }
    catch {
        Set-Scenario 'Cleanup' 'FAIL' "Manual cleanup required for Docker project $runId."
        $report.Overall = 'FAIL'
        $exitCode = 1
    }
    foreach ($name in $originalEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $originalEnvironment[$name], 'Process')
    }
    $report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportPath -Encoding utf8
    $sanitizedLog = @("ServiceDesk verification $runId", "Overall: $($report.Overall)")
    $sanitizedLog += $report.Scenarios | ForEach-Object { '{0}: {1} {2}' -f $_.Name, $_.Status, $_.Detail }
    $sanitizedLog | Set-Content -LiteralPath (Join-Path $resultDirectory 'verification.log') -Encoding utf8
    Write-Host "`nServiceDesk Live Integration Verification ($runId)"
    foreach ($scenario in $report.Scenarios) { Write-Host ('{0,-29} {1}' -f $scenario.Name, $scenario.Status) }
    Write-Host "Overall: $($report.Overall)"
    Write-Host "Sanitized report: $reportPath"
}
exit $exitCode
