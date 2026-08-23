[CmdletBinding()]
param(
    [switch]$WithDockerConfig,
    [switch]$WithUnity
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    & (Join-Path $PSScriptRoot 'validate-card-content.ps1')
    npm run typecheck
    npm test
    npm run build
    if ($WithUnity) {
        & (Join-Path $PSScriptRoot 'validate-unity.ps1')
    }
    if ($WithDockerConfig) {
        docker compose config --quiet
    }
    Write-Host 'Validation passed.' -ForegroundColor Green
}
finally {
    Pop-Location
}
