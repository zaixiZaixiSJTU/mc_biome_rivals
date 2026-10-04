[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$validator = Join-Path $PSScriptRoot 'assert-online-building-report.ps1'
$base = [pscustomobject]@{
    ok=$true; performedUiDeploy=$true; paymentVerified=$true; worldVerified=$true; privateProjectionVerified=$true; reconnectRecovered=$true; recoveryStateVerified=$true
    role='single'; matchId='building-audit'; viewerPlayerId='alice'; winnerPlayerId='alice'; matchStatus='FINISHED'; playerFaction='cave_dark_forest'; opponentFaction='cave_dark_forest'
    arenaId='nether_lava_sea'; unitSlotCount=3; buildingSlotCount=4; revision=20; frozenRevision=19; recoveredRevision=19
    singleInstanceId='object-1'; structureInstanceId='object-2'; singleSlotIndex=3; structureSlotIndex=2; singleEventId=8; structureEventId=12
    publicBoardHash=('A'*64); deploymentEventHash=('B'*64)
}
$peer = $base | ConvertTo-Json | ConvertFrom-Json
$peer.role='structure'; $peer.viewerPlayerId='bob'
foreach ($arena in @('deep_caverns','nether_lava_sea','standard_meadow')) {
    $units = $(if ($arena -eq 'deep_caverns') { 5 } elseif ($arena -eq 'nether_lava_sea') { 3 } else { 4 })
    $a = $base | ConvertTo-Json | ConvertFrom-Json; $b = $peer | ConvertTo-Json | ConvertFrom-Json
    foreach ($probe in @($a,$b)) { $probe.arenaId=$arena; $probe.unitSlotCount=$units; $probe.buildingSlotCount=7-$units; $probe.singleSlotIndex=6-$units; $probe.structureSlotIndex=5-$units }
    & $validator -ProbeA $a -ProbeB $b -ExpectedArena $arena
}
$negative = @()
foreach ($field in @('ok','performedUiDeploy','paymentVerified','worldVerified','privateProjectionVerified','reconnectRecovered','recoveryStateVerified')) {
    foreach ($value in @($false,$null,'true')) { $negative += @{ field=$field; value=$value } }
}
$negative += @(
    @{field='role';value='single'}, @{field='arenaId';value='standard_meadow'}, @{field='unitSlotCount';value=4}, @{field='buildingSlotCount';value=3},
    @{field='singleSlotIndex';value=2}, @{field='structureSlotIndex';value=3}, @{field='recoveredRevision';value=20}, @{field='revision';value=21},
    @{field='singleEventId';value=0}, @{field='structureEventId';value=8}, @{field='singleInstanceId';value='object-2'},
    @{field='publicBoardHash';value=('C'*64)}, @{field='deploymentEventHash';value=$null}, @{field='matchStatus';value='ACTIVE'},
    @{field='winnerPlayerId';value='bob'}, @{field='viewerPlayerId';value='alice'}
)
foreach ($control in $negative) {
    $changed = $peer | ConvertTo-Json | ConvertFrom-Json
    $changed.($control.field)=$control.value
    $rejected=$false
    try { & $validator -ProbeA $base -ProbeB $changed -ExpectedArena nether_lava_sea } catch { $rejected=$true }
    if (-not $rejected) { throw "Building report false positive: $($control.field)" }
}
Write-Output "Online building report validator: 3 positive / $($negative.Count) negative controls passed."
