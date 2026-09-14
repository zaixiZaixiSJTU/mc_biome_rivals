[CmdletBinding()]
param(
    [string]$ExecutablePath,
    [int]$TimeoutSeconds = 90
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $ExecutablePath) {
    $ExecutablePath = Join-Path $repoRoot 'client-unity\Builds\DemoPreview\BiomeRivalsDemo.exe'
}
if (-not (Test-Path -LiteralPath $ExecutablePath)) {
    throw "Windows demo executable was not found: $ExecutablePath"
}

$artifactsPath = Join-Path $repoRoot 'artifacts'
$logsPath = Join-Path $repoRoot 'client-unity\Logs'
[System.IO.Directory]::CreateDirectory($artifactsPath) | Out-Null
[System.IO.Directory]::CreateDirectory($logsPath) | Out-Null
$reportA = Join-Path $artifactsPath 'online-probe-a.json'
$reportB = Join-Path $artifactsPath 'online-probe-b.json'
$logA = Join-Path $logsPath 'online-probe-a.log'
$logB = Join-Path $logsPath 'online-probe-b.log'
Remove-Item -LiteralPath $reportA,$reportB,$logA,$logB -Force -ErrorAction SilentlyContinue

$runId = [Guid]::NewGuid().ToString('N')
$argumentsA = @(
    '-batchmode','-nographics','-autoOnline','-autoOnlineAction','-autoReconnectProbe',
    '-previewPlayerFaction','plains_forest','-nakamaDeviceId',"online-probe-a-$runId",
    '-onlineProbe',$reportA,'-quitAfterOnlineProbe','-logFile',$logA)
$argumentsB = @(
    '-batchmode','-nographics','-autoOnline','-autoOnlineAction',
    '-previewPlayerFaction','desert_badlands','-nakamaDeviceId',"online-probe-b-$runId",
    '-onlineProbe',$reportB,'-quitAfterOnlineProbe','-logFile',$logB)

$proxyVariables = @('HTTP_PROXY','HTTPS_PROXY','ALL_PROXY')
$savedProxyValues = @{}
foreach ($name in $proxyVariables) {
    $savedProxyValues[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    [Environment]::SetEnvironmentVariable($name, $null, 'Process')
}

$processA = Start-Process -FilePath $ExecutablePath -ArgumentList $argumentsA -WindowStyle Hidden -PassThru
$processB = Start-Process -FilePath $ExecutablePath -ArgumentList $argumentsB -WindowStyle Hidden -PassThru
try {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline -and
           (-not (Test-Path -LiteralPath $reportA) -or -not (Test-Path -LiteralPath $reportB))) {
        Start-Sleep -Milliseconds 500
    }
    if (-not (Test-Path -LiteralPath $reportA) -or -not (Test-Path -LiteralPath $reportB)) {
        throw "Online demo probes timed out. See $logA and $logB"
    }

    $probeA = Get-Content -Raw -Encoding utf8 -LiteralPath $reportA | ConvertFrom-Json
    $probeB = Get-Content -Raw -Encoding utf8 -LiteralPath $reportB | ConvertFrom-Json
    if (-not $probeA.ok -or -not $probeB.ok) { throw 'At least one online demo probe reported failure.' }
    if ($probeA.matchId -ne $probeB.matchId) { throw 'Online demo probes joined different authoritative matches.' }
    if ($probeA.viewerPlayerId -eq $probeB.viewerPlayerId) { throw 'Online demo probes reused one player identity.' }
    if ($probeA.accountPhase -ne 'Ready' -or $probeB.accountPhase -ne 'Ready') {
        throw 'At least one Unity account service was not ready after device authentication.'
    }
    if ($probeA.accountUserId -ne $probeA.viewerPlayerId -or $probeB.accountUserId -ne $probeB.viewerPlayerId) {
        throw 'The lobby account identity does not match the authoritative match viewer.'
    }
    if (-not $probeA.accountDisplayName -or -not $probeB.accountDisplayName) {
        throw 'At least one authenticated lobby profile has no display name.'
    }
    if ($probeA.playerFaction -ne 'plains_forest' -or $probeB.playerFaction -ne 'desert_badlands') {
        throw 'Online demo faction projection does not match the submitted factions.'
    }
    if ($probeA.matchStatus -ne 'FINISHED' -or $probeB.matchStatus -ne 'FINISHED') {
        throw 'Online demo probes did not complete the authoritative action scenario.'
    }
    if (-not $probeA.winnerPlayerId -or $probeA.winnerPlayerId -ne $probeB.winnerPlayerId) {
        throw 'Online demo probes disagree about the concession winner.'
    }
    if ($probeA.revision -ne $probeB.revision) {
        throw 'Online demo probes did not converge on the same final revision.'
    }
    if (-not ($probeA.performedDeploy -or $probeB.performedDeploy)) { throw 'No Unity client deployed a unit.' }
    if (-not ($probeA.performedAttack -or $probeB.performedAttack)) { throw 'No Unity client attacked.' }
    if (-not ($probeA.performedEndTurn -or $probeB.performedEndTurn)) { throw 'No Unity client ended a turn.' }
    if (-not ($probeA.performedConcede -or $probeB.performedConcede)) { throw 'No Unity client conceded.' }
    if (-not $probeA.reconnectRecovered) { throw 'The first Unity client did not recover from the forced connection loss.' }
    if ([Math]::Min([Math]::Min($probeA.playerLife, $probeA.opponentLife), [Math]::Min($probeB.playerLife, $probeB.opponentLife)) -ge 30) {
        throw 'The authoritative hero attack did not change either projected life total.'
    }
    if (($probeA.playerUnitCount + $probeA.opponentUnitCount) -lt 1 -or
        ($probeB.playerUnitCount + $probeB.opponentUnitCount) -lt 1) {
        throw 'A deployed unit was not present in both final projections.'
    }
    Write-Output "Online demo validation passed: $($probeA.matchId), final revision $($probeA.revision), winner $($probeA.winnerPlayerId)."
}
finally {
    foreach ($process in @($processA,$processB)) {
        if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    }
    foreach ($name in $proxyVariables) {
        [Environment]::SetEnvironmentVariable($name, $savedProxyValues[$name], 'Process')
    }
}
