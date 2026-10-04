[CmdletBinding()]
param(
    [Parameter(Mandatory)]$ProbeA,
    [Parameter(Mandatory)]$ProbeB,
    [Parameter(Mandatory)][ValidateSet('standard_meadow','plains_sunrise','deep_caverns','nether_lava_sea','end_void','deep_ocean','desert_storm')][string]$ExpectedArena
)
$ErrorActionPreference = 'Stop'
$units = $(if ($ExpectedArena -eq 'deep_caverns') { 5 } elseif ($ExpectedArena -in @('nether_lava_sea','end_void')) { 3 } else { 4 })
$buildings = 7-$units
if ((@($ProbeA.role,$ProbeB.role | Sort-Object) -join ',') -ne 'single,structure') { throw 'Deployment rejection reports need both roles.' }
$ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($probe in @($ProbeA,$ProbeB)) {
    if ($probe.deploymentRejectionsVerified -isnot [bool] -or -not $probe.deploymentRejectionsVerified -or $probe.arenaId -ne $ExpectedArena) { throw 'Deployment rejection proof missing.' }
    $expected = @('unit-negative','unit-capacity','unit-capacity-plus-one','unit-occupied','building-negative','building-capacity')
    if ($probe.role -eq 'structure') { $expected += @('structure-edge','building-occupied') } else { $expected += 'structure-occupied' }
    if ($probe.deploymentRejections -isnot [array] -or $probe.deploymentRejections.Count -ne $expected.Count -or
        ((@($probe.deploymentRejections.label | Sort-Object) -join ',') -cne (@($expected | Sort-Object) -join ','))) { throw 'Deployment rejection cases missing/duplicated.' }
    foreach ($case in $probe.deploymentRejections) {
        foreach ($field in @('rejected','noEvents','freshSnapshotVerified')) {
            if ($case.$field -isnot [bool] -or -not $case.$field) { throw "Deployment case proof missing: $field" }
        }
        foreach ($field in @('slotIndex','revision','recoveredRevision','effectiveCost','energyBefore','energyAfter')) {
            if ($case.$field -isnot [int] -and $case.$field -isnot [long]) { throw "Deployment case integer missing: $field" }
        }
        $slot = switch ($case.label) {
            'unit-negative' { -1 }; 'unit-capacity' { $units }; 'unit-capacity-plus-one' { $units+1 }; 'unit-occupied' { $units-1 }
            'building-negative' { -1 }; 'building-capacity' { $buildings }; 'structure-edge' { $buildings-1 }; 'structure-occupied' { $buildings-2 }; 'building-occupied' { $buildings-2 }
        }
        $code = $(if ($case.label -like '*occupied') { 'SLOT_OCCUPIED' } else { 'INVALID_TARGET' })
        $kind = $(if ($case.label -like 'unit-*') { 'UNIT' } else { 'BUILDING' })
        $card = $(if ($case.label -eq 'building-occupied') { 'cd_004' } elseif ($case.label -like 'structure-*' -or $probe.role -eq 'structure' -and $kind -eq 'BUILDING') { 'cd_007' } else { 'cd_004' })
        if ($kind -eq 'UNIT') {
            if ($case.cardId -notin @('cd_001','cd_002','cd_003','cd_005')) { throw 'Unsafe or wrong unit rejection card.' }
        } elseif ($case.cardId -ne $card) { throw 'Wrong building rejection card.' }
        $costs = @{cd_001=1;cd_002=2;cd_003=2;cd_004=2;cd_005=3;cd_007=4}
        if ($case.commandId -cnotmatch '^online-[a-f0-9]{32}$' -or -not $ids.Add($case.commandId) -or
            $case.slotKind -ne $kind -or $case.slotIndex -ne $slot -or $case.code -ne $code -or $case.revision -lt 1 -or
            $case.recoveredRevision -ne $case.revision -or $case.revision -gt $probe.frozenRevision -or
            $case.effectiveCost -ne $costs[$case.cardId] -or $case.energyBefore -lt $case.effectiveCost -or $case.energyBefore -gt 10 -or
            $case.energyAfter -ne $case.energyBefore -or $case.beforeHash -cnotmatch '^[A-F0-9]{64}$' -or $case.afterHash -cne $case.beforeHash) {
            throw 'Deployment rejection identity, payment, complete-state hash or recovery proof inconsistent.'
        }
    }
}
