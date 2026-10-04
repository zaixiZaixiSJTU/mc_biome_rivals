[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$tempRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot ('Temp\CardFrameSyncTests-' + [guid]::NewGuid().ToString('N'))))
if (-not $tempRoot.StartsWith(($repoRoot.TrimEnd('\') + '\'), [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Card-frame test directory must stay inside the repository: $tempRoot"
}

[System.IO.Directory]::CreateDirectory($tempRoot) | Out-Null
$source = Join-Path $tempRoot 'source.png'
$targetDirectory = Join-Path $tempRoot 'missing-target-directory'
$target = Join-Path $targetDirectory 'target.png'
$sourceRelative = [System.IO.Path]::GetRelativePath($repoRoot, $source)
$targetRelative = [System.IO.Path]::GetRelativePath($repoRoot, $target)
$syncScript = Join-Path $PSScriptRoot 'sync-card-frame-study.ps1'
try {
    [System.IO.File]::WriteAllText($source, 'canonical image bytes')
    [System.IO.Directory]::CreateDirectory($targetDirectory) | Out-Null
    [System.IO.File]::WriteAllText($target, 'canonical image bytes')
    & $syncScript -SourcePath $sourceRelative -TargetPath $targetRelative -Check
    if (-not $?) { throw 'Current card-frame copy was rejected.' }

    [System.IO.File]::WriteAllText($target, 'stale image bytes')
    $staleBefore = [System.IO.File]::ReadAllText($target)
    $rejectedStale = $false
    try { & $syncScript -SourcePath $sourceRelative -TargetPath $targetRelative -Check }
    catch { $rejectedStale = $_.Exception.Message.Contains('is stale') }
    if (-not $rejectedStale) { throw 'Read-only card-frame check did not reject a stale copy.' }
    if ([System.IO.File]::ReadAllText($target) -cne $staleBefore) {
        throw 'Read-only card-frame check modified the stale target.'
    }

    Remove-Item -LiteralPath $target
    Remove-Item -LiteralPath $targetDirectory
    $rejectedMissing = $false
    try { & $syncScript -SourcePath $sourceRelative -TargetPath $targetRelative -Check }
    catch { $rejectedMissing = $_.Exception.Message.Contains('is missing') }
    if (-not $rejectedMissing) { throw 'Read-only card-frame check did not reject a missing copy.' }
    if (Test-Path -LiteralPath $target) { throw 'Read-only card-frame check recreated a missing target.' }
    if (Test-Path -LiteralPath $targetDirectory) { throw 'Read-only card-frame check recreated the missing target directory.' }

    Write-Output 'Card-frame sync check is read-only and rejects stale or missing copies.'
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force
}
