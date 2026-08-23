[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

function Require-Command([string]$Name, [string]$InstallHint) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Missing command: $Name. $InstallHint"
    }
}

Require-Command 'node' 'Install Node.js 20 LTS.'
Require-Command 'npm' 'Install npm 10 with Node.js.'
Require-Command 'git' 'Install Git for Windows.'

$nodeMajor = [int]((node --version).TrimStart('v').Split('.')[0])
if ($nodeMajor -ne 20) {
    throw "This repository requires Node.js 20; current version: $(node --version)."
}

Push-Location $repoRoot
try {
    if (Test-Path -LiteralPath 'package-lock.json') {
        npm ci
    }
    else {
        npm install
    }
    if ($LASTEXITCODE -ne 0) { throw 'npm dependency installation failed.' }

    if (Get-Command docker -ErrorAction SilentlyContinue) {
        Write-Host 'Docker CLI found. Docker Desktop engine availability will be checked when Compose starts.'
    }
    else {
        Write-Warning 'Docker CLI is missing. Server tests work, but local Nakama/PostgreSQL cannot start.'
    }

    & (Join-Path $PSScriptRoot 'find-unity.ps1')
    if ($LASTEXITCODE -ne 0) {
        Write-Warning 'Unity is required for client validation, but does not block server dependency setup.'
    }
    $global:LASTEXITCODE = 0
}
finally {
    Pop-Location
}
