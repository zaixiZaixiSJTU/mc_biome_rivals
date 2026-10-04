[CmdletBinding()]
param([Parameter(Mandatory)]$ProbeA, [Parameter(Mandatory)]$ProbeB)
$ErrorActionPreference = 'Stop'
foreach ($probe in @($ProbeA, $ProbeB)) {
    if ([string]::IsNullOrWhiteSpace($probe.matchId) -or [string]::IsNullOrWhiteSpace($probe.viewerPlayerId) -or
        $probe.protocolVersion -ne 40 -or $probe.rulesetVersion -cne 'prototype-0.65' -or
        $probe.matchStatus -cne 'FINISHED' -or [string]::IsNullOrWhiteSpace($probe.winnerPlayerId)) { throw 'Invalid pending match/version/result identity.' }
    foreach ($flag in @('ok','pendingUiVerified','actualAcknowledgementVerified','privateProjectionVerified')) {
        if ($probe.$flag -isnot [bool] -or -not $probe.$flag) { throw "Pending report has invalid $flag." }
    }
    foreach ($number in @('protocolVersion','pendingRevision','ackRevision','heldResponseCount','pendingHandCount','revision')) {
        if ($probe.$number -isnot [int] -and $probe.$number -isnot [long]) { throw "Pending report has noninteger $number." }
    }
    if ($probe.pendingHandCount -ne 7 -or $probe.heldResponseCount -lt 1 -or $probe.heldResponseCount -gt 64 -or
        $probe.pendingRevision -lt 2 -or $probe.ackRevision -ne ($probe.pendingRevision + 1) -or $probe.revision -le $probe.ackRevision) {
        throw 'Pending report has inconsistent response, revision, or full hand.'
    }
    if ($probe.pendingCommandId -isnot [string] -or $probe.pendingCommandId -cnotmatch '^online-[0-9a-f]{32}$') { throw 'Invalid real pending command id.' }
    if ($probe.pendingHandInstanceIds -isnot [array] -or $probe.pendingHandInstanceIds.Count -ne 7 -or
        @($probe.pendingHandInstanceIds | Select-Object -Unique).Count -ne 7) { throw 'Invalid pending private hand identities.' }
    foreach ($id in $probe.pendingHandInstanceIds) { if ($id -isnot [string] -or $id -cnotmatch '^hand-[1-9][0-9]*$') { throw 'Invalid pending hand instance.' } }
}
if ($ProbeA.matchId -ne $ProbeB.matchId -or $ProbeA.viewerPlayerId -eq $ProbeB.viewerPlayerId -or
    $ProbeA.pendingCommandId -eq $ProbeB.pendingCommandId -or $ProbeA.revision -ne $ProbeB.revision -or
    $ProbeA.winnerPlayerId -ne $ProbeB.winnerPlayerId -or $ProbeA.winnerPlayerId -notin @($ProbeA.viewerPlayerId,$ProbeB.viewerPlayerId) -or
    [Math]::Abs($ProbeA.pendingRevision - $ProbeB.pendingRevision) -ne 1) { throw 'Pending peers do not describe distinct successive real commands in one match.' }
foreach ($id in $ProbeA.pendingHandInstanceIds) { if ($id -in $ProbeB.pendingHandInstanceIds) { throw 'Pending private hands share global identities.' } }
