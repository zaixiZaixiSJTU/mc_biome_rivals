[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$unityOutput = Join-Path $repoRoot 'client-unity\Assets\Game\Content\Resources\CardContent'
[System.IO.Directory]::CreateDirectory($unityOutput) | Out-Null

& (Join-Path $PSScriptRoot 'sync-card-name-registry.ps1')
& (Join-Path $PSScriptRoot 'sync-card-definition-registry.ps1')
& (Join-Path $PSScriptRoot 'sync-server-card-catalog.ps1')

$copies = [ordered]@{
    'shared-schema\card-data\localization\card-name-registry.zh-CN.v1.json' = 'card-name-registry.zh-CN.v1.json'
    'shared-schema\card-data\card-theme-registry.v1.json' = 'card-theme-registry.v1.json'
    'shared-schema\card-data\card-definition-registry.v1.json' = 'card-definition-registry.v1.json'
    'shared-schema\card-data\localization\card-text-registry.zh-CN.v1.json' = 'card-text-registry.zh-CN.v1.json'
}

foreach ($entry in $copies.GetEnumerator()) {
    $source = Join-Path $repoRoot $entry.Key
    $target = Join-Path $unityOutput $entry.Value
    if (-not (Test-Path -LiteralPath $source)) { throw "Content source not found: $source" }
    $content = [System.IO.File]::ReadAllText($source, [System.Text.Encoding]::UTF8)
    [System.IO.File]::WriteAllText($target, $content, [System.Text.UTF8Encoding]::new($false))
    Write-Output "Synced $($entry.Key) -> $target"
}
