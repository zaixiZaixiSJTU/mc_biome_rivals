[CmdletBinding()]
param(
    [string]$SourceMarkdown = 'docs\design\Minecraft_Biome_Rivals_Prototype_Cards_v0.1.md',
    [string]$OutputPath = 'shared-schema\card-data\localization\card-name-registry.zh-CN.v1.json'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $repoRoot $SourceMarkdown
$targetPath = Join-Path $repoRoot $OutputPath

if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw "Card design source not found: $sourcePath"
}

$entries = [System.Collections.Generic.List[object]]::new()
$seenIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$pattern = '^\|\s*(?<designId>(?:PF|DB|SI|CD|OR|NT|ED|TK)-\d{3})\s*\|\s*(?<name>[^|]+?)\s*\|'

foreach ($line in Get-Content -LiteralPath $sourcePath -Encoding UTF8) {
    $match = [regex]::Match($line, $pattern)
    if (-not $match.Success) { continue }

    $designId = $match.Groups['designId'].Value
    $cardId = $designId.ToLowerInvariant().Replace('-', '_')
    if (-not $seenIds.Add($cardId)) {
        throw "Duplicate card id after normalization: $cardId"
    }

    $entries.Add([ordered]@{
        id = $cardId
        designId = $designId
        nameKey = "card.$cardId.name"
        name = $match.Groups['name'].Value.Trim()
        collectible = -not $designId.StartsWith('TK-', [System.StringComparison]::Ordinal)
    })
}

if ($entries.Count -ne 74) {
    throw "Expected 74 registered names (56 collectible + 18 token), found $($entries.Count)."
}

$document = [ordered]@{
    schemaVersion = 1
    locale = 'zh-CN'
    source = $SourceMarkdown.Replace('\', '/')
    entries = $entries
}

$targetDirectory = Split-Path -Parent $targetPath
[System.IO.Directory]::CreateDirectory($targetDirectory) | Out-Null
$json = $document | ConvertTo-Json -Depth 6
[System.IO.File]::WriteAllText($targetPath, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
Write-Output "Registered $($entries.Count) card names -> $targetPath"
