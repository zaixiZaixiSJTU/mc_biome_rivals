[CmdletBinding()]
param(
    [string]$MinecraftJar,
    [string]$SourceConfigPath = 'shared-schema\card-art\bedrock-entity-source.v1.json',
    [string]$JavaSourceConfigPath = 'shared-schema\card-art\minecraft-asset-source.v1.json',
    [string]$OutputDirectory = 'client-unity\Assets\Generated\MinecraftWorldTextures\Resources\DemoWorld',
    # Partial extraction is staged separately; never replace a full provenance ledger.
    [string[]]$OnlyKeys
)
# Extracts vanilla entity geometry (minecraft:geometry JSON) and matched entity
# textures from Mojang's official public bedrock-samples repository, plus the
# wool overlay texture from the locally owned Java Edition JAR. Extracted files
# are build-time inputs only and must never be committed to Git.

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$sourceConfigFile = Join-Path $repoRoot $SourceConfigPath
$javaSourceConfigFile = Join-Path $repoRoot $JavaSourceConfigPath
$outputRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))

if (-not $outputRoot.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Output directory must stay inside the repository: $outputRoot"
}
if (-not (Test-Path -LiteralPath $sourceConfigFile)) { throw "Entity source config not found: $sourceConfigFile" }
$sourceConfig = Get-Content -LiteralPath $sourceConfigFile -Raw -Encoding UTF8 | ConvertFrom-Json
if ($OnlyKeys) {
    foreach ($key in $OnlyKeys) {
        if ($key -notmatch '^[a-z0-9_]+$' -or
            -not $sourceConfig.entityGeometry.PSObject.Properties[$key] -or
            -not $sourceConfig.entityTextures.PSObject.Properties[$key]) {
            throw "Partial extraction requires a registered geometry/texture pair: $key"
        }
    }
    if (Test-Path -LiteralPath $outputRoot) {
        throw 'Partial extraction requires a new staging directory; existing assets/provenance will not be overwritten.'
    }
}

$commit = [string]$sourceConfig.pinnedCommit
if ($commit -notmatch '^[0-9a-f]{40}$') { throw "Pinned bedrock-samples commit is not a full SHA: $commit" }
$rawBase = "https://raw.githubusercontent.com/Mojang/bedrock-samples/$commit"

function Save-UrlWithRetry([string]$Url, [string]$TargetPath) {
    $attempts = 3
    for ($attempt = 1; $attempt -le $attempts; $attempt++) {
        try {
            Invoke-WebRequest -Uri $Url -OutFile $TargetPath -UseBasicParsing | Out-Null
            return
        }
        catch {
            if ($attempt -eq $attempts) { throw }
            Write-Warning "Download failed (attempt $attempt/$attempts): $Url"
            Start-Sleep -Seconds 2
        }
    }
}

function Get-HttpFileHash([string]$Url) {
    $tempFile = [System.IO.Path]::GetTempFileName()
    try {
        Invoke-WebRequest -Uri $Url -OutFile $tempFile -UseBasicParsing | Out-Null
        return (Get-FileHash -LiteralPath $tempFile -Algorithm SHA256).Hash
    }
    finally { Remove-Item -LiteralPath $tempFile -ErrorAction SilentlyContinue }
}

