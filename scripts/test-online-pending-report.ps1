$ErrorActionPreference = 'Stop'
$assertPath = Join-Path $PSScriptRoot 'assert-online-pending-report.ps1'
function Copy-Probe($value) { $value | ConvertTo-Json -Depth 5 | ConvertFrom-Json }
$a = [pscustomobject]@{
    ok=$true; pendingUiVerified=$true; actualAcknowledgementVerified=$true; privateProjectionVerified=$true
    matchId='match'; viewerPlayerId='a'; protocolVersion=40; rulesetVersion='prototype-0.65'; matchStatus='FINISHED'; winnerPlayerId='b'
    pendingCommandId=('online-' + ('a' * 32)); pendingRevision=6; ackRevision=7; revision=9; heldResponseCount=2; pendingHandCount=7
    pendingHandInstanceIds=@(1..7 | ForEach-Object { "hand-$_" })
}
$b = Copy-Probe $a
$b.viewerPlayerId='b'; $b.pendingCommandId=('online-' + ('b' * 32)); $b.pendingRevision=7; $b.ackRevision=8
$b.pendingHandInstanceIds=@(8..14 | ForEach-Object { "hand-$_" })
& $assertPath -ProbeA $a -ProbeB $b
& $assertPath -ProbeA $b -ProbeB $a
$controls = @(
    @{field='ok';value=$false}, @{field='pendingUiVerified';value=$false}, @{field='pendingUiVerified';value='true'},
    @{field='actualAcknowledgementVerified';value=$false}, @{field='privateProjectionVerified';value=$false},
    @{field='pendingHandCount';value=6}, @{field='pendingHandCount';value='7'},
    @{field='heldResponseCount';value=0}, @{field='heldResponseCount';value=65}, @{field='heldResponseCount';value='2'},
    @{field='pendingRevision';value=0}, @{field='ackRevision';value=9}, @{field='revision';value=8},
    @{field='pendingCommandId';value=$a.pendingCommandId}, @{field='pendingCommandId';value='invented'},
    @{field='pendingHandInstanceIds';value=@('hand-8','hand-8','hand-9','hand-10','hand-11','hand-12','hand-13')},
    @{field='pendingHandInstanceIds';value=@('hand-0','hand-9','hand-10','hand-11','hand-12','hand-13','hand-14')},
    @{field='pendingHandInstanceIds';value=@('hand-1','hand-9','hand-10','hand-11','hand-12','hand-13','hand-14')},
    @{field='viewerPlayerId';value='a'}, @{field='viewerPlayerId';value=$null}, @{field='matchId';value='other'},
    @{field='protocolVersion';value=39}, @{field='rulesetVersion';value='old'}, @{field='matchStatus';value='ACTIVE'},
    @{field='winnerPlayerId';value=$null}, @{field='winnerPlayerId';value='other'}, @{field='protocolVersion';value='40'}
)
foreach ($control in $controls) {
    $changed=Copy-Probe $b
    $changed.($control.field)=$control.value
    $rejected=$false
    try { & $assertPath -ProbeA $a -ProbeB $changed } catch { $rejected=$true }
    if (-not $rejected) { throw "Pending report accepted negative control: $($control.field)" }
}
"Pending report validator: 2 positive / $($controls.Count) negative controls passed."
