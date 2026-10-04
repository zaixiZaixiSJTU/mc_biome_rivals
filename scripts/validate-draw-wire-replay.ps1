[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceReportPath,
    [Parameter(Mandatory)][string]$OutputReportPath,
    [Parameter(Mandatory)][string]$ProjectPath,
    [Parameter(Mandatory)][string]$UnityPath,
    [string]$UnityCliPath,
    [ValidateRange(1, 600)][int]$TimeoutSeconds = 240
)
$ErrorActionPreference = 'Stop'
if (-not $UnityCliPath) {
    $cliCommand = Get-Command unity -ErrorAction SilentlyContinue
    $UnityCliPath = if ($cliCommand) { $cliCommand.Source } else { Join-Path $env:LOCALAPPDATA 'Unity/bin/unity.exe' }
}
foreach ($path in @($SourceReportPath, $UnityPath, $UnityCliPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "File not found: $path" }
}
$SourceReportPath = [IO.Path]::GetFullPath($SourceReportPath)
$OutputReportPath = [IO.Path]::GetFullPath($OutputReportPath)
$ProjectPath = [IO.Path]::GetFullPath($ProjectPath)
if (Test-Path -LiteralPath $OutputReportPath) { throw "Refusing to overwrite replay report: $OutputReportPath" }
if (-not (Test-Path -LiteralPath (Join-Path $ProjectPath 'ProjectSettings/ProjectVersion.txt'))) { throw 'Invalid Unity project.' }
$versionLine = Get-Content -LiteralPath (Join-Path $ProjectPath 'ProjectSettings/ProjectVersion.txt') |
    Where-Object { $_ -match '^m_EditorVersion:\s*(.+)$' } | Select-Object -First 1
if (-not $versionLine -or $versionLine -notmatch '^m_EditorVersion:\s*(.+)$') { throw 'Unity project version missing.' }
$requiredVersion = $Matches[1].Trim()
if (-not (Get-Item -LiteralPath $UnityPath).VersionInfo.ProductVersion.StartsWith($requiredVersion)) {
    throw 'Editor does not match the Unity project version.'
}
$usingProject = Get-CimInstance Win32_Process | Where-Object {
    $_.Name -eq 'Unity.exe' -and $_.CommandLine -and
    $_.CommandLine.IndexOf($ProjectPath, [StringComparison]::OrdinalIgnoreCase) -ge 0
}
if ($usingProject) { throw 'Project is already open. Use a synchronized isolated project for this audit.' }
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($OutputReportPath)) | Out-Null
$sourceHash = (Get-FileHash -LiteralPath $SourceReportPath -Algorithm SHA256).Hash
& $UnityCliPath run $ProjectPath --editor-path $UnityPath --timeout $TimeoutSeconds -- -executeMethod BiomeRivals.Demo.Editor.DemoWireReplayAudit.ValidateDrawFromCommandLine -wireReplaySource $SourceReportPath -wireReplayResult $OutputReportPath
if ($LASTEXITCODE -ne 0) { throw "Unity wire replay failed with exit code $LASTEXITCODE. See $OutputReportPath" }
$result = Get-Content -LiteralPath $OutputReportPath -Raw -Encoding utf8 | ConvertFrom-Json
if (-not $result.ok -or $result.replayedClients -ne 2 -or $result.replayedMessages -lt 2 -or
    $result.sourceSha256 -ne $sourceHash -or
    (Get-FileHash -LiteralPath $SourceReportPath -Algorithm SHA256).Hash -ne $sourceHash) {
    throw 'Wire audit failed or source capture changed during replay.'
}
Write-Output "Unity draw wire replay passed: $($result.matchId), revision $($result.revision), $($result.replayedMessages) messages."