[System.IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$modelRoot = Join-Path $outputRoot 'entity_models'
[System.IO.Directory]::CreateDirectory($modelRoot) | Out-Null

$provenance = [System.Collections.Generic.List[object]]::new()

foreach ($item in $sourceConfig.entityGeometry.PSObject.Properties) {
    $key = $item.Name
    if ($OnlyKeys -and $key -notin $OnlyKeys) { continue }
    if ($key -notmatch '^[a-z0-9_]+$') { throw "Unsafe entity model key: $key" }
    $sourcePath = [string]$item.Value
    if ($sourcePath -notmatch '^[A-Za-z0-9_.\-]+\.geo\.json$') { throw "Unsafe geometry path for ${key}: $sourcePath" }
    $url = "$rawBase/$([string]$sourceConfig.geometryRoot)/$sourcePath"
    $targetPath = Join-Path $modelRoot "$key.json"
    Save-UrlWithRetry $url $targetPath
    $provenance.Add([ordered]@{
        key = $key
        kind = 'geometry'
        sourceUrl = $url
        outputFile = "entity_models/$key.json"
        sha256 = (Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash
    })
}

foreach ($item in $sourceConfig.entityTextures.PSObject.Properties) {
    $key = $item.Name
    if ($OnlyKeys -and $key -notin $OnlyKeys) { continue }
    if ($key -notmatch '^[a-z0-9_]+$') { throw "Unsafe entity texture key: $key" }
    $sourcePath = [string]$item.Value
    if ($sourcePath -notmatch '^[A-Za-z0-9_./\-]+$') { throw "Unsafe texture path for ${key}: $sourcePath" }
    $extension = [System.IO.Path]::GetExtension($sourcePath).ToLowerInvariant()
    if ($extension -notin @('.png', '.tga')) { throw "Unsupported entity texture format for ${key}: $sourcePath" }
    $url = "$rawBase/$([string]$sourceConfig.textureRoot)/$sourcePath"
    $targetPath = Join-Path $outputRoot "$key$extension"
    Save-UrlWithRetry $url $targetPath
    $provenance.Add([ordered]@{
        key = $key
        kind = 'texture'
        sourceUrl = $url
        outputFile = "$key$extension"
        sha256 = (Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash
    })
}

if ($sourceConfig.localJavaJarExtras -and -not $OnlyKeys) {
    $javaSourceConfig = Get-Content -LiteralPath $javaSourceConfigFile -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $MinecraftJar) {
        $versionFolder = [string]$javaSourceConfig.versionFolder
        $MinecraftJar = Join-Path $env:APPDATA ".minecraft\versions\$versionFolder\$versionFolder.jar"
    }
    $MinecraftJar = [System.IO.Path]::GetFullPath($MinecraftJar)
    if (Test-Path -LiteralPath $MinecraftJar) {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [System.IO.Compression.ZipFile]::OpenRead($MinecraftJar)
        try {
            $entries = @{}
            foreach ($entry in $archive.Entries) { $entries[$entry.FullName] = $entry }
            foreach ($item in $sourceConfig.localJavaJarExtras.PSObject.Properties) {
                $key = $item.Name
                if ($key -notmatch '^[a-z0-9_]+$') { throw "Unsafe Java extra key: $key" }
                $sourcePath = [string]$item.Value
                if (-not $entries.ContainsKey($sourcePath)) { throw "Missing local JAR texture for ${key}: $sourcePath" }
                $targetPath = Join-Path $outputRoot "$key.png"
                $inputStream = $entries[$sourcePath].Open()
                try {
                    $outputStream = [System.IO.File]::Open($targetPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
                    try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose() }
                }
                finally { $inputStream.Dispose() }
                $provenance.Add([ordered]@{
                    key = $key
                    kind = 'texture'
                    sourceUrl = "local-jar:$MinecraftJar#$sourcePath"
                    outputFile = "$key.png"
                    sha256 = (Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash
                })
            }
        }
        finally { $archive.Dispose() }
    }
    else {
        Write-Warning "Local Minecraft JAR not found; skipping wool overlay texture: $MinecraftJar"
    }
}

$document = [ordered]@{
    schemaVersion = 1
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    sourceRepository = [string]$sourceConfig.sourceRepository
    sourceCommit = $commit
    redistributionPolicy = [string]$sourceConfig.redistributionPolicy
    entries = $provenance
}
[System.IO.File]::WriteAllText(
    (Join-Path $outputRoot 'entity-asset-provenance.local.json'),
    ($document | ConvertTo-Json -Depth 8) + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))

Write-Output "Extracted $($provenance.Count) entity model/texture files -> $outputRoot"
