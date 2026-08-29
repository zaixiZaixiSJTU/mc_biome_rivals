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
    '-batchmode','-nographics','-autoOnline','-autoOnlineAction',
    '-previewPlayerFaction','plains_forest','-nakamaDeviceId',"online-probe-a-$runId",
    '-onlineProbe',$reportA,'-quitAfterOnlineProbe','-logFile',$logA)
$argumentsB = @(
    '-batchmode','-nographics','-autoOnline','-autoOnlineAction',
    '-previewPlayerFaction','desert_badlands','-nakamaDeviceId',"online-probe-b-$runId",
    '-onlineProbe',$reportB,'-quitAfterOnlineProbe','-logFile',$logB)

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
    if ($probeA.playerFaction -ne 'plains_forest' -or $probeB.playerFaction -ne 'desert_badlands') {
        throw 'Online demo faction projection does not match the submitted factions.'
    }
    if ($probeA.matchStatus -ne 'ACTIVE' -or $probeB.matchStatus -ne 'ACTIVE') {
        throw 'Online demo probes did not complete the opening-hand phase.'
    }
    Write-Output "Online demo validation passed: $($probeA.matchId), revisions $($probeA.revision)/$($probeB.revision)."
}
finally {
    foreach ($process in @($processA,$processB)) {
        if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    }
}
