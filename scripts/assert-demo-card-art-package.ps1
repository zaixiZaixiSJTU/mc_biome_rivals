[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ExecutablePath,
    [Parameter(Mandatory)][string]$ProjectPath
)
$ErrorActionPreference = 'Stop'
$manifest = & (Join-Path $PSScriptRoot 'assert-demo-player-source.ps1') -ExecutablePath $ExecutablePath -ProjectPath $ProjectPath
$registry = Get-Content -LiteralPath (Join-Path $ProjectPath 'Assets\Game\Content\Resources\CardContent\card-name-registry.zh-CN.v1.json') -Raw | ConvertFrom-Json
$sourceRoot = Join-Path $ProjectPath 'Assets\Generated\MinecraftCardIcons'
$provenance = Get-Content -LiteralPath (Join-Path $sourceRoot 'asset-provenance.local.json') -Raw | ConvertFrom-Json
$expected = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($entry in $registry.entries) { if (-not $expected.Add([string]$entry.id)) { throw 'Duplicate registered card name ID.' } }
if ($expected.Count -eq 0 -or @($manifest.packagedCardArt).Count -ne $expected.Count) { throw 'Player card art package metadata is missing or incomplete.' }
$sourceHashes = @{}
foreach ($entry in $provenance.entries) {
    if (-not $expected.Contains([string]$entry.cardId) -or $sourceHashes.ContainsKey([string]$entry.cardId) -or
        [string]$entry.outputFile -cne ([string]$entry.cardId + '.png') -or [string]$entry.sha256 -notmatch '^[0-9A-Fa-f]{64}$') { throw 'Invalid card art provenance entry.' }
    $sourceHashes[[string]$entry.cardId] = [string]$entry.sha256
}
if ($sourceHashes.Count -ne $expected.Count) { throw 'Card art provenance is incomplete.' }
$playerRoot = Split-Path -Parent ([System.IO.Path]::GetFullPath($ExecutablePath))
$seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($icon in $manifest.packagedCardArt) {
    if ([string]$icon.path -cnotmatch '^MinecraftCardIcons/([a-z]{2}_[0-9]{3})\.png$') { throw 'Unsafe or unexpected Player card art package path.' }
    $id = $Matches[1]
    if (-not $expected.Contains($id) -or -not $seen.Add($id) -or [string]$icon.sha256 -ine $sourceHashes[$id]) { throw "Unknown/duplicate/mismatched Player card art entry: $id" }
    $sourcePath = Join-Path $sourceRoot ($id + '.png')
    $packagePath = Join-Path $playerRoot ([string]$icon.path)
    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) { throw "Packaged Minecraft card art is missing: $id" }
    if ((Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash -ine $sourceHashes[$id] -or
        (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash -ine $sourceHashes[$id]) { throw "Packaged/source Minecraft card art hash mismatch: $id" }
}
if (-not $seen.SetEquals($expected) -or @(Get-ChildItem -LiteralPath (Join-Path $playerRoot 'MinecraftCardIcons') -File -Filter '*.png').Count -ne $expected.Count) {
    throw 'Player icon directory does not exactly cover the registry.'
}
[pscustomobject]@{ ok = $true; registeredIcons = $expected.Count; sourceFileCount = $manifest.inputs.Count; developmentBuild = $manifest.developmentBuild }
