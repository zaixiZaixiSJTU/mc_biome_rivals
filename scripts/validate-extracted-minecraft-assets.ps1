[CmdletBinding()]
param(
    [string]$CardIconDirectory = 'client-unity\Assets\Generated\MinecraftCardIcons',
    [string]$WorldTextureDirectory = 'client-unity\Assets\Generated\MinecraftWorldTextures\Resources\DemoWorld',
    [string]$EntityAssetDirectory = 'client-unity\Assets\Generated\MinecraftWorldTextures\Resources\DemoWorld'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$separator = [System.IO.Path]::DirectorySeparatorChar
$repoPrefix = $repoRoot.TrimEnd('\', '/') + $separator

function Resolve-RepositoryPath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { $fullPath = [System.IO.Path]::GetFullPath($Path) }
    else { $fullPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path)) }
    if (-not $fullPath.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase) -and
        -not $fullPath.Equals($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Extracted asset directory must stay inside the repository: $fullPath"
    }
    return $fullPath
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Required asset provenance input not found: $Path" }
    return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
}

function New-OutputSet {
    return ,([System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase))
}

function Register-ManifestOutput([string]$Root, [string]$OutputFile, [string]$ExpectedHash, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($OutputFile) -or $OutputFile.Contains('\') -or
        [System.IO.Path]::IsPathRooted($OutputFile)) {
        throw "$Label contains an unsafe output path: $OutputFile"
    }
    $segments = $OutputFile.Split('/')
    if ($segments.Count -eq 0 -or @($segments | Where-Object { $_ -in @('', '.', '..') }).Count -gt 0) {
        throw "$Label contains an unsafe output path: $OutputFile"
    }
    if ($ExpectedHash -notmatch '^[0-9A-Fa-f]{64}$') { throw "$Label has an invalid SHA-256: $OutputFile" }

    $fullRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $target = [System.IO.Path]::GetFullPath((Join-Path $fullRoot ($OutputFile.Replace('/', $separator))))
    if (-not $target.StartsWith(($fullRoot + $separator), [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label output escapes its extraction root: $OutputFile"
    }
    if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { throw "$Label output is missing: $OutputFile" }

    $actualHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    if ($actualHash -ine $ExpectedHash) { throw "$Label output SHA-256 mismatch: $OutputFile" }
    $assetIdentity = ($fullRoot + '/' + $OutputFile).Replace('\', '/')
    if (-not $script:ExpectedOutputs.Add($assetIdentity)) {
        throw "Duplicate extracted output path in ${Label}: $OutputFile"
    }
}

function Assert-JavaManifestHeader($Document, [string]$Label, $SourceConfig) {
    if ([int]$Document.schemaVersion -ne 1 -or
        [string]$Document.sourceGameVersion -cne [string]$SourceConfig.gameVersion -or
        [string]$Document.sourceJarSha256 -ine [string]$SourceConfig.validatedJarSha256 -or
        [string]$Document.redistributionPolicy -cne [string]$SourceConfig.redistributionPolicy) {
        throw "$Label source version, JAR fingerprint, or redistribution policy differs from the shared Minecraft source config."
    }
    if ($null -eq $Document.entries -or $Document.entries.Count -eq 0) { throw "$Label has no provenance entries." }
}

$javaSource = Read-Json (Join-Path $repoRoot 'shared-schema\card-art\minecraft-asset-source.v1.json')
$cardArt = Read-Json (Join-Path $repoRoot 'shared-schema\card-art\card-art-registry.v1.json')
$worldTextures = Read-Json (Join-Path $repoRoot 'shared-schema\card-art\minecraft-world-texture-registry.v1.json')
$bedrockSource = Read-Json (Join-Path $repoRoot 'shared-schema\card-art\bedrock-entity-source.v1.json')
$cardIconRoot = Resolve-RepositoryPath $CardIconDirectory
$worldTextureRoot = Resolve-RepositoryPath $WorldTextureDirectory
$entityAssetRoot = Resolve-RepositoryPath $EntityAssetDirectory

$script:ExpectedOutputs = New-OutputSet
$script:ScanRoots = @($cardIconRoot, $worldTextureRoot, $entityAssetRoot) | Select-Object -Unique

$validatedManifests = 0

$iconManifestPath = Join-Path $cardIconRoot 'asset-provenance.local.json'
if (Test-Path -LiteralPath $iconManifestPath -PathType Leaf) {
    $manifest = Read-Json $iconManifestPath
    Assert-JavaManifestHeader $manifest 'Minecraft card icon manifest' $javaSource
    if ($manifest.entries.Count -ne $cardArt.entries.Count) {
        throw "Minecraft card icon manifest covers $($manifest.entries.Count) entries; card art registry contains $($cardArt.entries.Count)."
    }
    $expectedByCard = @{}
    foreach ($entry in $cardArt.entries) { $expectedByCard[[string]$entry.cardId] = $entry }
    $seenCards = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($entry in $manifest.entries) {
        $cardId = [string]$entry.cardId
        if (-not $seenCards.Add($cardId)) { throw "Duplicate card icon provenance entry: $cardId" }
        if (-not $expectedByCard.ContainsKey($cardId)) { throw "Card icon is not registered: $cardId" }
        $expected = $expectedByCard[$cardId]
        if ([string]$entry.artKey -cne [string]$expected.artKey -or
            [string]$entry.sourcePath -cne [string]$expected.sourcePath -or
            [string]$entry.usage -cne [string]$expected.usage -or
            [string]$entry.outputFile -cne "$cardId.png") {
            throw "Card icon provenance differs from the shared art registry: $cardId"
        }
        Register-ManifestOutput $cardIconRoot ([string]$entry.outputFile) ([string]$entry.sha256) 'Minecraft card icon manifest'
    }
    $validatedManifests++
    Write-Output "Verified local Minecraft card icons: $($manifest.entries.Count) files."
}

$worldManifestPath = Join-Path $worldTextureRoot 'asset-provenance.local.json'
if (Test-Path -LiteralPath $worldManifestPath -PathType Leaf) {
    $manifest = Read-Json $worldManifestPath
    Assert-JavaManifestHeader $manifest 'Minecraft world texture manifest' $javaSource
    if ($manifest.entries.Count -ne $worldTextures.entries.Count) {
        throw "Minecraft world texture manifest covers $($manifest.entries.Count) entries; shared registry contains $($worldTextures.entries.Count)."
    }
    $expectedByKey = @{}
    foreach ($entry in $worldTextures.entries) { $expectedByKey[[string]$entry.key] = $entry }
    $seenKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($entry in $manifest.entries) {
        $key = [string]$entry.key
        if (-not $seenKeys.Add($key)) { throw "Duplicate world texture provenance entry: $key" }
        if (-not $expectedByKey.ContainsKey($key)) { throw "World texture is not registered: $key" }
        $expected = $expectedByKey[$key]
        if ([string]$entry.sourcePath -cne [string]$expected.sourcePath -or
            [string]$entry.outputFile -cne "$key.png") {
            throw "World texture provenance differs from the shared texture registry: $key"
        }
        Register-ManifestOutput $worldTextureRoot ([string]$entry.outputFile) ([string]$entry.sha256) 'Minecraft world texture manifest'
    }
    $validatedManifests++
    Write-Output "Verified local Minecraft world textures: $($manifest.entries.Count) files."
}

$entityManifestPath = Join-Path $entityAssetRoot 'entity-asset-provenance.local.json'
$entityManifestPaths = @()
if (Test-Path -LiteralPath $entityManifestPath -PathType Leaf) {
    $entityManifestPaths = @($entityManifestPath) + @(Get-ChildItem -LiteralPath $entityAssetRoot -File -Filter 'entity-*-provenance.local.json' |
        Where-Object { $_.FullName -ne $entityManifestPath } | Sort-Object Name | ForEach-Object FullName)
    $entityEntries = @()
    foreach ($manifestPath in $entityManifestPaths) {
        $manifest = Read-Json $manifestPath
        if ([int]$manifest.schemaVersion -ne 1 -or
            [string]$manifest.sourceRepository -cne [string]$bedrockSource.sourceRepository -or
            [string]$manifest.sourceCommit -cne [string]$bedrockSource.pinnedCommit -or
            [string]$manifest.redistributionPolicy -cne [string]$bedrockSource.redistributionPolicy) {
            throw 'Minecraft entity asset manifest source commit or redistribution policy differs from the shared Bedrock source config.'
        }
        if ($null -eq $manifest.entries -or $manifest.entries.Count -eq 0) { throw 'Minecraft entity asset manifest has no provenance entries.' }
        $entityEntries += @($manifest.entries)
    }

    $repositoryPath = ([string]$bedrockSource.sourceRepository) -replace '^https://github\.com/', ''
    $rawBase = "https://raw.githubusercontent.com/$repositoryPath/$([string]$bedrockSource.pinnedCommit)"
    $expectedEntities = @{}
    foreach ($property in $bedrockSource.entityGeometry.PSObject.Properties) {
        $key = [string]$property.Name
        $sourcePath = [string]$property.Value
        $expectedEntities["geometry|$key"] = [ordered]@{
            sourceUrl = "$rawBase/$([string]$bedrockSource.geometryRoot)/$sourcePath"
            outputFile = "entity_models/$key.json"
            optional = $false
        }
    }
    foreach ($property in $bedrockSource.entityTextures.PSObject.Properties) {
        $key = [string]$property.Name
        $sourcePath = [string]$property.Value
        $expectedEntities["texture|$key"] = [ordered]@{
            sourceUrl = "$rawBase/$([string]$bedrockSource.textureRoot)/$sourcePath"
            outputFile = "$key$([System.IO.Path]::GetExtension($sourcePath).ToLowerInvariant())"
            optional = $false
        }
    }
    foreach ($property in $bedrockSource.localJavaJarExtras.PSObject.Properties) {
        $key = [string]$property.Name
        $expectedEntities["texture|$key"] = [ordered]@{
            sourcePath = [string]$property.Value
            outputFile = "$key.png"
            optional = $true
        }
    }

    $seenEntities = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $optionalCount = 0
    foreach ($entry in $entityEntries) {
        $key = [string]$entry.key
        $kind = [string]$entry.kind
        $identity = "$kind|$key"
        if (-not $seenEntities.Add($identity)) { throw "Duplicate entity asset provenance entry: $identity" }
        if (-not $expectedEntities.ContainsKey($identity)) { throw "Entity asset is not registered: $identity" }
        $expected = $expectedEntities[$identity]
        if ([string]$entry.outputFile -cne [string]$expected.outputFile) { throw "Entity asset output path differs from registry: $identity" }
        if ($expected.optional) {
            if (-not ([string]$entry.sourceUrl).StartsWith('local-jar:', [System.StringComparison]::Ordinal) -or
                -not ([string]$entry.sourceUrl).EndsWith('#' + [string]$expected.sourcePath, [System.StringComparison]::Ordinal)) {
                throw "Local Java entity texture provenance differs from registry: $identity"
            }
            $optionalCount++
        }
        elseif ([string]$entry.sourceUrl -cne [string]$expected.sourceUrl) {
            throw "Entity asset source URL differs from the pinned Bedrock registry: $identity"
        }
        Register-ManifestOutput $entityAssetRoot ([string]$entry.outputFile) ([string]$entry.sha256) 'Minecraft entity asset manifest'
    }
    $requiredEntityCount = @($expectedEntities.Values | Where-Object { -not $_.optional }).Count
    $allOptionalCount = @($expectedEntities.Values | Where-Object { $_.optional }).Count
    if ($seenEntities.Count -ne ($requiredEntityCount + $optionalCount) -or
        ($optionalCount -ne 0 -and $optionalCount -ne $allOptionalCount)) {
        throw "Minecraft entity manifest is incomplete: required $requiredEntityCount, optional extras $optionalCount/$allOptionalCount."
    }
    $validatedManifests++
    Write-Output "Verified local Minecraft entity assets: $($entityEntries.Count) files across $($entityManifestPaths.Count) ledgers from commit $($bedrockSource.pinnedCommit)."
}

foreach ($root in $script:ScanRoots) {
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { continue }
    foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File -Force) {
        if ($file.Extension -eq '.meta' -or $file.Name -eq '.gitkeep' -or
            $file.Name -eq 'asset-provenance.local.json' -or $entityManifestPaths -contains $file.FullName) { continue }
        $assetIdentity = $file.FullName.Replace('\', '/')
        if (-not $script:ExpectedOutputs.Contains($assetIdentity)) {
            $relative = [System.IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
            throw "Extracted local asset is missing a provenance entry: $relative"
        }
    }
}

if ($validatedManifests -eq 0) {
    Write-Output 'No local Minecraft extraction manifests found; optional art sources are not extracted on this machine.'
}
