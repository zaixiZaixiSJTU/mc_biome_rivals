[CmdletBinding()]
param(
    [string]$SourcePath = 'docs\design\assets\card-frame-theme-study-v1.png',
    [string]$TargetPath = 'client-unity\Assets\Game\Demo\Art\Resources\DemoCardFrames\card-frame-theme-study-v1.png'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$sourceFile = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $SourcePath))
$targetFile = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $TargetPath))
if (-not $sourceFile.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) { throw "Card-frame source must stay inside the repository: $sourceFile" }
if (-not $targetFile.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) { throw "Card-frame target must stay inside the repository: $targetFile" }
if (-not (Test-Path -LiteralPath $sourceFile)) { throw "Card-frame study not found: $sourceFile" }

$targetDirectory = Split-Path -Parent $targetFile
[System.IO.Directory]::CreateDirectory($targetDirectory) | Out-Null
$sourceHash = (Get-FileHash -LiteralPath $sourceFile -Algorithm SHA256).Hash
$targetHash = if (Test-Path -LiteralPath $targetFile) { (Get-FileHash -LiteralPath $targetFile -Algorithm SHA256).Hash } else { '' }
if ($sourceHash -ne $targetHash) { Copy-Item -LiteralPath $sourceFile -Destination $targetFile -Force }
Write-Output "Synced card-frame study -> $targetFile ($sourceHash)"
