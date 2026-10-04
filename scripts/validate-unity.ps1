[CmdletBinding()]
param(
    [string]$UnityPath,
    [string]$UnityCliPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'client-unity'
$versionFile = Join-Path $projectPath 'ProjectSettings\ProjectVersion.txt'
$logsPath = Join-Path $projectPath 'Logs'
[System.IO.Directory]::CreateDirectory($logsPath) | Out-Null

$versionLine = Get-Content -LiteralPath $versionFile | Where-Object { $_ -match '^m_EditorVersion:\s*(.+)$' } | Select-Object -First 1
if (-not $versionLine -or $versionLine -notmatch '^m_EditorVersion:\s*(.+)$') { throw "Cannot read Unity project version: $versionFile" }
$requiredVersion = $Matches[1].Trim()

if (-not $UnityPath) {
    $candidates = @(& (Join-Path $PSScriptRoot 'find-unity.ps1'))
    $UnityPath = $candidates | Where-Object {
        (Get-Item -LiteralPath $_).VersionInfo.ProductVersion.StartsWith($requiredVersion, [System.StringComparison]::Ordinal)
    } | Select-Object -First 1
}
if (-not $UnityPath -or -not (Test-Path -LiteralPath $UnityPath)) { throw "Unity $requiredVersion was not found." }
$actualVersion = (Get-Item -LiteralPath $UnityPath).VersionInfo.ProductVersion
if (-not $actualVersion.StartsWith($requiredVersion, [System.StringComparison]::Ordinal)) {
    throw "Unity version mismatch. Project requires $requiredVersion; executable is $actualVersion."
}

$cliCandidates = @()
if ($UnityCliPath) {
    $cliCandidates += $UnityCliPath
}
else {
    $cliCommand = Get-Command 'unity' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($cliCommand) { $cliCandidates += $cliCommand.Source }
    if ($env:LOCALAPPDATA) {
        $cliCandidates += Join-Path $env:LOCALAPPDATA 'Unity\bin\unity.exe'
        $cliCandidates += Join-Path $env:LOCALAPPDATA 'Programs\Unity Hub\resources\cli\unity.exe'
    }
}

$UnityCliPath = $cliCandidates |
    Where-Object { $_ -and (Test-Path -LiteralPath $_) } |
    Select-Object -First 1
if (-not $UnityCliPath) {
    throw 'Unity CLI was not found. Install/update Unity Hub or pass -UnityCliPath. No generated card content was touched.'
}

$unityProcesses = Get-CimInstance Win32_Process | Where-Object {
    $_.Name -eq 'Unity.exe' -and $_.CommandLine -and
    $_.CommandLine.IndexOf($projectPath, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
}
if ($unityProcesses) {
    throw 'Close the Unity Editor using this project before validation. No generated card content was touched.'
}

$licenseOutput = & $UnityCliPath license status --json 2>$null
if ($LASTEXITCODE -ne 0) {
    throw 'Unity CLI could not read the active license. Sign in and activate an Editor license, then retry. No generated card content was touched.'
}
try {
    $licenseStatus = ($licenseOutput -join [Environment]::NewLine) | ConvertFrom-Json -ErrorAction Stop
}
catch {
    throw 'Unity CLI returned an unreadable license status. No generated card content was touched.'
}
if (-not $licenseStatus.success -or -not $licenseStatus.data.active) {
    throw 'Unity CLI reports no active Editor license. Activate an Editor license, then retry. No generated card content was touched.'
}

& (Join-Path $PSScriptRoot 'validate-card-content.ps1')
if (-not $?) { throw 'Card content drift validation failed.' }
& (Join-Path $PSScriptRoot 'sync-card-frame-study.ps1') -Check
if (-not $?) { throw 'Card-frame study drift validation failed.' }

$testResults = Join-Path $repoRoot 'Temp\RULE-033B-validation-editmode.xml'
[System.IO.Directory]::CreateDirectory((Split-Path -Parent $testResults)) | Out-Null
$testTimeoutSeconds = 600
& $UnityCliPath test $projectPath --mode EditMode --timeout $testTimeoutSeconds --editor-path $UnityPath --output $testResults
if ($LASTEXITCODE -ne 0) {
    throw "Unity CLI EditMode tests failed with exit code $LASTEXITCODE. See $testResults."
}

[xml]$results = Get-Content -LiteralPath $testResults -Raw
$run = $results.'test-run'
if ($run.result -ne 'Passed' -or [int]$run.failed -ne 0) {
    throw "Unity tests did not pass. Result=$($run.result), failed=$($run.failed)."
}
Write-Output "Unity validation passed with ${actualVersion}: $($run.passed)/$($run.total) EditMode tests."
