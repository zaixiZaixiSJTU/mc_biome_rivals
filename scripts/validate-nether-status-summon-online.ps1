[CmdletBinding()]
param(
    [int]$TimeoutSeconds = 45,
    [int]$MaximumAttempts = 24
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$reportPath = Join-Path $repoRoot 'artifacts\nether-status-summon-online-probe.json'
[System.IO.Directory]::CreateDirectory((Split-Path -Parent $reportPath)) | Out-Null

$savedTimeout = [Environment]::GetEnvironmentVariable('BIOME_RIVALS_SMOKE_TIMEOUT_MS', 'Process')
$savedAttempts = [Environment]::GetEnvironmentVariable('BIOME_RIVALS_NETHER_STATUS_PROBE_ATTEMPTS', 'Process')
$savedReport = [Environment]::GetEnvironmentVariable('BIOME_RIVALS_NETHER_STATUS_PROBE_REPORT', 'Process')
try {
    [Environment]::SetEnvironmentVariable('BIOME_RIVALS_SMOKE_TIMEOUT_MS', [string]($TimeoutSeconds * 1000), 'Process')
    [Environment]::SetEnvironmentVariable('BIOME_RIVALS_NETHER_STATUS_PROBE_ATTEMPTS', [string]$MaximumAttempts, 'Process')
    [Environment]::SetEnvironmentVariable('BIOME_RIVALS_NETHER_STATUS_PROBE_REPORT', $reportPath, 'Process')
    Push-Location (Join-Path $repoRoot 'server-nakama')
    try {
        npm run smoke:nether-status-summon
        if ($LASTEXITCODE -ne 0) { throw "Nether status/summon online probe failed with exit code $LASTEXITCODE." }
    }
    finally {
        Pop-Location
    }

    $report = Get-Content -LiteralPath $reportPath -Raw -Encoding utf8 | ConvertFrom-Json
    if (-not $report.ok) { throw "Nether status/summon online probe reported failure: $($report.error)" }
    if ($report.strider.cleanseRevision -le $report.strider.fireRevision) {
        throw 'Strider cleanse did not follow the authoritative Fire application.'
    }
    if ($report.witherFortress.reconnectRevisions.Count -ne 2 -or
        $report.witherFortress.reconnectRevisions[0] -ne $report.witherFortress.checkpointRevision -or
        $report.witherFortress.reconnectRevisions[1] -ne $report.witherFortress.checkpointRevision) {
        throw 'Both reconnect snapshots must preserve the Fortress checkpoint revision.'
    }
    if ($report.witherFortress.recoveredWither.remainingDuration -ne 2 -or
        $report.witherFortress.recoveredWither.sourceCardId -ne 'nt_005' -or
        $report.witherFortress.recoveredWither.effectId -ne 'effect.nt_005.01') {
        throw 'Reconnect snapshot did not preserve the WITHER duration and source.'
    }
    if ($report.witherFortress.tickRevision -le $report.witherFortress.checkpointRevision) {
        throw 'Post-reconnect WITHER did not continue from the checkpoint.'
    }
    Write-Output "Nether status/summon online validation passed: Strider $($report.strider.matchId), WITHER/Fortress $($report.witherFortress.matchId), checkpoint revision $($report.witherFortress.checkpointRevision), tick revision $($report.witherFortress.tickRevision)."
}
finally {
    [Environment]::SetEnvironmentVariable('BIOME_RIVALS_SMOKE_TIMEOUT_MS', $savedTimeout, 'Process')
    [Environment]::SetEnvironmentVariable('BIOME_RIVALS_NETHER_STATUS_PROBE_ATTEMPTS', $savedAttempts, 'Process')
    [Environment]::SetEnvironmentVariable('BIOME_RIVALS_NETHER_STATUS_PROBE_REPORT', $savedReport, 'Process')
}
