[CmdletBinding()]
param(
    [string]$MinecraftJar,
    [string]$ManifestPath = 'shared-schema\card-art\card-art-registry.v1.json',
    [string]$SourceConfigPath = 'shared-schema\card-art\minecraft-asset-source.v1.json',
    [string]$OutputDirectory = 'client-unity\Assets\Generated\MinecraftCardIcons'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$manifestFile = Join-Path $repoRoot $ManifestPath
$sourceConfigFile = Join-Path $repoRoot $SourceConfigPath
$outputRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))

if (-not $outputRoot.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Output directory must stay inside the repository: $outputRoot"
}
if (-not (Test-Path -LiteralPath $manifestFile)) { throw "Art manifest not found: $manifestFile" }
if (-not (Test-Path -LiteralPath $sourceConfigFile)) { throw "Source config not found: $sourceConfigFile" }

$manifest = Get-Content -LiteralPath $manifestFile -Raw -Encoding UTF8 | ConvertFrom-Json
$sourceConfig = Get-Content -LiteralPath $sourceConfigFile -Raw -Encoding UTF8 | ConvertFrom-Json

if (-not $MinecraftJar) {
    $versionFolder = [string]$sourceConfig.versionFolder
    $MinecraftJar = Join-Path $env:APPDATA ".minecraft\versions\$versionFolder\$versionFolder.jar"
}
$MinecraftJar = [System.IO.Path]::GetFullPath($MinecraftJar)
if (-not (Test-Path -LiteralPath $MinecraftJar)) { throw "Minecraft client JAR not found: $MinecraftJar" }

$jarHash = (Get-FileHash -LiteralPath $MinecraftJar -Algorithm SHA256).Hash
if ($sourceConfig.validatedJarSha256 -and $jarHash -ne $sourceConfig.validatedJarSha256) {
    Write-Warning "Minecraft JAR hash differs from the validated local source. Paths will still be verified."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($MinecraftJar)
try {
    $zipEntries = @{}
    foreach ($entry in $archive.Entries) { $zipEntries[$entry.FullName] = $entry }

    $seenCards = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($item in $manifest.entries) {
        $cardId = [string]$item.cardId
        if ($cardId -notmatch '^[a-z0-9_]+$') { throw "Unsafe card id in art manifest: $cardId" }
        if (-not $seenCards.Add($cardId)) { throw "Duplicate card id in art manifest: $cardId" }
        if (-not $zipEntries.ContainsKey([string]$item.sourcePath)) {
            throw "Missing JAR entry for ${cardId}: $($item.sourcePath)"
        }
    }

    [System.IO.Directory]::CreateDirectory($outputRoot) | Out-Null
    $provenance = [System.Collections.Generic.List[object]]::new()
    foreach ($item in $manifest.entries) {
        $cardId = [string]$item.cardId
        $sourcePath = [string]$item.sourcePath
        $targetPath = Join-Path $outputRoot "$cardId.png"
        $inputStream = $zipEntries[$sourcePath].Open()
        try {
            $outputStream = [System.IO.File]::Open($targetPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
            try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose() }
        }
        finally { $inputStream.Dispose() }

        $provenance.Add([ordered]@{
            cardId = $cardId
            artKey = [string]$item.artKey
            sourcePath = $sourcePath
            usage = [string]$item.usage
            outputFile = "$cardId.png"
            sha256 = (Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash
        })
    }

    $provenanceDocument = [ordered]@{
        schemaVersion = 1
        generatedAtUtc = [DateTime]::UtcNow.ToString('o')
        sourceJar = $MinecraftJar
        sourceJarSha256 = $jarHash
        sourceGameVersion = [string]$sourceConfig.gameVersion
        redistributionPolicy = [string]$sourceConfig.redistributionPolicy
        entries = $provenance
    }
    $provenanceJson = $provenanceDocument | ConvertTo-Json -Depth 8
    [System.IO.File]::WriteAllText(
        (Join-Path $outputRoot 'asset-provenance.local.json'),
        $provenanceJson + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false))

    Write-Output "Extracted $($provenance.Count) local prototype icons -> $outputRoot"
}
finally {
    $archive.Dispose()
}
