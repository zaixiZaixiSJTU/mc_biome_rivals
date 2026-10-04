[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ExecutablePath,
    [Parameter(Mandatory)][string]$ProjectPath
)

$ErrorActionPreference = 'Stop'
$playerPath = [System.IO.Path]::GetFullPath($ExecutablePath)
$sourceProjectPath = [System.IO.Path]::GetFullPath($ProjectPath).TrimEnd([char[]]@('\', '/'))
if (-not (Test-Path -LiteralPath $playerPath -PathType Leaf)) { throw "Windows Demo Player was not found: $playerPath" }
$manifestPath = "$playerPath.build-manifest.json"
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Player build manifest is missing; rebuild this Player with DemoBuildAutomation: $manifestPath"
}
try {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
} catch { throw "Player build manifest is unreadable: $manifestPath" }
if ($manifest.schemaVersion -ne 1 -or $manifest.buildTarget -ne 'StandaloneWindows64' -or
    [string]::IsNullOrWhiteSpace([string]$manifest.unityVersion) -or $null -eq $manifest.inputs) {
    throw "Player build manifest has an unsupported or incomplete schema: $manifestPath"
}
$currentPaths = [System.Collections.Generic.List[string]]::new()
foreach ($rootName in @('Assets', 'ProjectSettings', 'Packages')) {
    $sourceRoot = Join-Path $sourceProjectPath $rootName
    if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container)) { throw "Unity source directory is missing: $sourceRoot" }
    foreach ($sourceFile in Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Force) {
        $currentPaths.Add($sourceFile.FullName.Substring($sourceProjectPath.Length + 1).Replace('\', '/'))
    }
}
$actualPaths = [string[]]$currentPaths.ToArray()
$recordedPaths = [string[]]@($manifest.inputs | ForEach-Object { [string]$_.path })
[Array]::Sort($actualPaths, [System.StringComparer]::Ordinal)
[Array]::Sort($recordedPaths, [System.StringComparer]::Ordinal)
if ($actualPaths.Length -eq 0 -or $actualPaths.Length -ne $recordedPaths.Length) {
    throw "Player is stale: current Unity project has $($actualPaths.Length) source files, manifest records $($recordedPaths.Length). Rebuild it."
}
for ($index = 0; $index -lt $actualPaths.Length; $index++) {
    if ($actualPaths[$index] -cne $recordedPaths[$index]) { throw "Player source file set differs near '$($actualPaths[$index])'. Rebuild it." }
}
$sourcePrefix = $sourceProjectPath + [System.IO.Path]::DirectorySeparatorChar
foreach ($input in $manifest.inputs) {
    $sourcePath = [System.IO.Path]::GetFullPath((Join-Path $sourceProjectPath ([string]$input.path).Replace('/', [System.IO.Path]::DirectorySeparatorChar)))
    if (-not $sourcePath.StartsWith($sourcePrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Player build manifest contains a path outside the Unity project: $($input.path)"
    }
    if ((Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash -ine [string]$input.sha256) {
        throw "Player is stale: source file changed after build: $($input.path). Rebuild it."
    }
}
return $manifest
