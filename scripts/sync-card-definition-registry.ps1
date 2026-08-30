[CmdletBinding()]
param(
    [string]$SourceMarkdown = 'docs\design\Minecraft_Biome_Rivals_Prototype_Cards_v0.1.md',
    [string]$DefinitionOutput = 'shared-schema\card-data\card-definition-registry.v1.json',
    [string]$TextOutput = 'shared-schema\card-data\localization\card-text-registry.zh-CN.v1.json',
    [string]$ImplementedEffects = 'shared-schema\card-data\implemented-effect-registry.v1.json'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $repoRoot $SourceMarkdown
if (-not (Test-Path -LiteralPath $sourcePath)) { throw "Card design source not found: $sourcePath" }
$implementedEffectsPath = Join-Path $repoRoot $ImplementedEffects
if (-not (Test-Path -LiteralPath $implementedEffectsPath)) { throw "Implemented effect registry not found: $implementedEffectsPath" }
$implementedEffectDocument = Get-Content -LiteralPath $implementedEffectsPath -Raw -Encoding UTF8 | ConvertFrom-Json
$implementedEffectIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($effectId in $implementedEffectDocument.implementedEffectIds) {
    if (-not $implementedEffectIds.Add([string]$effectId)) { throw "Duplicate implemented effect id: $effectId" }
}

$factionByPrefix = [ordered]@{
    PF = 'plains_forest'; DB = 'desert_badlands'; SI = 'snow_ice'; CD = 'cave_dark_forest'
    OR = 'ocean_river'; NT = 'nether'; ED = 'end'
}
$tokenThemeByNumber = @{
    1='plains_forest'; 2='plains_forest'; 3='plains_forest'; 4='plains_forest'
    5='desert_badlands'; 6='desert_badlands'; 7='desert_badlands'; 8='desert_badlands'
    9='snow_ice'; 10='cave_dark_forest'; 11='cave_dark_forest'; 12='ocean_river'
    13='nether'; 14='nether'; 15='nether'; 16='end'; 17='end'; 18='desert_badlands'
}
$rarityMap = [ordered]@{ '常见'='COMMON'; '稀有'='RARE'; '史诗'='EPIC'; '传说'='LEGENDARY' }
$tagMap = [ordered]@{
    '节肢'='arthropod'; '动物'='animal'; '村民'='villager'; '交易'='trade'; '植物'='plant'
    '生产'='production'; '繁殖'='breeding'; '自然'='nature'; '傀儡'='golem'; '亡灵'='undead'
    '僵尸'='zombie'; '考古'='archaeology'; '材料'='material'; '防御'='defense'; '灾厄'='illager'
    '结构'='structure'; '冻伤'='frostbite'; '骷髅'='skeleton'; '回复'='healing'; '幽匿'='sculk'
    '深暗'='deep_dark'; '水生'='aquatic'; '守卫者'='guardian'; '武器'='weapon'; '水流'='current'
    '岩浆'='magma'; '猪灵'='piglin'; '火焰'='fire'; '凋灵'='wither'; '下界'='nether'
    '红石'='redstone'; '末影'='ender'; '仪式'='ritual'; '虚空'='void'; '悬置'='suspend'
    '纤维'='fiber'; '食物'='food'; '幼体'='juvenile'; '掩埋'='buried'; '爆炸'='explosive'
    '石材'='stone'; '海晶'='prismarine'; '龙'='dragon'; '宝石'='gem'
}

function Normalize-RulesText([string]$Value) {
    return $Value.Replace('**', '').Trim()
}

function Resolve-Keywords([string]$RulesText) {
    $keywords = [System.Collections.Generic.List[string]]::new()
    if ($RulesText -match '(^|[。；;])\s*嘲讽(?=[。：；;]|$)') { $keywords.Add('TAUNT') }
    if ($RulesText -match '(^|[。；;])\s*冲锋(?=[。：；;]|$)') { $keywords.Add('CHARGE') }
    return ,([string[]]$keywords.ToArray())
}

function Resolve-CardType([string]$TypeLabel) {
    if ($TypeLabel -match '生物') { return 'UNIT' }
    if ($TypeLabel -match '建筑') { return 'BUILDING' }
    if ($TypeLabel -match '结构') { return 'STRUCTURE' }
    if ($TypeLabel -match '装备') { return 'EQUIPMENT' }
    if ($TypeLabel -match '法术') { return 'SPELL' }
    return 'MATERIAL'
}

$definitions = [System.Collections.Generic.List[object]]::new()
$texts = [System.Collections.Generic.List[object]]::new()
$seenIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$recipesByTarget = @{}

foreach ($line in Get-Content -LiteralPath $sourcePath -Encoding UTF8) {
    if ($line -notmatch '^\|\s*(?<recipeDesignId>CR-\d{3})\s*\|') { continue }
    $cells = @($line.Trim().Trim('|').Split('|') | ForEach-Object { $_.Trim() })
    if ($cells.Count -lt 7) { throw "Incomplete crafting recipe row: $($Matches['recipeDesignId'])" }
    $recipeDesignId, $targetDesignId, $ingredientText, $attackBonusText, $healthBonusText, $durabilityBonusText = $cells[0..5]
    if ($targetDesignId -notmatch '^(?:PF|DB|SI|CD|OR|NT|ED|TK)-\d{3}$') { throw "Invalid crafting target id: $targetDesignId" }
    if ($recipesByTarget.ContainsKey($targetDesignId)) { throw "Duplicate crafting recipe target: $targetDesignId" }
    $ingredients = [System.Collections.Generic.List[object]]::new()
    foreach ($ingredientPart in $ingredientText.Split('+')) {
        $part = $ingredientPart.Trim()
        if ($part -notmatch '^(?<cardDesignId>(?:PF|DB|SI|CD|OR|NT|ED|TK)-\d{3})\s*[×xX]\s*(?<count>\d+)$') {
            throw "Invalid crafting ingredient '$part' for $recipeDesignId"
        }
        $count = [int]$Matches['count']
        if ($count -lt 1) { throw "Crafting ingredient count must be positive for $recipeDesignId" }
        $ingredients.Add([ordered]@{
            cardId=$Matches['cardDesignId'].ToLowerInvariant().Replace('-', '_')
            count=$count
        })
    }
    $recipesByTarget[$targetDesignId] = [ordered]@{
        recipeId="recipe.$($targetDesignId.ToLowerInvariant().Replace('-', '_')).01"
        ingredients=$ingredients
        attackBonus=[int]$attackBonusText
        healthBonus=[int]$healthBonusText
        durabilityBonus=[int]$durabilityBonusText
    }
}

foreach ($line in Get-Content -LiteralPath $sourcePath -Encoding UTF8) {
    if ($line -notmatch '^\|\s*(?<designId>(?:PF|DB|SI|CD|OR|NT|ED|TK)-\d{3})\s*\|') { continue }
    $cells = @($line.Trim().Trim('|').Split('|') | ForEach-Object { $_.Trim() })
    $designId = $Matches['designId']
    $cardId = $designId.ToLowerInvariant().Replace('-', '_')
    if (-not $seenIds.Add($cardId)) { throw "Duplicate card id: $cardId" }

    $isToken = $designId.StartsWith('TK-', [System.StringComparison]::Ordinal)
    if ($isToken) {
        if ($cells.Count -lt 7) { throw "Incomplete token row: $designId" }
        $name, $typeLabel, $costText, $attributeText, $tagText, $rulesText = $cells[1..6]
        $rarityLabel = '衍生'
        $rarity = 'TOKEN'
        $designNotes = '不可收集衍生牌'
        $factionId = 'neutral'
        $tokenNumber = [int]$designId.Substring(3)
        $themeId = $tokenThemeByNumber[$tokenNumber]
    }
    else {
        if ($cells.Count -lt 9) { throw "Incomplete collectible row: $designId" }
        $name, $rarityLabel, $typeLabel, $costText, $attributeText, $tagText, $rulesText, $designNotes = $cells[1..8]
        $rarity = $rarityMap[$rarityLabel]
        if (-not $rarity) { throw "Unknown rarity '$rarityLabel' for $designId" }
        $prefix = $designId.Substring(0, 2)
        $factionId = $factionByPrefix[$prefix]
        $themeId = $factionId
    }

    $cardType = Resolve-CardType $typeLabel
    $cost = [int]$costText
    $tags = @()
    $tagLabels = @($tagText.Split('/') | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    foreach ($tagLabel in $tagLabels) {
        $tag = $tagMap[$tagLabel]
        if (-not $tag) { throw "Unregistered tag '$tagLabel' for $designId" }
        $tags += $tag
    }

    $hasAttack = $false; $attack = 0; $hasHealth = $false; $health = 0
    $hasDurability = $false; $durability = 0; $buildingSlots = 0
    if ($cardType -eq 'UNIT' -and $attributeText -match '^(\d+)\s*/\s*(\d+)$') {
        $hasAttack = $true; $attack = [int]$Matches[1]; $hasHealth = $true; $health = [int]$Matches[2]
    }
    elseif ($cardType -eq 'BUILDING' -and $attributeText -match '^(\d+)$') {
        $hasHealth = $true; $health = [int]$Matches[1]; $buildingSlots = 1
    }
    elseif ($cardType -eq 'STRUCTURE' -and $attributeText -match '^(\d+)\s*[；;]\s*(\d+)\s*格$') {
        $hasHealth = $true; $health = [int]$Matches[1]; $buildingSlots = [int]$Matches[2]
    }
    elseif ($cardType -eq 'EQUIPMENT' -and $attributeText -match '^(\d+)\s*/\s*(\d+)$') {
        $hasAttack = $true; $attack = [int]$Matches[1]; $hasDurability = $true; $durability = [int]$Matches[2]
    }
    elseif ($cardType -notin @('SPELL','MATERIAL')) {
        throw "Unsupported attributes '$attributeText' for $designId ($cardType)"
    }

    $normalizedRules = Normalize-RulesText $rulesText
    $keywords = Resolve-Keywords $normalizedRules
    $hasEffect = $normalizedRules -and $normalizedRules -ne '无卡牌文本。'
    $effectId = "effect.$cardId.01"
    $effectIds = [System.Collections.Generic.List[string]]::new()
    if ($hasEffect) { $effectIds.Add($effectId) }
    $recipe = $recipesByTarget[$designId]
    $hasCraftingRecipe = $null -ne $recipe
    $craftingRecipe = [System.Collections.Generic.List[object]]::new()
    if ($hasCraftingRecipe) {
        foreach ($ingredient in $recipe.ingredients) { $craftingRecipe.Add($ingredient) }
    }
    $definitions.Add([ordered]@{
        id=$cardId; designId=$designId; contentVersion=17; collectible=(-not $isToken)
        nameKey="card.$cardId.name"; rulesTextKey="card.$cardId.rules"
        factionId=$factionId; themeId=$themeId; rarity=$rarity; cardType=$cardType; cost=$cost
        hasAttack=$hasAttack; attack=$attack; hasHealth=$hasHealth; health=$health
        hasDurability=$hasDurability; durability=$durability; buildingSlots=$buildingSlots
        artKey="card_art.$cardId"; tags=([string[]]$tags); keywords=$keywords
        hasCraftingRecipe=$hasCraftingRecipe
        recipeId=$(if ($hasCraftingRecipe) { $recipe.recipeId } else { '' })
        craftingRecipe=$craftingRecipe
        craftedAttackBonus=$(if ($hasCraftingRecipe) { $recipe.attackBonus } else { 0 })
        craftedHealthBonus=$(if ($hasCraftingRecipe) { $recipe.healthBonus } else { 0 })
        craftedDurabilityBonus=$(if ($hasCraftingRecipe) { $recipe.durabilityBonus } else { 0 })
        effectImplementationStatus=$(if (-not $hasEffect) { 'NONE' } elseif ($implementedEffectIds.Contains($effectId)) { 'IMPLEMENTED' } else { 'PENDING' })
        effectIds=$effectIds
    })
    $texts.Add([ordered]@{
        id=$cardId; nameKey="card.$cardId.name"; name=$name
        rulesTextKey="card.$cardId.rules"; rulesText=$normalizedRules
        typeLabel=$typeLabel; rarityLabel=$rarityLabel; tagLabels=([string[]]$tagLabels); designNotes=$designNotes
    })
}

foreach ($effectId in $implementedEffectIds) {
    if (-not ($definitions | Where-Object { $_.effectIds -contains $effectId })) { throw "Implemented effect id is not registered by a card: $effectId" }
}

foreach ($targetDesignId in $recipesByTarget.Keys) {
    $targetId = $targetDesignId.ToLowerInvariant().Replace('-', '_')
    $target = $definitions | Where-Object { $_.id -eq $targetId } | Select-Object -First 1
    if ($null -eq $target) { throw "Crafting target is not registered: $targetDesignId" }
    foreach ($ingredient in $target.craftingRecipe) {
        $material = $definitions | Where-Object { $_.id -eq $ingredient.cardId } | Select-Object -First 1
        if ($null -eq $material) { throw "Crafting ingredient is not registered: $($ingredient.cardId)" }
        if ($material.cardType -ne 'MATERIAL') { throw "Crafting ingredient must be MATERIAL: $($ingredient.cardId)" }
    }
}

if ($definitions.Count -ne 74) { throw "Expected 74 card definitions, found $($definitions.Count)." }
$definitionDocument = [ordered]@{ schemaVersion=3; contentVersion=17; source=$SourceMarkdown.Replace('\','/'); entries=$definitions }
$textDocument = [ordered]@{ schemaVersion=1; locale='zh-CN'; source=$SourceMarkdown.Replace('\','/'); entries=$texts }

foreach ($output in @(
    @((Join-Path $repoRoot $DefinitionOutput), $definitionDocument),
    @((Join-Path $repoRoot $TextOutput), $textDocument)
)) {
    [System.IO.Directory]::CreateDirectory((Split-Path -Parent $output[0])) | Out-Null
    $json = $output[1] | ConvertTo-Json -Depth 10
    [System.IO.File]::WriteAllText($output[0], $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
}
Write-Output "Registered $($definitions.Count) complete card definitions and localized texts."
