[CmdletBinding()]
param(
    [string]$MinecraftJar,
    [string]$SourceConfigPath = 'shared-schema\card-art\minecraft-asset-source.v1.json',
    [string]$OutputDirectory = 'client-unity\Assets\Generated\MinecraftWorldTextures\Resources\DemoWorld'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$sourceConfigFile = Join-Path $repoRoot $SourceConfigPath
$outputRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))

if (-not $outputRoot.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Output directory must stay inside the repository: $outputRoot"
}
if (-not (Test-Path -LiteralPath $sourceConfigFile)) { throw "Source config not found: $sourceConfigFile" }
$sourceConfig = Get-Content -LiteralPath $sourceConfigFile -Raw -Encoding UTF8 | ConvertFrom-Json

if (-not $MinecraftJar) {
    $versionFolder = [string]$sourceConfig.versionFolder
    $MinecraftJar = Join-Path $env:APPDATA ".minecraft\versions\$versionFolder\$versionFolder.jar"
}
$MinecraftJar = [System.IO.Path]::GetFullPath($MinecraftJar)
if (-not (Test-Path -LiteralPath $MinecraftJar)) { throw "Minecraft client JAR not found: $MinecraftJar" }

$textures = [ordered]@{
    dirt = 'assets/minecraft/textures/block/dirt.png'
    grass_block_top = 'assets/minecraft/textures/block/grass_block_top.png'
    mossy_stone_bricks = 'assets/minecraft/textures/block/mossy_stone_bricks.png'
    oak_planks = 'assets/minecraft/textures/block/oak_planks.png'
    oak_leaves = 'assets/minecraft/textures/block/oak_leaves.png'
    stone_bricks = 'assets/minecraft/textures/block/stone_bricks.png'
    polished_blackstone_bricks = 'assets/minecraft/textures/block/polished_blackstone_bricks.png'
    nether_bricks = 'assets/minecraft/textures/block/nether_bricks.png'
    netherrack = 'assets/minecraft/textures/block/netherrack.png'
    basalt_top = 'assets/minecraft/textures/block/basalt_top.png'
    water_still = 'assets/minecraft/textures/block/water_still.png'
    magma = 'assets/minecraft/textures/block/magma.png'
    red_sandstone = 'assets/minecraft/textures/block/red_sandstone.png'
    packed_ice = 'assets/minecraft/textures/block/packed_ice.png'
    deepslate_bricks = 'assets/minecraft/textures/block/deepslate_bricks.png'
    prismarine_bricks = 'assets/minecraft/textures/block/prismarine_bricks.png'
    purpur_block = 'assets/minecraft/textures/block/purpur_block.png'
    entity_magma_cube = 'assets/minecraft/textures/entity/slime/magmacube.png'
    entity_blaze = 'assets/minecraft/textures/entity/blaze.png'
    entity_bee = 'assets/minecraft/textures/entity/bee/bee.png'
    entity_sheep = 'assets/minecraft/textures/entity/sheep/sheep.png'
    entity_wolf = 'assets/minecraft/textures/entity/wolf/wolf.png'
    entity_villager = 'assets/minecraft/textures/entity/villager/villager.png'
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($MinecraftJar)
try {
    $entries = @{}
    foreach ($entry in $archive.Entries) { $entries[$entry.FullName] = $entry }
    foreach ($sourcePath in $textures.Values) {
        if (-not $entries.ContainsKey($sourcePath)) { throw "Missing Minecraft JAR texture: $sourcePath" }
    }

    [System.IO.Directory]::CreateDirectory($outputRoot) | Out-Null
    $provenance = [System.Collections.Generic.List[object]]::new()
    foreach ($item in $textures.GetEnumerator()) {
        $targetPath = Join-Path $outputRoot "$($item.Key).png"
        $inputStream = $entries[$item.Value].Open()
        try {
            $outputStream = [System.IO.File]::Open($targetPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
            try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose() }
        }
        finally { $inputStream.Dispose() }

        $provenance.Add([ordered]@{
            key = [string]$item.Key
            sourcePath = [string]$item.Value
            outputFile = "$($item.Key).png"
            sha256 = (Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash
        })
    }

    $document = [ordered]@{
        schemaVersion = 1
        generatedAtUtc = [DateTime]::UtcNow.ToString('o')
        sourceJar = $MinecraftJar
        sourceJarSha256 = (Get-FileHash -LiteralPath $MinecraftJar -Algorithm SHA256).Hash
        sourceGameVersion = [string]$sourceConfig.gameVersion
        redistributionPolicy = [string]$sourceConfig.redistributionPolicy
        entries = $provenance
    }
    [System.IO.File]::WriteAllText(
        (Join-Path $outputRoot 'asset-provenance.local.json'),
        ($document | ConvertTo-Json -Depth 8) + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false))

    Write-Output "Extracted $($provenance.Count) local 2.5D block/entity textures -> $outputRoot"
}
finally {
    $archive.Dispose()
}
