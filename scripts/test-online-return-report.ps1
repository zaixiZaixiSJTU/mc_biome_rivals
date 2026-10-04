[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$assertPath = Join-Path $PSScriptRoot 'assert-online-return-report.ps1'
function Copy-Probe($probe) { $probe | ConvertTo-Json -Depth 5 | ConvertFrom-Json }
$owner = [pscustomobject]@{
    ok = $true; role = 'owner'; viewerPlayerId = 'owner'; ownerPlayerId = 'owner'
    matchId = 'match-1'; sourceCardId = 'ed_005'; targetInstanceId = 'unit-1'; returnedCardId = 'ed_004'
    playerFaction = 'end'; opponentFaction = 'end'; protocolVersion = 40; rulesetVersion = 'prototype-0.65'
    returnRevision = 10; recoveredRevision = 10; expiryRevision = 12; revision = 13; expiryCount = 1
    returnEventId = 20; expiryEventId = 24; returnEventOrderHash = ('A' * 64); expiryEventOrderHash = ('B' * 64)
    winnerPlayerId = 'observer'; matchStatus = 'FINISHED'; returnedHandCardInstanceId = 'returned-1'
    costModifier = -2; discountedCost = 1; baseCost = 3
    playerLife = 30; opponentLife = 30; playerUnitCount = 0; opponentUnitCount = 0
    causalOrderVerified = $true; performedUiCardPlay = $true; paymentVerified = $true
    privateProjectionVerified = $true; boardRemovalVerified = $true
    reconnectRecovered = $true; recoveryStateVerified = $true
    discountUiVerified = $true; expiredUiVerified = $true; expiryStateVerified = $true
}
$observer = Copy-Probe $owner
$observer.role = 'observer'
$observer.viewerPlayerId = 'observer'
$observer.returnedHandCardInstanceId = $null
$observer.costModifier = 0
$observer.discountedCost = 0
$observer.baseCost = 0
$observer.paymentVerified = $false
$observer.performedUiCardPlay = $false
$observer.discountUiVerified = $false
$observer.expiredUiVerified = $false
& $assertPath -ProbeA $owner -ProbeB $observer -ReturnCardId ed_005
& $assertPath -ProbeA $observer -ProbeB $owner -ReturnCardId ed_005
$negativeControls = @(
    @{ target = 'owner'; field = 'performedUiCardPlay'; value = $false },
    @{ target = 'owner'; field = 'paymentVerified'; value = $false },
    @{ target = 'owner'; field = 'discountUiVerified'; value = $false },
    @{ target = 'owner'; field = 'expiredUiVerified'; value = $false },
    @{ target = 'owner'; field = 'costModifier'; value = -1 },
    @{ target = 'owner'; field = 'expiryCount'; value = 2 },
    @{ target = 'owner'; field = 'recoveredRevision'; value = 11 },
    @{ target = 'observer'; field = 'returnedHandCardInstanceId'; value = 'leaked' },
    @{ target = 'observer'; field = 'costModifier'; value = -2 },
    @{ target = 'observer'; field = 'returnEventOrderHash'; value = ('C' * 64) },
    @{ target = 'observer'; field = 'expiryEventOrderHash'; value = ('C' * 64) },
    @{ target = 'observer'; field = 'matchId'; value = 'different-match' },
    @{ target = 'observer'; field = 'playerUnitCount'; value = 1 },
    @{ target = 'observer'; field = 'reconnectRecovered'; value = $false }
)
foreach ($control in $negativeControls) {
    $a = Copy-Probe $owner
    $b = Copy-Probe $observer
    if ($control.target -eq 'owner') { $a.($control.field) = $control.value }
    else { $b.($control.field) = $control.value }
    $rejected = $false
    try { & $assertPath -ProbeA $a -ProbeB $b -ReturnCardId ed_005 }
    catch { $rejected = $true }
    if (-not $rejected) { throw "Report negative control was accepted: $($control.target)/$($control.field)" }
}
Write-Output "Online return report validator: 2 positive / $($negativeControls.Count) negative controls passed."
