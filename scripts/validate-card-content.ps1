[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$namesPath = Join-Path $repoRoot 'shared-schema\card-data\localization\card-name-registry.zh-CN.v1.json'
$themesPath = Join-Path $repoRoot 'shared-schema\card-data\card-theme-registry.v1.json'
$artPath = Join-Path $repoRoot 'shared-schema\card-art\card-art-registry.v1.json'
$definitionsPath = Join-Path $repoRoot 'shared-schema\card-data\card-definition-registry.v1.json'
$textsPath = Join-Path $repoRoot 'shared-schema\card-data\localization\card-text-registry.zh-CN.v1.json'
$implementedEffectsPath = Join-Path $repoRoot 'shared-schema\card-data\implemented-effect-registry.v1.json'
$markdownPath = Join-Path $repoRoot 'docs\design\Minecraft_Biome_Rivals_Prototype_Cards_v0.1.md'

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Required content file not found: $Path" }
    return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Convert-HexToRgb([string]$Hex) {
    if ($Hex -notmatch '^#[0-9A-Fa-f]{6}$') { throw "Invalid color: $Hex" }
    [double[]]$rgb = @(
        ([Convert]::ToInt32($Hex.Substring(1, 2), 16) / 255.0)
        ([Convert]::ToInt32($Hex.Substring(3, 2), 16) / 255.0)
        ([Convert]::ToInt32($Hex.Substring(5, 2), 16) / 255.0)
    )
    return $rgb
}

function Get-Luminance([string]$Hex) {
    $rgb = Convert-HexToRgb $Hex
    $linear = foreach ($value in $rgb) {
        if ($value -le 0.03928) { $value / 12.92 }
        else { [Math]::Pow(($value + 0.055) / 1.055, 2.4) }
    }
    return 0.2126 * $linear[0] + 0.7152 * $linear[1] + 0.0722 * $linear[2]
}

function Get-Contrast([string]$A, [string]$B) {
    $la = Get-Luminance $A
    $lb = Get-Luminance $B
    $lighter = [Math]::Max($la, $lb)
    $darker = [Math]::Min($la, $lb)
    return ($lighter + 0.05) / ($darker + 0.05)
}

$names = Read-Json $namesPath
$themes = Read-Json $themesPath
$art = Read-Json $artPath
$definitions = Read-Json $definitionsPath
$texts = Read-Json $textsPath
$implementedEffects = Read-Json $implementedEffectsPath
if ($implementedEffects.schemaVersion -ne 1 -or $implementedEffects.contentVersion -lt 1) { throw 'Implemented effect registry version is invalid.' }
$implementedEffectIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($effectId in $implementedEffects.implementedEffectIds) {
    if ([string]$effectId -notmatch '^effect\.[a-z0-9_]+\.[0-9]{2}$') { throw "Invalid implemented effect id: $effectId" }
    if (-not $implementedEffectIds.Add([string]$effectId)) { throw "Duplicate implemented effect id: $effectId" }
}

if ($names.entries.Count -ne 74) { throw "Expected 74 names, found $($names.entries.Count)." }
if ($art.entries.Count -ne 74) { throw "Expected 74 art entries, found $($art.entries.Count)." }
if ($themes.themes.Count -ne 7) { throw "Expected 7 themes, found $($themes.themes.Count)." }
if ($definitions.entries.Count -ne 74) { throw "Expected 74 definitions, found $($definitions.entries.Count)." }
if ($texts.entries.Count -ne 74) { throw "Expected 74 localized texts, found $($texts.entries.Count)." }
if ($definitions.schemaVersion -ne 2) { throw "Unsupported card definition schema version: $($definitions.schemaVersion)" }

$nameIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$nameKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($entry in $names.entries) {
    if ($entry.id -notmatch '^[a-z0-9_]+$') { throw "Invalid registered card id: $($entry.id)" }
    if (-not $nameIds.Add([string]$entry.id)) { throw "Duplicate registered card id: $($entry.id)" }
    if (-not $nameKeys.Add([string]$entry.nameKey)) { throw "Duplicate name key: $($entry.nameKey)" }
    if ([string]::IsNullOrWhiteSpace([string]$entry.name)) { throw "Empty name for card: $($entry.id)" }
}

$artIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$artKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($entry in $art.entries) {
    if (-not $artIds.Add([string]$entry.cardId)) { throw "Duplicate art card id: $($entry.cardId)" }
    if (-not $artKeys.Add([string]$entry.artKey)) { throw "Duplicate art key: $($entry.artKey)" }
    if ($entry.artKey -ne "card_art.$($entry.cardId)") { throw "Art key mismatch: $($entry.artKey)" }
    if ($entry.sourcePath -notmatch '^assets/minecraft/textures/(item|block)/[a-z0-9_/.]+\.png$') {
        throw "Unsafe or unsupported Minecraft texture path: $($entry.sourcePath)"
    }
}

if ($nameIds.Count -ne $artIds.Count) { throw 'Card name and art registry id sets differ.' }
foreach ($id in $nameIds) {
    if (-not $artIds.Contains($id)) { throw "Card name and art registry id sets differ at: $id" }
}

$definitionIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$definitionById = @{}
foreach ($entry in $definitions.entries) {
    $id = [string]$entry.id
    if (-not $definitionIds.Add($id)) { throw "Duplicate card definition id: $id" }
    $definitionById[$id] = $entry
    if ($entry.artKey -ne "card_art.$id") { throw "Definition art key mismatch: $id" }
    if ($entry.nameKey -ne "card.$id.name") { throw "Definition name key mismatch: $id" }
    if ($entry.rulesTextKey -ne "card.$id.rules") { throw "Definition rules key mismatch: $id" }
    if ($null -eq $entry.keywords) { throw "Definition keywords must be an array, not null: $id" }
    $seenKeywords = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($keyword in $entry.keywords) {
        if ([string]$keyword -notin @('TAUNT', 'CHARGE')) { throw "Unsupported keyword '$keyword' on $id" }
        if (-not $seenKeywords.Add([string]$keyword)) { throw "Duplicate keyword '$keyword' on $id" }
    }
    if ($null -eq $entry.effectIds) { throw "Definition effectIds must be an array, not null: $id" }
    if ($entry.effectImplementationStatus -eq 'PENDING') {
        if ($entry.effectIds.Count -ne 1 -or $entry.effectIds[0] -ne "effect.$id.01") {
            throw "Pending card must reserve exactly effect.$id.01"
        }
    }
    elseif ($entry.effectImplementationStatus -eq 'IMPLEMENTED') {
        if ($entry.effectIds.Count -ne 1 -or -not $implementedEffectIds.Contains([string]$entry.effectIds[0])) {
            throw "Implemented card must reference an implemented effect id: $id"
        }
    }
    elseif ($entry.effectImplementationStatus -eq 'NONE') {
        if ($entry.effectIds.Count -ne 0) { throw "No-effect card has effect ids: $id" }
    }
    else { throw "Unsupported effect implementation status for ${id}: $($entry.effectImplementationStatus)" }

    switch ($entry.cardType) {
        'UNIT' { if (-not $entry.hasAttack -or -not $entry.hasHealth) { throw "Unit stats incomplete: $id" } }
        'BUILDING' { if (-not $entry.hasHealth -or $entry.buildingSlots -ne 1) { throw "Building stats incomplete: $id" } }
        'STRUCTURE' { if (-not $entry.hasHealth -or $entry.buildingSlots -lt 2) { throw "Structure stats incomplete: $id" } }
        'EQUIPMENT' { if (-not $entry.hasAttack -or -not $entry.hasDurability) { throw "Equipment stats incomplete: $id" } }
        'SPELL' { }
        'MATERIAL' { }
        default { throw "Unknown card type for ${id}: $($entry.cardType)" }
    }
}
if ($definitionIds.Count -ne $nameIds.Count) { throw 'Card definition and name registry counts differ.' }
foreach ($id in $nameIds) {
    if (-not $definitionIds.Contains($id)) { throw "Card definition registry is missing: $id" }
}
foreach ($effectId in $implementedEffectIds) {
    $implementedCard = $definitions.entries | Where-Object { $_.effectIds -contains $effectId -and $_.effectImplementationStatus -eq 'IMPLEMENTED' } | Select-Object -First 1
    if ($null -eq $implementedCard) { throw "Implemented effect registry entry is not backed by an implemented card: $effectId" }
}

$textIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($entry in $texts.entries) {
    $id = [string]$entry.id
    if (-not $textIds.Add($id)) { throw "Duplicate localized text id: $id" }
    if ($entry.nameKey -ne "card.$id.name" -or $entry.rulesTextKey -ne "card.$id.rules") {
        throw "Localized text keys mismatch: $id"
    }
    $registeredName = $names.entries | Where-Object id -eq $id | Select-Object -First 1
    if ($null -eq $registeredName -or $registeredName.name -ne $entry.name) { throw "Localized name mismatch: $id" }
}
if ($textIds.Count -ne $nameIds.Count) { throw 'Card text and name registry counts differ.' }
foreach ($id in $nameIds) {
    if (-not $textIds.Contains($id)) { throw "Card text registry is missing: $id" }
}

$expectedThemeIds = @(
    'plains_forest', 'desert_badlands', 'snow_ice', 'cave_dark_forest',
    'ocean_river', 'nether', 'end'
)
$themeIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($theme in $themes.themes) {
    if (-not $themeIds.Add([string]$theme.id)) { throw "Duplicate theme id: $($theme.id)" }
    $titleContrast = Get-Contrast $theme.frameBase $theme.titleText
    $bodyContrast = Get-Contrast $theme.rulesSurface $theme.bodyText
    if ($titleContrast -lt 4.5) { throw "Theme $($theme.id) title contrast is $([Math]::Round($titleContrast, 2)); minimum is 4.5." }
    if ($bodyContrast -lt 7.0) { throw "Theme $($theme.id) body contrast is $([Math]::Round($bodyContrast, 2)); minimum is 7.0." }
}
if ($themeIds.Count -ne $expectedThemeIds.Count) { throw 'Theme registry does not match the seven expected factions.' }
foreach ($id in $expectedThemeIds) {
    if (-not $themeIds.Contains($id)) { throw "Theme registry is missing expected faction: $id" }
}
foreach ($entry in $definitions.entries) {
    if (-not $themeIds.Contains([string]$entry.themeId)) { throw "Definition references missing theme: $($entry.id)" }
}

$markdownNames = @{}
$pattern = '^\|\s*(?<designId>(?:PF|DB|SI|CD|OR|NT|ED|TK)-\d{3})\s*\|\s*(?<name>[^|]+?)\s*\|'
foreach ($line in Get-Content -LiteralPath $markdownPath -Encoding UTF8) {
    $match = [regex]::Match($line, $pattern)
    if (-not $match.Success) { continue }
    $id = $match.Groups['designId'].Value.ToLowerInvariant().Replace('-', '_')
    $markdownNames[$id] = $match.Groups['name'].Value.Trim()
}
foreach ($entry in $names.entries) {
    if ($markdownNames[[string]$entry.id] -ne [string]$entry.name) {
        throw "Name registry is stale for $($entry.id). Run scripts/sync-card-content.ps1."
    }
}

$unityCopies = @(
    @($namesPath, (Join-Path $repoRoot 'client-unity\Assets\Game\Content\Resources\CardContent\card-name-registry.zh-CN.v1.json')),
    @($themesPath, (Join-Path $repoRoot 'client-unity\Assets\Game\Content\Resources\CardContent\card-theme-registry.v1.json')),
    @($definitionsPath, (Join-Path $repoRoot 'client-unity\Assets\Game\Content\Resources\CardContent\card-definition-registry.v1.json')),
    @($textsPath, (Join-Path $repoRoot 'client-unity\Assets\Game\Content\Resources\CardContent\card-text-registry.zh-CN.v1.json'))
)
foreach ($pair in $unityCopies) {
    if (-not (Test-Path -LiteralPath $pair[1])) { throw "Unity content copy missing: $($pair[1])" }
    $sourceHash = (Get-FileHash -LiteralPath $pair[0] -Algorithm SHA256).Hash
    $targetHash = (Get-FileHash -LiteralPath $pair[1] -Algorithm SHA256).Hash
    if ($sourceHash -ne $targetHash) { throw "Unity content copy is stale: $($pair[1])" }
}

& (Join-Path $PSScriptRoot 'sync-server-card-catalog.ps1') -Check

$pendingCount = @($definitions.entries | Where-Object effectImplementationStatus -eq 'PENDING').Count
$implementedCount = @($definitions.entries | Where-Object effectImplementationStatus -eq 'IMPLEMENTED').Count
$tauntCount = @($definitions.entries | Where-Object { $_.keywords -contains 'TAUNT' }).Count
$chargeCount = @($definitions.entries | Where-Object { $_.keywords -contains 'CHARGE' }).Count
Write-Output "Card content validation passed: 74 definitions/texts/art mappings, 7 accessible themes, $implementedCount implemented and $pendingCount reserved effects, $tauntCount TAUNT and $chargeCount CHARGE cards."
