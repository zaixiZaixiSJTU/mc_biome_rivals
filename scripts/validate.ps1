[CmdletBinding()]
param(
    [switch]$WithDockerConfig,
    [switch]$WithUnity
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

function Invoke-ValidationStage {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Command,
        [switch]$Native
    )

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        & $Command
        if ($Native) {
            $exitCode = $LASTEXITCODE
            if ($exitCode -ne 0) { throw "Native command exited with code $exitCode." }
        }
        $stopwatch.Stop()
        Write-Host ("[PASS] {0} ({1:0.0}s)" -f $Name, $stopwatch.Elapsed.TotalSeconds) -ForegroundColor Green
    }
    catch {
        $stopwatch.Stop()
        throw ("[FAIL] {0} after {1:0.0}s: {2}" -f $Name, $stopwatch.Elapsed.TotalSeconds, $_.Exception.Message)
    }
}

$totalStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
Push-Location $repoRoot
try {
    Invoke-ValidationStage -Name 'Card content' -Command { & (Join-Path $PSScriptRoot 'validate-card-content.ps1') }
    Invoke-ValidationStage -Name 'Extracted Minecraft asset provenance control' -Command { & (Join-Path $PSScriptRoot 'test-extracted-minecraft-assets.ps1') }
    Invoke-ValidationStage -Name 'Card-frame copy drift control' -Command { & (Join-Path $PSScriptRoot 'test-card-frame-sync.ps1') }
    Invoke-ValidationStage -Name 'Player feedback reason contract' -Command { & (Join-Path $PSScriptRoot 'test-player-feedback-contract.ps1') }
    Invoke-ValidationStage -Name 'Online return report positive/negative controls' -Command { & (Join-Path $PSScriptRoot 'test-online-return-report.ps1') }
    Invoke-ValidationStage -Name 'Online draw report positive/negative controls' -Command { & (Join-Path $PSScriptRoot 'test-online-draw-report.ps1') }
    Invoke-ValidationStage -Name 'Online pending report positive/negative controls' -Command { & (Join-Path $PSScriptRoot 'test-online-pending-report.ps1') }
    Invoke-ValidationStage -Name 'Online arena report positive/negative controls' -Command { & (Join-Path $PSScriptRoot 'test-online-arena-report.ps1') }
    Invoke-ValidationStage -Name 'Online building report positive/negative controls' -Command { & (Join-Path $PSScriptRoot 'test-online-building-report.ps1') }
    Invoke-ValidationStage -Name 'Online goat report positive/negative controls' -Command { & (Join-Path $PSScriptRoot 'test-online-goat-report.ps1') }
    Invoke-ValidationStage -Name 'Online deployment rejection positive/negative controls' -Command { & (Join-Path $PSScriptRoot 'test-online-deployment-rejections.ps1') }
    Invoke-ValidationStage -Name 'Server typecheck' -Command { npm run typecheck } -Native
    Invoke-ValidationStage -Name 'Server tests' -Command { npm test } -Native
    Invoke-ValidationStage -Name 'Server build' -Command { npm run build } -Native
    if ($WithUnity) {
        Invoke-ValidationStage -Name 'Unity EditMode' -Command { & (Join-Path $PSScriptRoot 'validate-unity.ps1') }
    }
    if ($WithDockerConfig) {
        Invoke-ValidationStage -Name 'Docker Compose config' -Command { docker compose config --quiet } -Native
    }
    $totalStopwatch.Stop()
    Write-Host ("Validation passed in {0:0.0}s." -f $totalStopwatch.Elapsed.TotalSeconds) -ForegroundColor Green
}
finally {
    Pop-Location
}
