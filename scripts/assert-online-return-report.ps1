[CmdletBinding()]
param(
    [Parameter(Mandatory)]$ProbeA,
    [Parameter(Mandatory)]$ProbeB,
    [Parameter(Mandatory)][ValidateSet('ed_002','ed_005')][string]$ReturnCardId
)
$ErrorActionPreference = 'Stop'
$probes = @($ProbeA, $ProbeB)
$owners = @($probes | Where-Object { $_.role -eq 'owner' })
$observers = @($probes | Where-Object { $_.role -eq 'observer' })
if ($owners.Count -ne 1 -or $observers.Count -ne 1) { throw 'Return reports must contain one owner and one observer.' }
$owner = $owners[0]
$observer = $observers[0]
foreach ($probe in $probes) {
    if (-not $probe.ok -or $probe.playerFaction -ne 'end' -or $probe.opponentFaction -ne 'end' -or
        $probe.sourceCardId -ne $ReturnCardId -or $probe.ownerPlayerId -ne $owner.viewerPlayerId) {
        throw 'Return report card, identities, factions, or success flag are incorrect.'
    }
    foreach ($flag in @('causalOrderVerified','privateProjectionVerified','boardRemovalVerified',
        'reconnectRecovered','recoveryStateVerified','expiryStateVerified')) {
        if ($probe.$flag -ne $true) { throw "Return proof is missing: $($probe.role)/$flag" }
    }
    if ($probe.returnRevision -le 0 -or $probe.recoveredRevision -ne $probe.returnRevision -or
        $probe.expiryRevision -le $probe.returnRevision -or $probe.revision -le $probe.expiryRevision -or
        $probe.expiryCount -ne 1 -or $probe.returnEventId -le 0 -or $probe.expiryEventId -le $probe.returnEventId) {
        throw 'Return/recovery/expiry/final revision or event sequence is invalid.'
    }
    if ($probe.protocolVersion -le 0 -or -not $probe.rulesetVersion) { throw 'Return protocol identity is missing.' }
}
foreach ($field in @('matchId','sourceCardId','ownerPlayerId','targetInstanceId','returnedCardId','protocolVersion',
    'rulesetVersion','returnRevision','recoveredRevision','expiryRevision','returnEventId','expiryEventId',
    'returnEventOrderHash','expiryEventOrderHash','revision','winnerPlayerId','matchStatus')) {
    if (-not $owner.$field -or $owner.$field -ne $observer.$field) { throw "Return clients disagree on $field" }
}
if ($owner.viewerPlayerId -eq $observer.viewerPlayerId -or $owner.winnerPlayerId -ne $observer.viewerPlayerId -or
    $owner.matchStatus -ne 'FINISHED') { throw 'Return scenario did not finish by owner concession.' }
if ($owner.returnEventOrderHash -notmatch '^[0-9A-F]{64}$' -or $owner.expiryEventOrderHash -notmatch '^[0-9A-F]{64}$') {
    throw 'Public event order hashes are malformed.'
}
$expectedModifier = if ($ReturnCardId -eq 'ed_002') { -1 } else { -2 }
if (-not $owner.performedUiCardPlay -or -not $owner.paymentVerified -or -not $owner.discountUiVerified -or -not $owner.expiredUiVerified -or
    -not $owner.returnedHandCardInstanceId -or $owner.costModifier -ne $expectedModifier -or
    $owner.baseCost -le 0 -or $owner.discountedCost -ne [Math]::Max(0, $owner.baseCost + $expectedModifier)) {
    throw 'Owner payment, precise instance discount, or actual Unity card UI proof is missing.'
}
if ($observer.returnedHandCardInstanceId -or $observer.costModifier -ne 0 -or $observer.discountedCost -ne 0 -or
    $observer.baseCost -ne 0 -or $observer.discountUiVerified -or $observer.expiredUiVerified -or $observer.paymentVerified -or $observer.performedUiCardPlay) {
    throw 'Observer report contains private return identity, fee, or owner-only assertions.'
}
if ($owner.playerLife -ne $observer.opponentLife -or $owner.opponentLife -ne $observer.playerLife -or
    $owner.playerUnitCount -ne $observer.opponentUnitCount -or $owner.opponentUnitCount -ne $observer.playerUnitCount) {
    throw 'Return clients disagree on final public board or life.'
}
