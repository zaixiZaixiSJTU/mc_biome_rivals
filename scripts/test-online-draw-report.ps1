[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$assertPath = Join-Path $PSScriptRoot 'assert-online-draw-report.ps1'
function Copy-Probe($probe) { $probe | ConvertTo-Json -Depth 5 | ConvertFrom-Json }
$a = [pscustomobject]@{
    ok = $true; role = 'requester'; fixtureAcknowledged = $true
    matchId = 'draw-match'; viewerPlayerId = 'a'; protocolVersion = 40; rulesetVersion = 'prototype-0.65'
    matchStatus = 'FINISHED'; winnerPlayerId = $null; terminalReason = 'SIMULTANEOUS_DEFEAT'
    terminalEventCount = 1; terminalEventId = 8; publicEventOrderHash = ('A' * 64)
    openingRevision = 2; preparedRevision = 2; revision = 3
    playerLife = 0; opponentLife = 0; playerFaction = 'plains_forest'; opponentFaction = 'nether'
    privateProjectionVerified = $true; reconnectRecovered = $true
    uiVerifiedBeforeReconnect = $true; uiVerifiedAfterReconnect = $true
}
$b = Copy-Probe $a
$b.viewerPlayerId = 'b'; $b.role = 'observer'; $b.fixtureAcknowledged = $false
$b.playerFaction = 'nether'; $b.opponentFaction = 'plains_forest'
& $assertPath -ProbeA $a -ProbeB $b
$roleSwapA = Copy-Probe $a; $roleSwapB = Copy-Probe $b
$roleSwapA.role = 'observer'; $roleSwapA.fixtureAcknowledged = $false
$roleSwapB.role = 'requester'; $roleSwapB.fixtureAcknowledged = $true
& $assertPath -ProbeA $roleSwapA -ProbeB $roleSwapB
$controls = @(
    @{ field = 'ok'; value = $false },
    @{ field = 'matchStatus'; value = 'ACTIVE' },
    @{ field = 'winnerPlayerId'; value = 'b' },
    @{ field = 'terminalReason'; value = 'CONCEDED' },
    @{ field = 'terminalEventCount'; value = 2 },
    @{ field = 'terminalEventId'; value = 0 },
    @{ field = 'playerLife'; value = 1 },
    @{ field = 'opponentLife'; value = 1 },
    @{ field = 'openingRevision'; value = 0 },
    @{ field = 'preparedRevision'; value = 1 },
    @{ field = 'revision'; value = 4 },
    @{ field = 'publicEventOrderHash'; value = ('B' * 64) },
    @{ field = 'publicEventOrderHash'; value = 'invalid' },
    @{ field = 'matchId'; value = 'other-match' },
    @{ field = 'viewerPlayerId'; value = 'a' },
    @{ field = 'viewerPlayerId'; value = $null },
    @{ field = 'protocolVersion'; value = 39 },
    @{ field = 'rulesetVersion'; value = 'old' },
    @{ field = 'privateProjectionVerified'; value = $false },
    @{ field = 'reconnectRecovered'; value = $false },
    @{ field = 'uiVerifiedBeforeReconnect'; value = $false },
    @{ field = 'uiVerifiedAfterReconnect'; value = $false },
    @{ field = 'role'; value = 'requester' },
    @{ field = 'fixtureAcknowledged'; value = $true },
    @{ field = 'playerFaction'; value = 'end' },
    @{ field = 'opponentFaction'; value = 'end' }
)
foreach ($control in $controls) {
    $changed = Copy-Probe $b
    $changed.($control.field) = $control.value
    $rejected = $false
    try { & $assertPath -ProbeA $a -ProbeB $changed }
    catch { $rejected = $true }
    if (-not $rejected) { throw "Draw report negative control accepted: $($control.field)" }
}
Write-Output "Online draw report validator: 2 positive / $($controls.Count) negative controls passed."
$readableA = Copy-Probe $a
$readableB = Copy-Probe $b
foreach ($probe in @($readableA,$readableB)) {
    $probe | Add-Member readableHand $true
    $probe | Add-Member playerHandCount 7
    $probe | Add-Member ownHandInstanceIds @((1..7) | ForEach-Object { "hand-$_" })
}
$readableB.ownHandInstanceIds = @((8..14) | ForEach-Object { "hand-$_" })
& $assertPath -ProbeA $readableA -ProbeB $readableB -RequireFullHandReadability
$readControls = @(
    @{ field = 'readableHand'; value = $false },
    @{ field = 'readableHand'; value = 'true' },
    @{ field = 'playerHandCount'; value = 6 },
    @{ field = 'playerHandCount'; value = '7' },
    @{ field = 'ownHandInstanceIds'; value = @((1..7) | ForEach-Object { "hand-$_" }) },
    @{ field = 'ownHandInstanceIds'; value = @() },
    @{ field = 'ownHandInstanceIds'; value = @('hand-1','hand-2','hand-3','hand-4','hand-5','hand-6','hand-6') },
    @{ field = 'ownHandInstanceIds'; value = @('hand-1','hand-2','hand-3','hand-4','hand-5','hand-6','invalid') }
)
foreach ($control in $readControls) {
    $changed = Copy-Probe $readableB
    $changed.($control.field) = $control.value
    $rejected = $false
    try { & $assertPath -ProbeA $readableA -ProbeB $changed -RequireFullHandReadability } catch { $rejected = $true }
    if (-not $rejected) { throw "Readable draw negative control accepted: $($control.field)" }
}
Write-Output "Full-hand draw validator: 1 positive / $($readControls.Count) negative controls passed."
