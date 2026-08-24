[CmdletBinding()]
param(
    [string]$UnityPath,
    [switch]$WithWindowsPlayer,
    [switch]$WithMinecraftAssets
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'client-unity'
$versionFile = Join-Path $projectPath 'ProjectSettings\ProjectVersion.txt'
$requiredVersion = ((Get-Content -LiteralPath $versionFile | Select-String '^m_EditorVersion:\s*(.+)$').Matches[0].Groups[1].Value).Trim()

if (-not $UnityPath) {
    $UnityPath = @(& (Join-Path $PSScriptRoot 'find-unity.ps1')) | Where-Object {
        (Get-Item -LiteralPath $_).VersionInfo.ProductVersion.StartsWith($requiredVersion, [System.StringComparison]::Ordinal)
    } | Select-Object -First 1
}
if (-not $UnityPath -or -not (Test-Path -LiteralPath $UnityPath)) { throw "Unity $requiredVersion was not found." }

& (Join-Path $PSScriptRoot 'sync-card-content.ps1')
if ($WithMinecraftAssets) {
    & (Join-Path $PSScriptRoot 'extract-minecraft-card-icons.ps1')
    & (Join-Path $PSScriptRoot 'extract-minecraft-world-textures.ps1')
}
[System.IO.Directory]::CreateDirectory((Join-Path $projectPath 'Logs')) | Out-Null

function Invoke-DemoUnity([string]$Method, [string]$LogName, [int]$TimeoutSeconds) {
    $logPath = Join-Path $projectPath ("Logs\" + $LogName)
    $arguments = @('-batchmode','-nographics','-quit','-projectPath',$projectPath,'-executeMethod',$Method,'-logFile',$logPath)
    $process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
        throw "$Method timed out. See $logPath"
    }
    if ($process.ExitCode -ne 0) { throw "$Method failed with exit code $($process.ExitCode). See $logPath" }
}

Invoke-DemoUnity 'BiomeRivals.Demo.Editor.DemoSceneBuilder.BuildFromCommandLine' 'demo-build-scene.log' 300
Write-Output 'Demo scene generated: client-unity/Assets/Game/Demo/Scenes/Demo.unity'
if ($WithWindowsPlayer) {
    Invoke-DemoUnity 'BiomeRivals.Demo.Editor.DemoBuildAutomation.BuildWindowsFromCommandLine' 'demo-windows-build.log' 900
    Write-Output 'Windows demo generated: client-unity/Builds/DemoPreview/BiomeRivalsDemo.exe'
}
