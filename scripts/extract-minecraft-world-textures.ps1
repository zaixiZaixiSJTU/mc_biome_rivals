[CmdletBinding()]
param(
    [string]$MinecraftJar,
    [string]$SourceConfigPath = 'shared-schema\card-art\minecraft-asset-source.v1.json',
    [string]$TextureRegistryPath = 'shared-schema\card-art\minecraft-world-texture-registry.v1.json',
    [string]$OutputDirectory = 'client-unity\Assets\Generated\MinecraftWorldTextures\Resources\DemoWorld'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$sourceConfigFile = Join-Path $repoRoot $SourceConfigPath
$textureRegistryFile = Join-Path $repoRoot $TextureRegistryPath
$outputRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))

if (-not $outputRoot.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Output directory must stay inside the repository: $outputRoot"
}
if (-not (Test-Path -LiteralPath $sourceConfigFile)) { throw "Source config not found: $sourceConfigFile" }
if (-not (Test-Path -LiteralPath $textureRegistryFile)) { throw "World texture registry not found: $textureRegistryFile" }
$sourceConfig = Get-Content -LiteralPath $sourceConfigFile -Raw -Encoding UTF8 | ConvertFrom-Json
$textureRegistry = Get-Content -LiteralPath $textureRegistryFile -Raw -Encoding UTF8 | ConvertFrom-Json
if ([int]$textureRegistry.schemaVersion -ne 1 -or $textureRegistry.edition -ne 'Java' -or
    $textureRegistry.textureRoot -cne 'assets/minecraft/textures/block') {
    throw "Unsupported Minecraft world texture registry: $textureRegistryFile"
}

if (-not $MinecraftJar) {
    $versionFolder = [string]$sourceConfig.versionFolder
    $MinecraftJar = Join-Path $env:APPDATA ".minecraft\versions\$versionFolder\$versionFolder.jar"
}
$MinecraftJar = [System.IO.Path]::GetFullPath($MinecraftJar)
if (-not (Test-Path -LiteralPath $MinecraftJar)) { throw "Minecraft client JAR not found: $MinecraftJar" }

$textures = [ordered]@{}
foreach ($texture in $textureRegistry.entries) {
    $key = [string]$texture.key
    $sourcePath = [string]$texture.sourcePath
    if ($key -notmatch '^[a-z0-9_]+$') { throw "Unsafe world texture key: $key" }
    if ($textures.Contains($key)) { throw "Duplicate world texture key: $key" }
    if ($sourcePath -notmatch '^assets/minecraft/textures/block/[A-Za-z0-9_./-]+\.png$') {
        throw "Unsafe world texture source path for ${key}: $sourcePath"
    }
    $textures.Add($key, $sourcePath)
}
if ($textures.Count -eq 0) { throw 'Minecraft world texture registry is empty.' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Drawing

function Copy-MinecraftPngFirstFrame {
    param($Entry, [string]$TargetPath)
    $temporaryPath = $null
    $inputStream = $Entry.Open()
    try {
        $outputStream = [System.IO.File]::Open($TargetPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
        try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose() }
    }
    finally { $inputStream.Dispose() }

    $sourceBitmap = [System.Drawing.Bitmap]::FromFile($TargetPath)
    try {
        if ($sourceBitmap.Height -le $sourceBitmap.Width -or $sourceBitmap.Height % $sourceBitmap.Width -ne 0) { return }
        $frameSize = $sourceBitmap.Width
        $frameBitmap = [System.Drawing.Bitmap]::new($frameSize, $frameSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($frameBitmap)
            try {
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
                $rect = [System.Drawing.Rectangle]::new(0, 0, $frameSize, $frameSize)
                $graphics.DrawImage($sourceBitmap, $rect, $rect, [System.Drawing.GraphicsUnit]::Pixel)
            }
            finally { $graphics.Dispose() }
            $temporaryPath = "$TargetPath.first-frame.png"
            $frameBitmap.Save($temporaryPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $frameBitmap.Dispose() }
    }
    finally { $sourceBitmap.Dispose() }
    if ($temporaryPath) { Move-Item -LiteralPath $temporaryPath -Destination $TargetPath -Force }
}

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
        Copy-MinecraftPngFirstFrame -Entry $entries[$item.Value] -TargetPath $targetPath

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
