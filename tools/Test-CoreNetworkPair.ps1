# 실제 개발 빌드 두 개의 독립 프로세스로 Initial_Stage 네트워크 수명을 검증한다.
# 이 스크립트가 시작한 PID만 관리하며 기존 Editor/사용자 게임 프로세스를 종료하지 않는다.
[CmdletBinding()]
param(
    [string]$Executable = '',
    [ValidateRange(0, 65535)][int]$Port = 0,
    [ValidateRange(30, 900)][int]$TimeoutSeconds = 450
)
$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($Executable)) {
    $Executable = Join-Path $projectRoot 'Builds/CoreNetworkValidation/TheDeveloper.exe'
}
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$runDirectory = Join-Path $projectRoot ('Docs/Local/Validation/CoreSprint20261015/NetworkPair/' + $runId)
New-Item -ItemType Directory -Path $runDirectory | Out-Null
if ($Port -eq 0) {
    $reservation = [System.Net.Sockets.UdpClient]::new(0)
    try { $Port = ([System.Net.IPEndPoint]$reservation.Client.LocalEndPoint).Port }
    finally { $reservation.Dispose() }
}
$hostProcess = $null
$clientProcess = $null
$completed = $false
function Start-Probe([string]$Role) {
    $resultPath = Join-Path $runDirectory ($Role + '.json')
    $logPath = Join-Path $runDirectory ($Role + '.log')
    $arguments = @('-batchmode', '-nographics', '-logFile', ('"' + $logPath + '"'),
        '--core-network-role', $Role, '--core-network-port', $Port,
        '--core-network-run', $runId, '--core-network-result', ('"' + $resultPath + '"'))
    Start-Process -FilePath $Executable -ArgumentList $arguments -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
}
try {
    $hostProcess = Start-Probe 'host'
    Write-Output "Host PID=$($hostProcess.Id); run=$runId; port=$Port"
    $startupDeadline = [DateTime]::UtcNow.AddSeconds(90)
    $hostReady = $false
    while ([DateTime]::UtcNow -lt $startupDeadline) {
        $hostProcess.Refresh()
        if ($hostProcess.HasExited) { throw 'Host exited before its listening confirmation.' }
        $hostLog = Join-Path $runDirectory 'host.log'
        if (Test-Path -LiteralPath $hostLog) {
            $hostReady = [bool](Select-String -LiteralPath $hostLog -SimpleMatch 'CORE_NETWORK_PROBE_HOST_LISTENING' -Quiet)
            if ($hostReady) { break }
        }
        Start-Sleep -Milliseconds 500
    }
    if (!$hostReady) { throw 'Host startup did not complete within 90 seconds.' }
    $clientProcess = Start-Probe 'client'
    Write-Output "Client PID=$($clientProcess.Id)"
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        $hostProcess.Refresh(); $clientProcess.Refresh()
        if ($hostProcess.HasExited -and $clientProcess.HasExited) { break }
        if ($hostProcess.HasExited -and !(Test-Path -LiteralPath (Join-Path $runDirectory 'host.json'))) {
            throw 'Host exited without verification evidence.'
        }
        if ($clientProcess.HasExited -and !(Test-Path -LiteralPath (Join-Path $runDirectory 'client.json'))) {
            throw 'Client exited without verification evidence.'
        }
        foreach ($role in @('host', 'client')) {
            $resultPath = Join-Path $runDirectory ($role + '.json')
            if (Test-Path -LiteralPath $resultPath) {
                $endpoint = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
                if (!$endpoint.Success) { throw "$role verification failed: $($endpoint.Error)" }
            }
        }
        Start-Sleep -Seconds 1
    }
    $hostProcess.Refresh(); $clientProcess.Refresh()
    if (!$hostProcess.HasExited -or !$clientProcess.HasExited) { throw 'Network pair timed out; inspect per-role logs.' }
    $hostProcess.WaitForExit(); $clientProcess.WaitForExit()
    $hostResult = Get-Content -LiteralPath (Join-Path $runDirectory 'host.json') -Raw | ConvertFrom-Json
    $clientResult = Get-Content -LiteralPath (Join-Path $runDirectory 'client.json') -Raw | ConvertFrom-Json
    if ($hostProcess.ExitCode -ne 0 -or $clientProcess.ExitCode -ne 0 -or !$hostResult.Success -or !$clientResult.Success) {
        throw "Endpoint verification failed. Host=$($hostResult.Error); Client=$($clientResult.Error)"
    }
    $required = @('Static', 'ClockPaused', 'ClockRate', 'ClockReset', 'Dynamic', 'Release', 'Ended', 'Restart', 'FinalEnded', 'Disconnect', 'Disconnected')
    foreach ($result in @($hostResult, $clientResult)) {
        foreach ($phase in $required) {
            if ($phase -notin $result.Observations.Phase) { throw "Missing phase $phase in $($result.Role)." }
        }
        if ($result.Run -ne $runId -or $result.Errors.Count -ne 0) { throw 'Wrong run or runtime errors in endpoint evidence.' }
        foreach ($check in @('On:Applied:', 'Replay:Applied:', 'Off:Applied:', 'Far:Unauthorized:OutOfInteractionRange',
            'NoActor:Unauthorized:AuthorityActorUnavailable', 'ForeignMatch:DifferentMatch:', 'ForgedIssuer:Unauthorized:')) {
            if (!@($result.TurretChecks | Where-Object { $_.StartsWith($check) }).Count) { throw "Missing real command check $check in $($result.Role)." }
        }
    }
    if ($hostResult.ProcessId -eq $clientResult.ProcessId -or $hostResult.Build -ne $clientResult.Build) {
        throw 'Expected two independent processes of the same build.'
    }
    $runtimeErrors = @(Get-ChildItem -LiteralPath $runDirectory -Filter '*.log' | ForEach-Object {
        Select-String -LiteralPath $_.FullName -Pattern '^.*Exception:|^.*Error:|LogError|LogException|Assertion failed|CORE_NETWORK_PROBE_FAILED'
    })
    if ($runtimeErrors.Count -ne 0) { throw 'Runtime error signatures found, including startup or shutdown; inspect endpoint logs.' }
    $shutdownWarnings = @(Get-ChildItem -LiteralPath $runDirectory -Filter '*.log' | ForEach-Object {
        Select-String -LiteralPath $_.FullName -SimpleMatch 'GarbageCollector disposing of ComputeBuffer'
    })
    $summary = [ordered]@{
        success = $true; run = $runId; port = $Port; executable = $Executable
        hostPid = $hostResult.ProcessId; clientPid = $clientResult.ProcessId
        hostExit = $hostProcess.ExitCode; clientExit = $clientProcess.ExitCode; build = $hostResult.Build
        runtimeErrorSignatures = $runtimeErrors.Count; computeBufferShutdownWarnings = $shutdownWarnings.Count
        requiredPhases = $required; turretChecks = $hostResult.TurretChecks; turretFixtureScope = $hostResult.TurretFixtureScope
        scope = 'Actual Initial_Stage; loopback; full HUD-frame hashes; active cohort disconnect. No full Player/Turret combat replication or GUI acceptance'
    }
    $summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runDirectory 'verified.json') -Encoding utf8
    $completed = $true
    Write-Output "VERIFIED $runDirectory"
}
finally {
    if (!$completed) {
        foreach ($process in @($hostProcess, $clientProcess)) {
            if ($null -eq $process) { continue }
            $process.Refresh()
            if (!$process.HasExited) { Stop-Process -Id $process.Id -Force }
        }
        Write-Output "FAILED evidence=$runDirectory"
    }
}
