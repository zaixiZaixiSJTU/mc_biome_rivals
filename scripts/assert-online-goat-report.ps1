[CmdletBinding()]
param(
    [Parameter(Mandatory)]$ProbeA,
    [Parameter(Mandatory)]$ProbeB,
    [Parameter(Mandatory)][ValidateSet('standard_meadow','plains_sunrise','deep_caverns','nether_lava_sea','end_void','deep_ocean','desert_storm')][string]$ExpectedArena
)
$ErrorActionPreference = 'Stop'
$units = $(if ($ExpectedArena -eq 'deep_caverns') { 5 } elseif ($ExpectedArena -in @('nether_lava_sea','end_void')) { 3 } else { 4 })
if ((@($ProbeA.role,$ProbeB.role | Sort-Object) -join ',') -ne 'mover,observer') { throw 'Goat reports must cover mover and observer exactly once.' }
foreach ($probe in @($ProbeA,$ProbeB)) {
    foreach ($field in @('ok','eventCausalityVerified','footprintVerified','worldVerified','privateProjectionVerified','reconnectRecovered','recoveryStateVerified')) {
        if ($probe.$field -isnot [bool] -or -not $probe.$field) { throw "Goat proof missing: $field" }
    }
    foreach ($field in @('performedTargetUiDeploy','performedGoatUiDeploy','paymentVerified')) {
        if ($probe.$field -isnot [bool] -or $probe.$field -ne ($probe.role -eq 'mover')) { throw "Goat role-specific proof invalid: $field" }
    }
    foreach ($field in @('revision','frozenRevision','recoveredRevision','unitSlotCount','buildingSlotCount','fromSlotIndex','toSlotIndex','goatSlotIndex','movementEventCount','targetEventId','goatEventId','movementEventId')) {
        if ($probe.$field -isnot [int] -and $probe.$field -isnot [long]) { throw "Goat integer proof missing: $field" }
    }
    if ($probe.arenaId -ne $ExpectedArena -or $probe.unitSlotCount -ne $units -or $probe.buildingSlotCount -ne 7-$units -or
        $probe.fromSlotIndex -ne $units-3 -or $probe.goatSlotIndex -ne $units-2 -or $probe.toSlotIndex -ne $units-1 -or
        $probe.frozenRevision -lt 1 -or $probe.recoveredRevision -ne $probe.frozenRevision -or $probe.revision -ne $probe.frozenRevision+1 -or
        $probe.movementEventCount -ne 1 -or $probe.targetEventId -lt 1 -or $probe.goatEventId -le $probe.targetEventId -or $probe.movementEventId -le $probe.goatEventId -or
        $probe.targetInstanceId -cnotmatch '^object-[0-9]+$' -or $probe.goatInstanceId -cnotmatch '^object-[0-9]+$' -or
        $probe.targetInstanceId -eq $probe.goatInstanceId -or $probe.movementSourceInstanceId -ne $probe.goatInstanceId -or
        $probe.movementSourceCardId -ne 'si_004' -or $probe.movementEffectId -ne 'effect.si_004.01' -or
        $probe.publicBoardHash -cnotmatch '^[A-F0-9]{64}$' -or $probe.playerFaction -ne 'snow_ice' -or $probe.opponentFaction -ne 'snow_ice' -or
        $probe.matchStatus -ne 'FINISHED') { throw 'Goat identity/cause/footprint/recovery proof inconsistent.' }
}
foreach ($field in @('matchId','arenaId','revision','frozenRevision','recoveredRevision','targetInstanceId','goatInstanceId','targetEventId','goatEventId','movementEventId','publicBoardHash','winnerPlayerId')) {
    if ($ProbeA.$field -ne $ProbeB.$field -or [string]::IsNullOrWhiteSpace([string]$ProbeA.$field)) { throw "Goat clients disagree: $field" }
}
if (-not $ProbeA.viewerPlayerId -or -not $ProbeB.viewerPlayerId -or $ProbeA.viewerPlayerId -eq $ProbeB.viewerPlayerId) { throw 'Goat viewer identities invalid.' }
$mover = $(if ($ProbeA.role -eq 'mover') { $ProbeA } else { $ProbeB })
if ($ProbeA.winnerPlayerId -ne $mover.viewerPlayerId) { throw 'Goat concession winner is not the mover.' }
