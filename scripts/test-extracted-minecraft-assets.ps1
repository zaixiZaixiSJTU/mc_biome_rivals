[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$tempRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot ('Temp\ExtractedMinecraftAssetTests-' + [guid]::NewGuid().ToString('N'))))
$repoPrefix = $repoRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
if (-not $tempRoot.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Minecraft extraction test directory must stay inside the repository: $tempRoot"
}

[System.IO.Directory]::CreateDirectory($tempRoot) | Out-Null
$iconRoot = Join-Path $tempRoot 'icons'
$emptyAssetsRoot = Join-Path $tempRoot 'unextracted-assets'
[System.IO.Directory]::CreateDirectory($iconRoot) | Out-Null
$artRegistry = Get-Content -LiteralPath (Join-Path $repoRoot 'shared-schema\card-art\card-art-registry.v1.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$sourceConfig = Get-Content -LiteralPath (Join-Path $repoRoot 'shared-schema\card-art\minecraft-asset-source.v1.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$entries = [System.Collections.Generic.List[object]]::new()

try {
    foreach ($art in $artRegistry.entries) {
        $cardId = [string]$art.cardId
        $outputFile = "$cardId.png"
        $outputPath = Join-Path $iconRoot $outputFile
        [System.IO.File]::WriteAllText($outputPath, "test-only image payload for $cardId")
        $entries.Add([ordered]@{
            cardId = $cardId
            artKey = [string]$art.artKey
            sourcePath = [string]$art.sourcePath
            usage = [string]$art.usage
            outputFile = $outputFile
            sha256 = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash
        })
    }

    $manifest = [ordered]@{
        schemaVersion = 1
        generatedAtUtc = [DateTime]::UtcNow.ToString('o')
        sourceJar = 'test-only-local-source.jar'
        sourceJarSha256 = [string]$sourceConfig.validatedJarSha256
        sourceGameVersion = [string]$sourceConfig.gameVersion
        redistributionPolicy = [string]$sourceConfig.redistributionPolicy
        entries = $entries
    }
    $manifestPath = Join-Path $iconRoot 'asset-provenance.local.json'
    [System.IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 8) + [Environment]::NewLine)

    $relativeIconRoot = [System.IO.Path]::GetRelativePath($repoRoot, $iconRoot)
    $relativeEmptyRoot = [System.IO.Path]::GetRelativePath($repoRoot, $emptyAssetsRoot)
    $validator = Join-Path $PSScriptRoot 'validate-extracted-minecraft-assets.ps1'
    & $validator -CardIconDirectory $relativeIconRoot -WorldTextureDirectory $relativeEmptyRoot -EntityAssetDirectory $relativeEmptyRoot
    if (-not $?) { throw 'Valid synthetic local asset provenance was rejected.' }

    $unregisteredPath = Join-Path $iconRoot 'rogue.png'
    [System.IO.File]::WriteAllText($unregisteredPath, 'unregistered test-only image payload')
    $rejectedUnregistered = $false
    try {
        & $validator -CardIconDirectory $relativeIconRoot -WorldTextureDirectory $relativeEmptyRoot -EntityAssetDirectory $relativeEmptyRoot
    }
    catch { $rejectedUnregistered = $_.Exception.Message.Contains('missing a provenance entry') }
    if (-not $rejectedUnregistered) { throw 'Extracted asset validator did not reject an unregistered output file.' }
    if ([System.IO.File]::ReadAllText($unregisteredPath) -cne 'unregistered test-only image payload') {
        throw 'Read-only extracted asset validation modified an unregistered output.'
    }
    Remove-Item -LiteralPath $unregisteredPath

    $probePath = Join-Path $iconRoot 'pf_001.png'
    [System.IO.File]::WriteAllText($probePath, 'tampered test-only image payload')
    $tamperedBytes = [System.IO.File]::ReadAllText($probePath)
    $rejectedTampering = $false
    try {
        & $validator -CardIconDirectory $relativeIconRoot -WorldTextureDirectory $relativeEmptyRoot -EntityAssetDirectory $relativeEmptyRoot
    }
    catch { $rejectedTampering = $_.Exception.Message.Contains('SHA-256 mismatch') }
    if (-not $rejectedTampering) { throw 'Extracted asset validator did not reject a tampered output.' }
    if ([System.IO.File]::ReadAllText($probePath) -cne $tamperedBytes) {
        throw 'Read-only extracted asset validation modified a tampered output.'
    }

    $entityRoot = Join-Path $tempRoot 'entities'
    [System.IO.Directory]::CreateDirectory($entityRoot) | Out-Null
    $bedrock = Get-Content -LiteralPath (Join-Path $repoRoot 'shared-schema/card-art/bedrock-entity-source.v1.json') -Raw | ConvertFrom-Json
    $mainEntries = [System.Collections.Generic.List[object]]::new()
    $incrementalEntries = [System.Collections.Generic.List[object]]::new()
    $rawBase = ([string]$bedrock.sourceRepository -replace '^https://github.com/', 'https://raw.githubusercontent.com/') + '/' + $bedrock.pinnedCommit
    foreach ($kind in @('geometry','texture')) {
        $mapping = if ($kind -eq 'geometry') { $bedrock.entityGeometry } else { $bedrock.entityTextures }
        $sourceRoot = if ($kind -eq 'geometry') { $bedrock.geometryRoot } else { $bedrock.textureRoot }
        foreach ($property in $mapping.PSObject.Properties) {
            $key = [string]$property.Name
            $outputFile = if ($kind -eq 'geometry') { "entity_models/$key.json" } else { $key + [System.IO.Path]::GetExtension([string]$property.Value).ToLowerInvariant() }
            $outputPath = Join-Path $entityRoot $outputFile
            [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($outputPath)) | Out-Null
            [System.IO.File]::WriteAllText($outputPath, "synthetic-$kind-$key")
            $entry = [ordered]@{ key=$key; kind=$kind; outputFile=$outputFile;
                sourceUrl="$rawBase/$sourceRoot/$($property.Value)"; sha256=(Get-FileHash -LiteralPath $outputPath).Hash }
            if ($key -eq 'entity_sheep_baby') { $incrementalEntries.Add($entry) } else { $mainEntries.Add($entry) }
        }
    }
    $entityManifest = [ordered]@{ schemaVersion=1; sourceRepository=$bedrock.sourceRepository;
        sourceCommit=$bedrock.pinnedCommit; redistributionPolicy=$bedrock.redistributionPolicy; entries=$mainEntries }
    [System.IO.File]::WriteAllText((Join-Path $entityRoot 'entity-asset-provenance.local.json'), ($entityManifest | ConvertTo-Json -Depth 8))
    $entityManifest.entries = $incrementalEntries
    $sidecarPath = Join-Path $entityRoot 'entity-baby-sheep-provenance.local.json'
    $validSidecar = $entityManifest | ConvertTo-Json -Depth 8
    [System.IO.File]::WriteAllText($sidecarPath, $validSidecar)
    & $validator -CardIconDirectory $relativeEmptyRoot -WorldTextureDirectory $relativeEmptyRoot -EntityAssetDirectory $entityRoot
    foreach ($negative in @('duplicate','wrong-commit','missing-pair')) {
        $entityManifest.entries = if ($negative -eq 'duplicate') { @($incrementalEntries) + @($mainEntries[0]) }
            elseif ($negative -eq 'missing-pair') { @($incrementalEntries[0]) } else { $incrementalEntries }
        $entityManifest.sourceCommit = if ($negative -eq 'wrong-commit') { 'invalid-commit' } else { $bedrock.pinnedCommit }
        [System.IO.File]::WriteAllText($sidecarPath, ($entityManifest | ConvertTo-Json -Depth 8))
        $expectedMessage = switch ($negative) {
            'duplicate' { 'Duplicate entity asset provenance entry' }
            'wrong-commit' { 'source commit or redistribution policy differs' }
            'missing-pair' { 'Minecraft entity manifest is incomplete' }
        }
        $rejected = $false
        try { & $validator -CardIconDirectory $relativeEmptyRoot -WorldTextureDirectory $relativeEmptyRoot -EntityAssetDirectory $entityRoot }
        catch { $rejected = $_.Exception.Message.Contains($expectedMessage) }
        if (-not $rejected) { throw "Incremental entity ledger did not reject $negative." }
    }
    [System.IO.File]::WriteAllText($sidecarPath, $validSidecar)
    & $validator -CardIconDirectory $relativeEmptyRoot -WorldTextureDirectory $relativeEmptyRoot -EntityAssetDirectory $entityRoot
    Write-Output 'Extracted Minecraft asset validation is read-only; altered outputs and duplicate/wrong-source/incomplete incremental ledgers are rejected.'
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force
}
