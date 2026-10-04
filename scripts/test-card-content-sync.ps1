[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$syncNames = Join-Path $PSScriptRoot 'sync-card-name-registry.ps1'
$syncDefinitions = Join-Path $PSScriptRoot 'sync-card-definition-registry.ps1'
$schemaValidator = Join-Path $repoRoot 'server-nakama\scripts\validate-card-content-schemas.mjs'
$tempParent = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'Temp'))
$tempRoot = [System.IO.Path]::GetFullPath((Join-Path $tempParent ("CardContentSyncTests-{0}" -f [Guid]::NewGuid().ToString('N'))))
$tempPrefix = $tempParent.TrimEnd([char[]]@('\', '/')) + [System.IO.Path]::DirectorySeparatorChar
if (-not $tempRoot.StartsWith($tempPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to create content sync test data outside Temp: $tempRoot"
}

$relativeTempRoot = [System.IO.Path]::GetRelativePath($repoRoot, $tempRoot)
$nameOutput = Join-Path $relativeTempRoot 'card-name-registry.json'
$definitionOutput = Join-Path $relativeTempRoot 'card-definition-registry.json'
$textOutput = Join-Path $relativeTempRoot 'card-text-registry.json'
[System.IO.Directory]::CreateDirectory($tempRoot) | Out-Null

try {
    & $syncNames -OutputPath $nameOutput
    & $syncNames -OutputPath $nameOutput -Check
    $namePath = Join-Path $repoRoot $nameOutput
    $nameHashBeforeCheck = (Get-FileHash -LiteralPath $namePath -Algorithm SHA256).Hash
    & $syncNames -OutputPath $nameOutput -Check
    $nameHashAfterCheck = (Get-FileHash -LiteralPath $namePath -Algorithm SHA256).Hash
    if ($nameHashBeforeCheck -ne $nameHashAfterCheck) { throw 'Card name -Check modified its input file.' }

    $canonicalNames = [System.IO.File]::ReadAllText($namePath).Replace("`r`n", "`n")
    $syncCatalog = Join-Path $PSScriptRoot 'sync-server-card-catalog.ps1'
    $catalogOutput = Join-Path $relativeTempRoot 'server-catalog.ts'
    $catalogPath = Join-Path $repoRoot $catalogOutput
    & $syncCatalog -Output $catalogOutput
    $canonicalCatalog = [System.IO.File]::ReadAllText($catalogPath).Replace("`r`n", "`n")
    foreach ($newline in @("`n", "`r`n")) {
        [System.IO.File]::WriteAllText($namePath, $canonicalNames.Replace("`n", $newline))
        [System.IO.File]::WriteAllText($catalogPath, $canonicalCatalog.Replace("`n", $newline))
        $nameBefore = (Get-FileHash -LiteralPath $namePath).Hash
        $catalogBefore = (Get-FileHash -LiteralPath $catalogPath).Hash
        & $syncNames -OutputPath $nameOutput -Check
        & $syncCatalog -Output $catalogOutput -Check
        if ($nameBefore -ne (Get-FileHash -LiteralPath $namePath).Hash -or
            $catalogBefore -ne (Get-FileHash -LiteralPath $catalogPath).Hash) { throw 'Line-ending checks modified generated content.' }
    }
    [System.IO.File]::WriteAllText($catalogPath, $canonicalCatalog + "// unexpected drift`n")
    $catalogDriftRejected = $false
    try { & $syncCatalog -Output $catalogOutput -Check }
    catch { $catalogDriftRejected = $_.Exception.Message.Contains('Generated server card catalog is stale') }
    if (-not $catalogDriftRejected) { throw 'Line-ending normalization hid server catalog content drift.' }

    $nameDocument = Get-Content -LiteralPath $namePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $nameDocument.entries[0].name += ' stale'
    $nameJson = ($nameDocument | ConvertTo-Json -Depth 6) + [Environment]::NewLine
    [System.IO.File]::WriteAllText($namePath, $nameJson, [System.Text.UTF8Encoding]::new($false))
    $nameStaleRejected = $false
    try { & $syncNames -OutputPath $nameOutput -Check }
    catch { $nameStaleRejected = $true }
    if (-not $nameStaleRejected) { throw 'Card name -Check accepted a stale generated registry.' }

    $effectsPath = Join-Path $repoRoot 'shared-schema\card-data\implemented-effect-registry.v1.json'
    $effectsDocument = Get-Content -LiteralPath $effectsPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $effectsDocument.schemaVersion = 999
    $invalidEffectsPath = Join-Path $tempRoot 'invalid-implemented-effect-registry.json'
    $invalidEffectsJson = ($effectsDocument | ConvertTo-Json -Depth 6) + [Environment]::NewLine
    [System.IO.File]::WriteAllText($invalidEffectsPath, $invalidEffectsJson, [System.Text.UTF8Encoding]::new($false))
    $schemaOutput = & node $schemaValidator --effects $invalidEffectsPath 2>&1
    $schemaExitCode = $LASTEXITCODE
    if ($schemaExitCode -eq 0) { throw 'Card content JSON Schema accepted an unsupported effect registry schema version.' }
    $global:LASTEXITCODE = 0
    Write-Output 'Card content JSON Schema rejected an unsupported effect registry schema version.'

    $effectsDocument.schemaVersion = 1
    $canonicalDefinitions = Get-Content -LiteralPath (Join-Path $repoRoot 'shared-schema\card-data\card-definition-registry.v1.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $effectsDocument.contentVersion = [int]$canonicalDefinitions.implementedEffectRegistryVersion + 1
    $mismatchedEffectsPath = Join-Path $tempRoot 'mismatched-implemented-effect-registry.json'
    $mismatchedEffectsJson = ($effectsDocument | ConvertTo-Json -Depth 6) + [Environment]::NewLine
    [System.IO.File]::WriteAllText($mismatchedEffectsPath, $mismatchedEffectsJson, [System.Text.UTF8Encoding]::new($false))
    $schemaOutput = & node $schemaValidator --effects $mismatchedEffectsPath 2>&1
    $schemaExitCode = $LASTEXITCODE
    if ($schemaExitCode -eq 0) { throw 'Card content validator accepted a stale implemented-effect registry version reference.' }
    $global:LASTEXITCODE = 0
    Write-Output 'Card content validator rejected an effect registry version mismatch.'

    $artPath = Join-Path $repoRoot 'shared-schema\card-art\card-art-registry.v1.json'
    $artDocument = Get-Content -LiteralPath $artPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $artDocument.entries[0].sourcePath = '../textures/unsafe.png'
    $invalidArtPath = Join-Path $tempRoot 'invalid-card-art-registry.json'
    $invalidArtJson = ($artDocument | ConvertTo-Json -Depth 6) + [Environment]::NewLine
    [System.IO.File]::WriteAllText($invalidArtPath, $invalidArtJson, [System.Text.UTF8Encoding]::new($false))
    $schemaOutput = & node $schemaValidator --art $invalidArtPath 2>&1
    $schemaExitCode = $LASTEXITCODE
    if ($schemaExitCode -eq 0) { throw 'Card art JSON Schema accepted a path outside Minecraft textures.' }
    $global:LASTEXITCODE = 0
    Write-Output 'Card art JSON Schema rejected a path outside Minecraft textures.'

    $worldTextureRegistryPath = Join-Path $repoRoot 'shared-schema\card-art\minecraft-world-texture-registry.v1.json'
    $worldTextureDocument = Get-Content -LiteralPath $worldTextureRegistryPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $worldTextureDocument.entries[0].sourcePath = '../unsafe.png'
    $invalidWorldTexturePath = Join-Path $tempRoot 'invalid-world-texture-registry.json'
    $invalidWorldTextureJson = ($worldTextureDocument | ConvertTo-Json -Depth 6) + [Environment]::NewLine
    [System.IO.File]::WriteAllText($invalidWorldTexturePath, $invalidWorldTextureJson, [System.Text.UTF8Encoding]::new($false))
    $schemaOutput = & node $schemaValidator --world-textures $invalidWorldTexturePath 2>&1
    $schemaExitCode = $LASTEXITCODE
    if ($schemaExitCode -eq 0) { throw 'Minecraft world texture schema accepted a path outside its texture root.' }
    $global:LASTEXITCODE = 0
    Write-Output 'Minecraft world texture schema rejected a path outside its texture root.'

    $bedrockSourcePath = Join-Path $repoRoot 'shared-schema\card-art\bedrock-entity-source.v1.json'
    $bedrockSourceDocument = Get-Content -LiteralPath $bedrockSourcePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $bedrockSourceDocument.entityTextures.PSObject.Properties.Remove('entity_bee')
    $invalidBedrockSourcePath = Join-Path $tempRoot 'invalid-bedrock-entity-source.json'
    $invalidBedrockSourceJson = ($bedrockSourceDocument | ConvertTo-Json -Depth 8) + [Environment]::NewLine
    [System.IO.File]::WriteAllText($invalidBedrockSourcePath, $invalidBedrockSourceJson, [System.Text.UTF8Encoding]::new($false))
    $schemaOutput = & node $schemaValidator --bedrock-source $invalidBedrockSourcePath 2>&1
    $schemaExitCode = $LASTEXITCODE
    if ($schemaExitCode -eq 0) { throw 'Bedrock entity source validator accepted mismatched geometry and texture keys.' }
    $global:LASTEXITCODE = 0
    Write-Output 'Bedrock entity source validator rejected mismatched geometry and texture keys.'

    $bedrockSourceDocument = Get-Content -LiteralPath $bedrockSourcePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $bedrockSourceDocument.entityGeometry.entity_bee = '../escape.geo.json'
    $unsafeBedrockSourcePath = Join-Path $tempRoot 'unsafe-bedrock-entity-source.json'
    $unsafeBedrockSourceJson = ($bedrockSourceDocument | ConvertTo-Json -Depth 8) + [Environment]::NewLine
    [System.IO.File]::WriteAllText($unsafeBedrockSourcePath, $unsafeBedrockSourceJson, [System.Text.UTF8Encoding]::new($false))
    $schemaOutput = & node $schemaValidator --bedrock-source $unsafeBedrockSourcePath 2>&1
    $schemaExitCode = $LASTEXITCODE
    if ($schemaExitCode -eq 0) { throw 'Bedrock entity source schema accepted a path outside the geometry root.' }
    $global:LASTEXITCODE = 0
    Write-Output 'Bedrock entity source schema rejected a path outside the geometry root.'

    & $syncDefinitions -DefinitionOutput $definitionOutput -TextOutput $textOutput
    & $syncDefinitions -DefinitionOutput $definitionOutput -TextOutput $textOutput -Check
    $definitionPath = Join-Path $repoRoot $definitionOutput
    $definitionDocument = Get-Content -LiteralPath $definitionPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $definitionDocument.entries[0].cost = [int]$definitionDocument.entries[0].cost + 1
    $definitionJson = ($definitionDocument | ConvertTo-Json -Depth 10) + [Environment]::NewLine
    [System.IO.File]::WriteAllText($definitionPath, $definitionJson, [System.Text.UTF8Encoding]::new($false))
    $definitionStaleRejected = $false
    try { & $syncDefinitions -DefinitionOutput $definitionOutput -TextOutput $textOutput -Check }
    catch { $definitionStaleRejected = $true }
    if (-not $definitionStaleRejected) { throw 'Card definition -Check accepted a stale generated registry.' }

    Write-Output 'Card content sync checks are read-only and reject stale generated names and definitions.'
}
finally {
    $resolvedTempRoot = [System.IO.Path]::GetFullPath($tempRoot)
    if ($resolvedTempRoot.StartsWith($tempPrefix, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $resolvedTempRoot -PathType Container)) {
        Remove-Item -LiteralPath $resolvedTempRoot -Recurse -Force
    }
}
