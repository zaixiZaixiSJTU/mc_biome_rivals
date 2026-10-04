[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$validator = Join-Path $PSScriptRoot 'assert-online-goat-report.ps1'
$base = [pscustomobject]@{
    ok=$true; performedTargetUiDeploy=$true; performedGoatUiDeploy=$true; paymentVerified=$true
    eventCausalityVerified=$true; footprintVerified=$true; worldVerified=$true; privateProjectionVerified=$true; reconnectRecovered=$true; recoveryStateVerified=$true
    role='mover'; matchId='goat-audit'; viewerPlayerId='alice'; winnerPlayerId='alice'; matchStatus='FINISHED'; playerFaction='snow_ice'; opponentFaction='snow_ice'
    arenaId='nether_lava_sea'; unitSlotCount=3; buildingSlotCount=4; revision=20; frozenRevision=19; recoveredRevision=19
    targetInstanceId='object-1'; goatInstanceId='object-2'; targetEventId=8; goatEventId=12; movementEventId=13; movementEventCount=1
    fromSlotIndex=0; goatSlotIndex=1; toSlotIndex=2; movementSourceInstanceId='object-2'; movementSourceCardId='si_004'; movementEffectId='effect.si_004.01'; publicBoardHash=('A'*64)
}
$peer = $base | ConvertTo-Json | ConvertFrom-Json
$peer.role='observer'; $peer.viewerPlayerId='bob'; $peer.performedTargetUiDeploy=$false; $peer.performedGoatUiDeploy=$false; $peer.paymentVerified=$false
foreach ($arena in @('standard_meadow','plains_sunrise','deep_caverns','nether_lava_sea','end_void','deep_ocean','desert_storm')) {
    $units = $(if ($arena -eq 'deep_caverns') { 5 } elseif ($arena -in @('nether_lava_sea','end_void')) { 3 } else { 4 })
    $a = $base | ConvertTo-Json | ConvertFrom-Json; $b = $peer | ConvertTo-Json | ConvertFrom-Json
    foreach ($probe in @($a,$b)) { $probe.arenaId=$arena; $probe.unitSlotCount=$units; $probe.buildingSlotCount=7-$units; $probe.fromSlotIndex=$units-3; $probe.goatSlotIndex=$units-2; $probe.toSlotIndex=$units-1 }
    & $validator -ProbeA $a -ProbeB $b -ExpectedArena $arena
}
$negative = @()
foreach ($field in @('ok','eventCausalityVerified','footprintVerified','worldVerified','privateProjectionVerified','reconnectRecovered','recoveryStateVerified')) {
    foreach ($value in @($false,$null,'true')) { $negative += @{field=$field;value=$value} }
}
foreach ($field in @('performedTargetUiDeploy','performedGoatUiDeploy','paymentVerified')) {
    foreach ($value in @($true,$null,'false')) { $negative += @{field=$field;value=$value} }
}
$negative += @(
    @{field='role';value='mover'}, @{field='arenaId';value='deep_caverns'}, @{field='unitSlotCount';value=5}, @{field='buildingSlotCount';value=2},
    @{field='fromSlotIndex';value=1}, @{field='goatSlotIndex';value=2}, @{field='toSlotIndex';value=0}, @{field='movementEventCount';value=2},
    @{field='goatEventId';value=8}, @{field='movementEventId';value=12}, @{field='targetInstanceId';value='object-2'}, @{field='movementSourceInstanceId';value='object-1'},
    @{field='movementSourceCardId';value='si_002'}, @{field='movementEffectId';value='effect.si_002.01'}, @{field='frozenRevision';value='19'},
    @{field='recoveredRevision';value=18}, @{field='revision';value=22}, @{field='publicBoardHash';value=('B'*64)},
    @{field='winnerPlayerId';value='bob'}, @{field='viewerPlayerId';value='alice'}, @{field='matchStatus';value='ACTIVE'}
)
foreach ($control in $negative) {
    $changed = $peer | ConvertTo-Json | ConvertFrom-Json; $changed.($control.field)=$control.value
    $rejected=$false
    try { & $validator -ProbeA $base -ProbeB $changed -ExpectedArena nether_lava_sea } catch { $rejected=$true }
    if (-not $rejected) { throw "Goat report false positive: $($control.field)" }
}
Write-Output "Online goat report validator: 7 positive / $($negative.Count) negative controls passed."
