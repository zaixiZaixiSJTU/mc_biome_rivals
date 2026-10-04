[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ExecutablePath,
    [Parameter(Mandatory)][string]$ProjectPath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateRange(1,600)][int]$TimeoutSeconds = 120
)
$ErrorActionPreference = 'Stop'
$executable = [IO.Path]::GetFullPath($ExecutablePath)
$output = [IO.Path]::GetFullPath($OutputDirectory)
$manifest = & (Join-Path $PSScriptRoot 'assert-demo-player-source.ps1') -ExecutablePath $executable -ProjectPath $ProjectPath
if ($manifest.developmentBuild -isnot [bool] -or -not $manifest.developmentBuild) {
    throw 'Catalogue requires a verified Development Player.'
}
if (Test-Path -LiteralPath $output) { throw 'Catalogue output must be a new directory; nothing will be overwritten.' }
$logPath = "$output.player.log"
if (Test-Path -LiteralPath $logPath) { throw 'Catalogue log already exists.' }
$arguments = @('-screen-fullscreen','0','-screen-width','512','-screen-height','512',
    '-captureEntityCatalogue', ('"' + $output + '"'), '-logFile', ('"' + $logPath + '"'))
$process = Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory (Split-Path -Parent $executable) -WindowStyle Hidden -PassThru
try {
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
        throw "Catalogue timed out: $logPath"
    }
    if ($process.ExitCode -ne 0) { throw "Catalogue Player exit $($process.ExitCode): $logPath" }
} finally { $process.Dispose() }
$reportPath = Join-Path $output 'catalogue.json'
if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) { throw 'No catalogue report.' }
$report = Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8 | ConvertFrom-Json
$entries = @($report.entries)
if ($report.schemaVersion -ne 1 -or $report.tileSize -ne 512 -or $entries.Count -ne 144) { throw 'Unexpected catalogue schema/coverage.' }
function Assert-PngDimensions([string]$Path, [int]$Width, [int]$Height) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 24 -or [BitConverter]::ToString($bytes,0,8) -ne '89-50-4E-47-0D-0A-1A-0A') { throw "Invalid PNG: $Path" }
    $actualWidth = $bytes[16] * 16777216 + $bytes[17] * 65536 + $bytes[18] * 256 + $bytes[19]
    $actualHeight = $bytes[20] * 16777216 + $bytes[21] * 65536 + $bytes[22] * 256 + $bytes[23]
    if ($actualWidth -ne $Width -or $actualHeight -ne $Height) { throw "Unexpected PNG dimensions: $Path" }
}
$groups = @($entries | Group-Object cardId)
if ($groups.Count -ne 36) { throw 'Not all 36 registrations were rendered.' }
foreach ($group in $groups) {
    if ($group.Count -ne 4) { throw "Incorrect view count: $($group.Name)" }
    foreach ($side in @($true,$false)) {
        foreach ($phase in @(0,0.23)) {
            $views = @($group.Group | Where-Object { $_.player -eq $side -and [Math]::Abs($_.idleSeconds - $phase) -lt 0.00001 })
            if ($views.Count -ne 1) { throw "Duplicate or missing view: $($group.Name)" }
        }
    }
}
foreach ($entry in $entries) {
    if ([string]::IsNullOrWhiteSpace($entry.cardName) -or [string]::IsNullOrWhiteSpace($entry.geometryId) -or @($entry.textures).Count -eq 0) { throw 'Incomplete source mapping.' }
    if ([IO.Path]::GetFileName($entry.image) -ne $entry.image) { throw 'Unsafe image path in report.' }
    $imagePath = Join-Path $output $entry.image
    if (-not (Test-Path -LiteralPath $imagePath -PathType Leaf) -or (Get-FileHash -LiteralPath $imagePath -Algorithm SHA256).Hash -ne $entry.imageSha256) { throw "Image hash mismatch: $imagePath" }
    Assert-PngDimensions $imagePath 512 512
}
$pageHashes = @{}
foreach ($page in 1..6) {
    $pagePath = Join-Path $output "page-$page.png"
    Assert-PngDimensions $pagePath 2048 3072
    $pageHashes["page-$page.png"] = (Get-FileHash -LiteralPath $pagePath -Algorithm SHA256).Hash
}
$log = Get-Content -LiteralPath $logPath -Raw -Encoding UTF8
if (-not $log.Contains('Entity catalogue completed: 36 registrations, 144 renders.')) { throw 'No Player completion marker.' }
[pscustomobject]@{ ok = $true; registrations = 36; renders = 144; sourceFileCount = $manifest.inputs.Count; output = $output; reportSha256 = (Get-FileHash -LiteralPath $reportPath -Algorithm SHA256).Hash; pageHashes = $pageHashes }
