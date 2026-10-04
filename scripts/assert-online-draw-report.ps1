[CmdletBinding()]
param([Parameter(Mandatory)]$ProbeA, [Parameter(Mandatory)]$ProbeB, [switch]$RequireFullHandReadability)
$ErrorActionPreference = 'Stop'
foreach ($probe in @($ProbeA, $ProbeB)) {
    if (($probe.readableHand -eq $true) -ne [bool]$RequireFullHandReadability) { throw 'Draw readability mode mismatch.' }
    if ($RequireFullHandReadability -and ($probe.readableHand -isnot [bool] -or
        ($probe.playerHandCount -isnot [int] -and $probe.playerHandCount -isnot [long]) -or $probe.playerHandCount -ne 7 -or
        @($probe.ownHandInstanceIds).Count -ne 7 -or @($probe.ownHandInstanceIds | Select-Object -Unique).Count -ne 7 -or
        @($probe.ownHandInstanceIds | Where-Object { $_ -notmatch '^hand-[0-9]+$' }).Count -ne 0)) {
        throw 'Draw full-hand readability evidence is missing valid seven-instance private hands.'
    }
    if (-not $probe.ok -or $probe.matchStatus -ne 'FINISHED' -or $probe.winnerPlayerId -or
        $probe.terminalReason -ne 'SIMULTANEOUS_DEFEAT' -or $probe.terminalEventCount -ne 1 -or
        $probe.playerLife -ne 0 -or $probe.opponentLife -ne 0 -or
        $probe.openingRevision -le 0 -or $probe.preparedRevision -ne $probe.openingRevision -or
        $probe.revision -ne $probe.openingRevision + 1 -or $probe.terminalEventId -le 0 -or
        $probe.publicEventOrderHash -notmatch '^[0-9A-F]{64}$') { throw 'Invalid authoritative draw evidence.' }
    foreach ($flag in @('privateProjectionVerified','reconnectRecovered','uiVerifiedBeforeReconnect','uiVerifiedAfterReconnect')) {
        if ($probe.$flag -ne $true) { throw "Missing Unity draw evidence: $flag" }
    }
}
if ($RequireFullHandReadability -and @($ProbeA.ownHandInstanceIds | Where-Object { $_ -in $ProbeB.ownHandInstanceIds }).Count -ne 0) {
    throw 'Draw private hands cannot share globally unique instance ids.'
}
foreach ($field in @('matchId','revision','protocolVersion','rulesetVersion','openingRevision','preparedRevision',
    'terminalReason','terminalEventId','publicEventOrderHash')) {
    if (-not $ProbeA.$field -or $ProbeA.$field -ne $ProbeB.$field) { throw "Draw clients disagree on $field" }
}
if (-not $ProbeA.viewerPlayerId -or -not $ProbeB.viewerPlayerId -or $ProbeA.viewerPlayerId -eq $ProbeB.viewerPlayerId -or
    $ProbeA.playerFaction -ne 'plains_forest' -or $ProbeB.playerFaction -ne 'nether' -or
    $ProbeA.opponentFaction -ne $ProbeB.playerFaction -or $ProbeB.opponentFaction -ne $ProbeA.playerFaction) {
    throw 'Draw factions or player identities are incorrect.'
}
$requesters = @(@($ProbeA,$ProbeB) | Where-Object { $_.role -eq 'requester' })
$observers = @(@($ProbeA,$ProbeB) | Where-Object { $_.role -eq 'observer' })
if ($requesters.Count -ne 1 -or $observers.Count -ne 1 -or
    -not $requesters[0].fixtureAcknowledged -or $observers[0].fixtureAcknowledged) {
    throw 'Draw requester/observer fixture acknowledgements are incorrect.'
}
