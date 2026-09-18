[CmdletBinding()]
param(
    [int]$TimeoutSeconds = 45,
    [int]$MaximumAttempts = 12
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$reportPath = Join-Path $repoRoot 'artifacts\nether-trigger-online-probe.json'
[System.IO.Directory]::CreateDirectory((Split-Path -Parent $reportPath)) | Out-Null
Remove-Item -LiteralPath $reportPath -Force -ErrorAction SilentlyContinue

$savedTimeout = [Environment]::GetEnvironmentVariable('BIOME_RIVALS_SMOKE_TIMEOUT_MS', 'Process')
$savedAttempts = [Environment]::GetEnvironmentVariable('BIOME_RIVALS_NETHER_PROBE_ATTEMPTS', 'Process')
$savedReport = [Environment]::GetEnvironmentVariable('BIOME_RIVALS_NETHER_PROBE_REPORT', 'Process')
try {
    [Environment]::SetEnvironmentVariable('BIOME_RIVALS_SMOKE_TIMEOUT_MS', [string]($TimeoutSeconds * 1000), 'Process')
    [Environment]::SetEnvironmentVariable('BIOME_RIVALS_NETHER_PROBE_ATTEMPTS', [string]$MaximumAttempts, 'Process')
    [Environment]::SetEnvironmentVariable('BIOME_RIVALS_NETHER_PROBE_REPORT', $reportPath, 'Process')
    Push-Location (Join-Path $repoRoot 'server-nakama')
    try {
        npm run smoke:nether-trigger
        if ($LASTEXITCODE -ne 0) { throw "Nether trigger online probe failed with exit code $LASTEXITCODE." }
    }
    finally {
        Pop-Location
    }
    $report = Get-Content -LiteralPath $reportPath -Raw -Encoding utf8 | ConvertFrom-Json
    if (-not $report.ok) { throw "Nether trigger online probe reported failure: $($report.error)" }
    if ($report.triggerRevision -ne $report.reconnectRevision) {
        throw 'Reconnect snapshot did not preserve the trigger revision.'
    }
    if ($report.anchorCount -lt 2 -or $report.recoveredTemporaryRedstone -ne $report.anchorCount -or
        $report.recoveredPiglinAttack -ne 3 -or
        $report.recoveredPiglinHealth -ne 3) {
        throw 'Reconnect snapshot did not preserve the expected trigger state.'
    }
    Write-Output "Nether trigger online validation passed: $($report.matchId), trigger revision $($report.triggerRevision), final revision $($report.finalRevision)."
}
finally {
    [Environment]::SetEnvironmentVariable('BIOME_RIVALS_SMOKE_TIMEOUT_MS', $savedTimeout, 'Process')
    [Environment]::SetEnvironmentVariable('BIOME_RIVALS_NETHER_PROBE_ATTEMPTS', $savedAttempts, 'Process')
    [Environment]::SetEnvironmentVariable('BIOME_RIVALS_NETHER_PROBE_REPORT', $savedReport, 'Process')
}
