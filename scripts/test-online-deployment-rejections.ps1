[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$validator = Join-Path $PSScriptRoot 'assert-online-deployment-rejections.ps1'
function New-AuditPair([string]$arena) {
    $units = $(if ($arena -eq 'deep_caverns') { 5 } elseif ($arena -in @('nether_lava_sea','end_void')) { 3 } else { 4 })
    $pair = @()
    foreach ($role in @('single','structure')) {
        $labels = @('unit-negative','unit-capacity','unit-capacity-plus-one','unit-occupied','building-negative','building-capacity')
        if ($role -eq 'structure') { $labels += @('structure-edge','building-occupied') } else { $labels += 'structure-occupied' }
        $cases = @()
        foreach ($label in $labels) {
            $kind = $(if ($label -like 'unit-*') { 'UNIT' } else { 'BUILDING' })
            $card = $(if ($kind -eq 'UNIT') { 'cd_002' } elseif ($label -eq 'building-occupied') { 'cd_004' } elseif ($label -like 'structure-*' -or $role -eq 'structure') { 'cd_007' } else { 'cd_004' })
            $slot = switch ($label) {
                'unit-negative' {-1}; 'unit-capacity' {$units}; 'unit-capacity-plus-one' {$units+1}; 'unit-occupied' {$units-1}
                'building-negative' {-1}; 'building-capacity' {7-$units}; 'structure-edge' {6-$units}; 'structure-occupied' {5-$units}; 'building-occupied' {5-$units}
            }
            $id = $cases.Count + $(if ($role -eq 'single') {1} else {20})
            $cases += [pscustomobject]@{ label=$label; commandId=('online-{0:x32}' -f $id); code=$(if ($label -like '*occupied') {'SLOT_OCCUPIED'} else {'INVALID_TARGET'})
                cardId=$card; slotKind=$kind; slotIndex=$slot; revision=10; recoveredRevision=10; effectiveCost=$(if ($card -eq 'cd_007') {4} else {2})
                energyBefore=6; energyAfter=6; beforeHash=('A'*64); afterHash=('A'*64); rejected=$true; noEvents=$true; freshSnapshotVerified=$true }
        }
        $pair += [pscustomobject]@{ role=$role; arenaId=$arena; frozenRevision=20; deploymentRejectionsVerified=$true; deploymentRejections=$cases }
    }
    return ,$pair
}
foreach ($arena in @('standard_meadow','plains_sunrise','deep_caverns','nether_lava_sea','end_void','deep_ocean','desert_storm')) {
    $pair = New-AuditPair $arena
    & $validator -ProbeA $pair[0] -ProbeB $pair[1] -ExpectedArena $arena
}
$controls = @(
    @{field='deploymentRejectionsVerified';value=$false;report=$true}, @{field='deploymentRejectionsVerified';value='true';report=$true},
    @{field='deploymentRejections';value=$null;report=$true}, @{field='role';value='single';report=$true}, @{field='arenaId';value='deep_caverns';report=$true},
    @{field='slotIndex';value=0}, @{field='slotIndex';value='-1'}, @{field='code';value='CARD_NOT_IN_HAND'}, @{field='cardId';value='si_003'},
    @{field='cardId';value='cd_008'}, @{field='effectiveCost';value=0}, @{field='energyBefore';value=1}, @{field='energyAfter';value=5}, @{field='recoveredRevision';value=11},
    @{field='revision';value=21}, @{field='beforeHash';value='bad'}, @{field='afterHash';value=('B'*64)}, @{field='commandId';value='uncorrelated'}
)
foreach ($field in @('rejected','noEvents','freshSnapshotVerified')) {
    foreach ($value in @($false,$null,'true')) { $controls += @{field=$field;value=$value} }
}
foreach ($control in $controls) {
    $pair = New-AuditPair nether_lava_sea
    if ($control.report) { $pair[1].($control.field)=$control.value } else { $pair[1].deploymentRejections[0].($control.field)=$control.value }
    $rejected=$false
    try { & $validator -ProbeA $pair[0] -ProbeB $pair[1] -ExpectedArena nether_lava_sea } catch { $rejected=$true }
    if (-not $rejected) { throw "Deployment report false positive: $($control.field)" }
}
foreach ($mode in @('duplicate-label','duplicate-command','missing-edge','wrong-overlap-card','wrong-overlap-label')) {
    $pair = New-AuditPair nether_lava_sea
    if ($mode -eq 'duplicate-label') { $pair[1].deploymentRejections[1].label=$pair[1].deploymentRejections[0].label }
    if ($mode -eq 'duplicate-command') { $pair[1].deploymentRejections[0].commandId=$pair[0].deploymentRejections[0].commandId }
    if ($mode -eq 'missing-edge') { $pair[1].deploymentRejections=@($pair[1].deploymentRejections | Where-Object label -ne 'structure-edge') }
    if ($mode -eq 'wrong-overlap-card') { ($pair[1].deploymentRejections | Where-Object label -eq 'building-occupied').cardId='cd_007' }
    if ($mode -eq 'wrong-overlap-label') { ($pair[1].deploymentRejections | Where-Object label -eq 'building-occupied').label='structure-occupied' }
    $rejected=$false
    try { & $validator -ProbeA $pair[0] -ProbeB $pair[1] -ExpectedArena nether_lava_sea } catch { $rejected=$true }
    if (-not $rejected) { throw "Deployment report false positive: $mode" }
}
Write-Output "Online deployment rejection validator: 7 positive / $($controls.Count+5) negative controls passed."
