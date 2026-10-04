[CmdletBinding()]
param(
    [string]$ExecutablePath,
    [ValidateRange(1, 600)][int]$TimeoutSeconds = 90,
    [string]$ProjectPath,
    [string]$OutputDirectory,
    [string]$ServerHost = '127.0.0.1',
    [ValidateRange(1, 65535)][int]$ServerPort = 17350,
    [ValidateSet('http', 'https')][string]$ServerScheme = 'http',
    [ValidateSet('ed_002', 'ed_005')][string]$ReturnCardId,
    [switch]$SimultaneousDraw,
    [switch]$RenderDrawUi,
    [switch]$DrawReadability,
    [switch]$PendingReadability,
    [ValidateRange(320,7680)][int]$CaptureWidth = 1920,
    [ValidateRange(320,7680)][int]$CaptureHeight = 1080,
    [switch]$RenderOnlineUi,
    [switch]$UiDeployment,
    [switch]$BuildingDeployment,
    [switch]$GoatMovement,
    [switch]$DeploymentRejections,
    [ValidateSet('standard_meadow','plains_sunrise','deep_caverns','nether_lava_sea','end_void','deep_ocean','desert_storm')]
    [string]$ExpectedArena = 'standard_meadow'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if ($PendingReadability -and ($SimultaneousDraw -or $ReturnCardId -or $RenderOnlineUi -or $UiDeployment -or $BuildingDeployment -or $GoatMovement -or $DeploymentRejections)) { throw 'PendingReadability is a separate graphical ordinary-command scenario.' }
if ($ReturnCardId -and $SimultaneousDraw) { throw 'Select one online probe scenario.' }
if ($RenderDrawUi -and -not $SimultaneousDraw) { throw 'RenderDrawUi requires SimultaneousDraw.' }
if ($DrawReadability -and (-not $SimultaneousDraw -or -not $RenderDrawUi)) { throw 'DrawReadability requires the graphical simultaneous draw scenario.' }
if ($RenderOnlineUi -and ($SimultaneousDraw -or $ReturnCardId)) { throw 'RenderOnlineUi currently applies to the basic arena scenario only.' }
if ($UiDeployment -and ($SimultaneousDraw -or $ReturnCardId -or -not $RenderOnlineUi)) {
    throw 'UiDeployment requires the graphical basic scenario (-RenderOnlineUi), without return/draw probes.'
}
if ($BuildingDeployment -and ($SimultaneousDraw -or $ReturnCardId -or $UiDeployment -or -not $RenderOnlineUi)) {
    throw 'BuildingDeployment requires RenderOnlineUi and cannot be combined with another probe scenario.'
}
if ($GoatMovement -and ($SimultaneousDraw -or $ReturnCardId -or $UiDeployment -or $BuildingDeployment -or -not $RenderOnlineUi)) {
    throw 'GoatMovement requires RenderOnlineUi and cannot be combined with another probe scenario.'
}
if ($DeploymentRejections -and ($SimultaneousDraw -or $ReturnCardId -or $UiDeployment -or $BuildingDeployment -or $GoatMovement -or -not $RenderOnlineUi)) {
    throw 'DeploymentRejections requires RenderOnlineUi and cannot be combined with another scenario.'
}
if ($DeploymentRejections -and $TimeoutSeconds -lt 250) { throw 'DeploymentRejections needs TimeoutSeconds >= 250 for its fresh-snapshot checks.' }
if (-not $ExecutablePath) {
    $ExecutablePath = Join-Path $repoRoot 'client-unity\Builds\DemoPreview\BiomeRivalsDemo.exe'
}
if (-not (Test-Path -LiteralPath $ExecutablePath)) {
    throw "Windows demo executable was not found: $ExecutablePath"
}

if (-not $ProjectPath) { $ProjectPath = Join-Path $repoRoot 'client-unity' }
$buildManifest = & (Join-Path $PSScriptRoot 'assert-demo-player-source.ps1') -ExecutablePath $ExecutablePath -ProjectPath $ProjectPath
if ([string]::IsNullOrWhiteSpace($ServerHost)) { throw 'ServerHost must not be empty.' }

$runId = [Guid]::NewGuid().ToString('N')
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot "artifacts\online-probe-$runId" }
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw "Refusing to overwrite online probe artifacts: $OutputDirectory" }
[System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$reportA = Join-Path $OutputDirectory 'online-probe-a.json'
$reportB = Join-Path $OutputDirectory 'online-probe-b.json'
$logA = Join-Path $OutputDirectory 'online-probe-a.log'
$logB = Join-Path $OutputDirectory 'online-probe-b.log'
$argumentsA = @(
    '-batchmode','-nographics','-autoOnline','-autoOnlineAction','-autoReconnectProbe',
    '-previewPlayerFaction','plains_forest','-nakamaDeviceId',"online-probe-a-$runId",
    '-onlineProbe',('"{0}"' -f $reportA),'-quitAfterOnlineProbe','-logFile',('"{0}"' -f $logA))
$argumentsB = @(
    '-batchmode','-nographics','-autoOnline','-autoOnlineAction',
    '-previewPlayerFaction','desert_badlands','-nakamaDeviceId',"online-probe-b-$runId",
    '-onlineProbe',('"{0}"' -f $reportB),'-quitAfterOnlineProbe','-logFile',('"{0}"' -f $logB))
if ($ReturnCardId) {
    $argumentsA = @('-batchmode','-nographics','-screen-width','1280','-screen-height','720','-autoOnline',
        '-previewPlayerFaction','end','-nakamaDeviceId',"online-return-a-$runId",
        '-onlineReturnCard',$ReturnCardId,'-onlineReturnBarrier',('"{0}"' -f $OutputDirectory),
        '-onlineProbe',('"{0}"' -f $reportA),'-quitAfterOnlineProbe','-logFile',('"{0}"' -f $logA))
    $argumentsB = @('-batchmode','-nographics','-screen-width','1280','-screen-height','720','-autoOnline',
        '-previewPlayerFaction','end','-nakamaDeviceId',"online-return-b-$runId",
        '-onlineReturnCard',$ReturnCardId,'-onlineReturnBarrier',('"{0}"' -f $OutputDirectory),
        '-onlineProbe',('"{0}"' -f $reportB),'-quitAfterOnlineProbe','-logFile',('"{0}"' -f $logB))
}
if ($SimultaneousDraw) {
    $argumentsA = @('-batchmode','-screen-width','1280','-screen-height','720','-autoOnline','-onlineDrawProbe',
        '-previewPlayerFaction','plains_forest','-nakamaDeviceId',"online-draw-a-$runId",
        '-onlineProbe',('"{0}"' -f $reportA),'-quitAfterOnlineProbe','-logFile',('"{0}"' -f $logA))
    $argumentsB = @('-batchmode','-screen-width','1280','-screen-height','720','-autoOnline','-onlineDrawProbe',
        '-previewPlayerFaction','nether','-nakamaDeviceId',"online-draw-b-$runId",
        '-onlineProbe',('"{0}"' -f $reportB),'-quitAfterOnlineProbe','-logFile',('"{0}"' -f $logB))
    if ($RenderDrawUi) {
        $argumentsA += @('-captureOnline',('"{0}"' -f (Join-Path $OutputDirectory 'online-draw-a.png')),
            '-captureWidth',"$CaptureWidth",'-captureHeight',"$CaptureHeight")
        $argumentsB += @('-captureOnline',('"{0}"' -f (Join-Path $OutputDirectory 'online-draw-b.png')),
            '-captureWidth',"$CaptureWidth",'-captureHeight',"$CaptureHeight")
    }
    else { $argumentsA += '-nographics'; $argumentsB += '-nographics' }
}
if ($PendingReadability) {
    $argumentsA = @('-batchmode','-screen-width',"$CaptureWidth",'-screen-height',"$CaptureHeight",'-autoOnline','-onlinePendingProbe',
        '-previewPlayerFaction','plains_forest','-nakamaDeviceId',"online-pending-a-$runId",
        '-onlinePendingBarrier',('"{0}"' -f $OutputDirectory),
        '-captureWidth',"$CaptureWidth",'-captureHeight',"$CaptureHeight",
        '-onlineProbe',('"{0}"' -f $reportA),'-quitAfterOnlineProbe','-logFile',('"{0}"' -f $logA))
    $argumentsB = @('-batchmode','-screen-width',"$CaptureWidth",'-screen-height',"$CaptureHeight",'-autoOnline','-onlinePendingProbe',
        '-previewPlayerFaction','desert_badlands','-nakamaDeviceId',"online-pending-b-$runId",
        '-onlinePendingBarrier',('"{0}"' -f $OutputDirectory),
        '-captureWidth',"$CaptureWidth",'-captureHeight',"$CaptureHeight",
        '-onlineProbe',('"{0}"' -f $reportB),'-quitAfterOnlineProbe','-logFile',('"{0}"' -f $logB))
}
if ($BuildingDeployment -or $DeploymentRejections) {
    $argumentsA = @('-batchmode','-screen-width','1280','-screen-height','720','-autoOnline','-onlineBuildingProbe',
        '-previewPlayerFaction','cave_dark_forest','-nakamaDeviceId',"online-building-a-$runId",
        '-onlineBuildingBarrier',('"{0}"' -f $OutputDirectory),
        '-onlineProbe',('"{0}"' -f $reportA),'-quitAfterOnlineProbe','-logFile',('"{0}"' -f $logA))
    $argumentsB = @('-batchmode','-screen-width','1280','-screen-height','720','-autoOnline','-onlineBuildingProbe',
        '-previewPlayerFaction','cave_dark_forest','-nakamaDeviceId',"online-building-b-$runId",
        '-onlineBuildingBarrier',('"{0}"' -f $OutputDirectory),
        '-onlineProbe',('"{0}"' -f $reportB),'-quitAfterOnlineProbe','-logFile',('"{0}"' -f $logB))
}

if ($GoatMovement) {
    $argumentsA = @('-batchmode','-autoOnline','-onlineGoatProbe','-previewPlayerFaction','snow_ice',
        '-nakamaDeviceId',"online-goat-a-$runId",'-onlineGoatBarrier',('"{0}"' -f $OutputDirectory),
        '-onlineProbe',('"{0}"' -f $reportA),'-quitAfterOnlineProbe','-logFile',('"{0}"' -f $logA))
    $argumentsB = @('-batchmode','-autoOnline','-onlineGoatProbe','-previewPlayerFaction','snow_ice',
        '-nakamaDeviceId',"online-goat-b-$runId",'-onlineGoatBarrier',('"{0}"' -f $OutputDirectory),
        '-onlineProbe',('"{0}"' -f $reportB),'-quitAfterOnlineProbe','-logFile',('"{0}"' -f $logB))
}

if ($DeploymentRejections) { $argumentsA += '-onlineDeploymentRejections'; $argumentsB += '-onlineDeploymentRejections' }
if ($DrawReadability) { $argumentsA += '-onlineDrawReadability'; $argumentsB += '-onlineDrawReadability' }
$argumentsA += @('-onlineExpectedArena',$ExpectedArena)
$argumentsB += @('-onlineExpectedArena',$ExpectedArena)
if ($UiDeployment) { $argumentsA += '-onlineUiDeploy'; $argumentsB += '-onlineUiDeploy' }
if ($RenderOnlineUi) {
    $argumentsA = @($argumentsA | Where-Object { $_ -ne '-nographics' })
    $argumentsB = @($argumentsB | Where-Object { $_ -ne '-nographics' })
    $argumentsA += @('-screen-width','1280','-screen-height','720','-captureOnline',('"{0}"' -f (Join-Path $OutputDirectory 'online-arena-a.png')),
        '-captureWidth','1920','-captureHeight','1080')
    $argumentsB += @('-screen-width','1280','-screen-height','720','-captureOnline',('"{0}"' -f (Join-Path $OutputDirectory 'online-arena-b.png')),
        '-captureWidth','1920','-captureHeight','1080')
}

$proxyVariables = @('HTTP_PROXY','HTTPS_PROXY','ALL_PROXY')
$environmentVariables = $proxyVariables + @('BIOME_RIVALS_NAKAMA_HOST','BIOME_RIVALS_NAKAMA_PORT','BIOME_RIVALS_NAKAMA_SCHEME')
$savedProxyValues = @{}
foreach ($name in $environmentVariables) {
    $savedProxyValues[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

$processA = $null
$processB = $null
try {
    foreach ($name in $proxyVariables) { [Environment]::SetEnvironmentVariable($name, $null, 'Process') }
    [Environment]::SetEnvironmentVariable('BIOME_RIVALS_NAKAMA_HOST', $ServerHost, 'Process')
    [Environment]::SetEnvironmentVariable('BIOME_RIVALS_NAKAMA_PORT', [string]$ServerPort, 'Process')
    [Environment]::SetEnvironmentVariable('BIOME_RIVALS_NAKAMA_SCHEME', $ServerScheme, 'Process')
    $processA = Start-Process -FilePath $ExecutablePath -ArgumentList $argumentsA -WindowStyle Hidden -PassThru
    $processB = Start-Process -FilePath $ExecutablePath -ArgumentList $argumentsB -WindowStyle Hidden -PassThru
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline -and
           (-not $processA.HasExited -or -not $processB.HasExited)) {
        Start-Sleep -Milliseconds 500
        $processA.Refresh()
        $processB.Refresh()
        foreach ($process in @($processA,$processB)) {
            if ($process.HasExited) {
                $process.WaitForExit()
                if ($process.ExitCode -ne 0) { throw "Online demo Player exited early with code $($process.ExitCode). See $logA and $logB" }
            }
        }
    }
    if (-not (Test-Path -LiteralPath $reportA) -or -not (Test-Path -LiteralPath $reportB)) {
        throw "Online demo probes timed out. See $logA and $logB"
    }
    if (-not $processA.HasExited -or -not $processB.HasExited) { throw "Online demo Player exit timed out. See $logA and $logB" }
    $processA.WaitForExit()
    $processB.WaitForExit()
    if ($processA.ExitCode -ne 0 -or $processB.ExitCode -ne 0) {
        throw "Online demo Player failed: A=$($processA.ExitCode), B=$($processB.ExitCode). See $logA and $logB"
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
    if (-not $ReturnCardId -and -not $SimultaneousDraw -and -not $BuildingDeployment -and -not $DeploymentRejections -and -not $GoatMovement -and ($probeA.playerFaction -ne 'plains_forest' -or $probeB.playerFaction -ne 'desert_badlands')) {
        throw 'Online demo faction projection does not match the submitted factions.'
    }
    if ($probeA.matchStatus -ne 'FINISHED' -or $probeB.matchStatus -ne 'FINISHED') {
        throw 'Online demo probes did not complete the authoritative action scenario.'
    }
    if (-not $SimultaneousDraw -and (-not $probeA.winnerPlayerId -or $probeA.winnerPlayerId -ne $probeB.winnerPlayerId)) {
        throw 'Online demo probes disagree about the concession winner.'
    }
    if ($probeA.revision -ne $probeB.revision) {
        throw 'Online demo probes did not converge on the same final revision.'
    }
    if ($probeA.playerLife -ne $probeB.opponentLife -or $probeA.opponentLife -ne $probeB.playerLife -or
        $probeA.playerUnitCount -ne $probeB.opponentUnitCount -or $probeA.opponentUnitCount -ne $probeB.playerUnitCount) {
        throw 'Unity clients disagree on final public life or battlefield counts.'
    }
    if ($PendingReadability) {
        & (Join-Path $PSScriptRoot 'assert-online-pending-report.ps1') -ProbeA $probeA -ProbeB $probeB
    }
    elseif ($SimultaneousDraw) {
        & (Join-Path $PSScriptRoot 'assert-online-draw-report.ps1') -ProbeA $probeA -ProbeB $probeB -RequireFullHandReadability:$DrawReadability
    }
    elseif ($ReturnCardId) {
        & (Join-Path $PSScriptRoot 'assert-online-return-report.ps1') -ProbeA $probeA -ProbeB $probeB -ReturnCardId $ReturnCardId
    }
    elseif ($DeploymentRejections) {
        & (Join-Path $PSScriptRoot 'assert-online-building-report.ps1') -ProbeA $probeA -ProbeB $probeB -ExpectedArena $ExpectedArena
        & (Join-Path $PSScriptRoot 'assert-online-deployment-rejections.ps1') -ProbeA $probeA -ProbeB $probeB -ExpectedArena $ExpectedArena
    }
    elseif ($GoatMovement) {
        & (Join-Path $PSScriptRoot 'assert-online-goat-report.ps1') -ProbeA $probeA -ProbeB $probeB -ExpectedArena $ExpectedArena
    }
    elseif ($BuildingDeployment) {
        & (Join-Path $PSScriptRoot 'assert-online-building-report.ps1') -ProbeA $probeA -ProbeB $probeB -ExpectedArena $ExpectedArena
    }
    else {
    & (Join-Path $PSScriptRoot 'assert-online-arena-report.ps1') -ProbeA $probeA -ProbeB $probeB -ExpectedArena $ExpectedArena -RequireUiDeployment:$UiDeployment
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
    }
    $evidence = [ordered]@{
        ok = $true
        matchId = $probeA.matchId
        revision = $probeA.revision
        winnerPlayerId = $probeA.winnerPlayerId
        endpoint = "${ServerScheme}://${ServerHost}:${ServerPort}"
        sourceFileCount = $buildManifest.inputs.Count
        unityVersion = $buildManifest.unityVersion
        scenario = $(if ($PendingReadability) { 'ordinary-pending-readability' } elseif ($SimultaneousDraw) { 'simultaneous-draw' } elseif ($ReturnCardId) { "end-return-$ReturnCardId" } elseif ($DeploymentRejections) { 'deployment-atomic-rejections' } elseif ($GoatMovement) { 'ui-goat-movement' } elseif ($BuildingDeployment) { 'ui-building-footprints' } elseif ($UiDeployment) { 'ui-final-unit-deployment' } else { 'basic-actions' })
        expectedArena = $ExpectedArena
        readableHand = [bool]$DrawReadability
        playerExitCodes = @($processA.ExitCode, $processB.ExitCode)
        reportHashes = @((Get-FileHash -LiteralPath $reportA -Algorithm SHA256).Hash, (Get-FileHash -LiteralPath $reportB -Algorithm SHA256).Hash)
        logPaths = @($logA, $logB)
    }
    if ($PendingReadability -or $RenderDrawUi -or $RenderOnlineUi) {
        $screenshotNames = $(if ($PendingReadability) { @('online-probe-a.pending.png','online-probe-b.pending.png') } elseif ($RenderDrawUi) { @('online-draw-a.png','online-draw-b.png') } else { @('online-arena-a.png','online-arena-b.png') })
        $screenshots = $screenshotNames | ForEach-Object { Join-Path $OutputDirectory $_ }
        foreach ($screenshot in $screenshots) {
            $pngBytes = [IO.File]::ReadAllBytes($screenshot)
            if ($pngBytes.Length -lt 24 -or [BitConverter]::ToString($pngBytes[0..7]) -ne '89-50-4E-47-0D-0A-1A-0A') {
                throw "Invalid online screenshot: $screenshot"
            }
            $widthBytes = [byte[]]$pngBytes[16..19]; [Array]::Reverse($widthBytes)
            $heightBytes = [byte[]]$pngBytes[20..23]; [Array]::Reverse($heightBytes)
            $expectedWidth = if ($RenderDrawUi -or $PendingReadability) { $CaptureWidth } else { 1920 }
            $expectedHeight = if ($RenderDrawUi -or $PendingReadability) { $CaptureHeight } else { 1080 }
            if ([BitConverter]::ToInt32($widthBytes,0) -ne $expectedWidth -or [BitConverter]::ToInt32($heightBytes,0) -ne $expectedHeight) {
                throw 'Online draw screenshot dimensions are incorrect.'
            }
        }
        $evidence['screenshotHashes'] = @($screenshots | ForEach-Object { (Get-FileHash -LiteralPath $_).Hash })
    }
    [System.IO.File]::WriteAllText((Join-Path $OutputDirectory 'validation.json'), ($evidence | ConvertTo-Json -Depth 5))
    Write-Output "Online demo validation passed: $($probeA.matchId), final revision $($probeA.revision), winner $($probeA.winnerPlayerId). Evidence: $OutputDirectory"
}
finally {
    foreach ($process in @($processA,$processB)) {
        if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    }
    foreach ($name in $environmentVariables) {
        if ($null -eq $savedProxyValues[$name]) {
            # PowerShell can bind a null string argument as ""; the provider removes it.
            Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
        }
        else {
            [Environment]::SetEnvironmentVariable($name, [string]$savedProxyValues[$name], 'Process')
        }
    }
}
