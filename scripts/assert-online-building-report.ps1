[CmdletBinding()]
param(
    [Parameter(Mandatory)]$ProbeA,
    [Parameter(Mandatory)]$ProbeB,
    [Parameter(Mandatory)][ValidateSet('standard_meadow','plains_sunrise','deep_caverns','nether_lava_sea','end_void','deep_ocean','desert_storm')][string]$ExpectedArena
)
$ErrorActionPreference = 'Stop'
$units = $(if ($ExpectedArena -eq 'deep_caverns') { 5 } elseif ($ExpectedArena -in @('nether_lava_sea','end_void')) { 3 } else { 4 })
$buildings = 7 - $units
$roles = @($ProbeA.role,$ProbeB.role | Sort-Object)
if (($roles -join ',') -ne 'single,structure') { throw 'Building reports must cover both roles exactly once.' }
foreach ($probe in @($ProbeA,$ProbeB)) {
    foreach ($field in @('ok','performedUiDeploy','paymentVerified','worldVerified','privateProjectionVerified','reconnectRecovered','recoveryStateVerified')) {
        if ($probe.$field -isnot [bool] -or $probe.$field -ne $true) { throw "Building proof missing: $field" }
    }
    foreach ($field in @('revision','frozenRevision','recoveredRevision','unitSlotCount','buildingSlotCount','singleSlotIndex','structureSlotIndex','singleEventId','structureEventId')) {
        if ($probe.$field -isnot [int] -and $probe.$field -isnot [long]) { throw "Building integer proof missing: $field" }
    }
    if ($probe.arenaId -ne $ExpectedArena -or $probe.unitSlotCount -ne $units -or $probe.buildingSlotCount -ne $buildings -or
        $probe.singleSlotIndex -ne $buildings - 1 -or $probe.structureSlotIndex -ne $buildings - 2 -or
        $probe.frozenRevision -lt 1 -or $probe.recoveredRevision -ne $probe.frozenRevision -or $probe.revision -ne $probe.frozenRevision + 1 -or
        $probe.singleEventId -lt 1 -or $probe.structureEventId -lt 1 -or $probe.singleEventId -eq $probe.structureEventId -or
        $probe.singleInstanceId -cnotmatch '^object-[0-9]+$' -or $probe.structureInstanceId -cnotmatch '^object-[0-9]+$' -or
        $probe.singleInstanceId -eq $probe.structureInstanceId -or $probe.publicBoardHash -cnotmatch '^[A-F0-9]{64}$' -or
        $probe.deploymentEventHash -cnotmatch '^[A-F0-9]{64}$' -or $probe.playerFaction -ne 'cave_dark_forest' -or
        $probe.opponentFaction -ne 'cave_dark_forest' -or $probe.matchStatus -ne 'FINISHED') { throw 'Building identity/footprint/recovery proof is inconsistent.' }
}
foreach ($field in @('matchId','revision','frozenRevision','recoveredRevision','singleInstanceId','structureInstanceId','singleEventId','structureEventId','publicBoardHash','deploymentEventHash','winnerPlayerId')) {
    if ($ProbeA.$field -ne $ProbeB.$field -or $null -eq $ProbeA.$field -or [string]::IsNullOrWhiteSpace([string]$ProbeA.$field)) { throw "Building clients disagree: $field" }
}
if (-not $ProbeA.viewerPlayerId -or -not $ProbeB.viewerPlayerId -or $ProbeA.viewerPlayerId -eq $ProbeB.viewerPlayerId) { throw 'Building viewer identities are invalid.' }
$single = $(if ($ProbeA.role -eq 'single') { $ProbeA } else { $ProbeB })
if ($ProbeA.winnerPlayerId -ne $single.viewerPlayerId) { throw 'Building concession winner is not the single-building viewer.' }
