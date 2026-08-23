[CmdletBinding()]
param(
    [string]$UnityPath
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

& (Join-Path $PSScriptRoot 'sync-card-content.ps1')
if (-not $?) { throw 'Card content sync failed.' }

function Invoke-Unity(
    [string[]]$Arguments,
    [string]$Operation,
    [int]$TimeoutSeconds,
    [string]$CompletionFile = '',
    [string]$CompletionPattern = '') {
    $process = Start-Process -FilePath $UnityPath -ArgumentList $Arguments -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        $completed = $false
        if ($CompletionFile -and (Test-Path -LiteralPath $CompletionFile)) {
            try {
                $stream = [System.IO.File]::Open($CompletionFile, 'Open', 'Read', 'ReadWrite')
                try {
                    $reader = [System.IO.StreamReader]::new($stream)
                    $completed = $reader.ReadToEnd() -match $CompletionPattern
                    $reader.Dispose()
                }
                finally { $stream.Dispose() }
            }
            catch { $completed = $false }
        }
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
        if ($completed) {
            Write-Warning "$Operation completed successfully but Unity did not terminate within $TimeoutSeconds seconds; the exact batch process was stopped."
            return
        }
        throw "$Operation timed out after $TimeoutSeconds seconds. See the Unity log for details."
    }
    if ($process.ExitCode -ne 0) { throw "$Operation failed with Unity exit code $($process.ExitCode)." }
}

$importLog = Join-Path $logsPath 'batch-import.log'
Invoke-Unity @('-batchmode','-nographics','-quit','-projectPath',$projectPath,'-logFile',$importLog) 'Unity compilation' 600

$testLog = Join-Path $logsPath 'editmode-tests.log'
$testResults = Join-Path $logsPath 'editmode-results.xml'
Invoke-Unity @('-batchmode','-nographics','-projectPath',$projectPath,'-runTests','-testPlatform','EditMode','-testResults',$testResults,'-logFile',$testLog) 'Unity EditMode tests' 60 $testLog 'Test run completed\. Exiting with code 0'

[xml]$results = Get-Content -LiteralPath $testResults -Raw
$run = $results.'test-run'
if ($run.result -ne 'Passed' -or [int]$run.failed -ne 0) {
    throw "Unity tests did not pass. Result=$($run.result), failed=$($run.failed)."
}
Write-Output "Unity validation passed with ${actualVersion}: $($run.passed)/$($run.total) EditMode tests."
