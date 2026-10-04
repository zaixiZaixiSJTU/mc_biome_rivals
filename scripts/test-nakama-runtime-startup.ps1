[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$configuration = & docker compose -f (Join-Path $repoRoot 'docker-compose.yml') config --format json
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve Compose startup contract.' }
$configuration = ($configuration -join [Environment]::NewLine) | ConvertFrom-Json
$entrypoint = $configuration.services.nakama.entrypoint[2]
$migrationIndex = $entrypoint.IndexOf('/nakama/nakama migrate up', [StringComparison]::Ordinal)
if ($migrationIndex -lt 1) { throw 'Runtime guard is missing from the actual Compose entrypoint.' }
$guard = $entrypoint.Substring(0,$migrationIndex)
$sourceModule = Join-Path $repoRoot 'server-nakama\build\index.js'
if (-not (Test-Path -LiteralPath $sourceModule -PathType Leaf)) { throw 'Build the real module before the positive startup test.' }
$evidenceRoot = Join-Path $repoRoot ('Temp\NakamaStartupGuard-' + [Guid]::NewGuid().ToString('N'))
$results = @()
foreach ($case in @('missing','empty','populated')) {
    $fixture = Join-Path $evidenceRoot $case
    [void][IO.Directory]::CreateDirectory($fixture)
    if ($case -eq 'empty') { [IO.File]::WriteAllBytes((Join-Path $fixture 'index.js'), [byte[]]@()) }
    if ($case -eq 'populated') { Copy-Item -LiteralPath $sourceModule -Destination (Join-Path $fixture 'index.js') }
    # Execute only the real pre-migration guard, with no network, database, ports, or writes.
    $output = & docker run --rm --network none --read-only --mount "type=bind,source=$fixture,target=/nakama/data/modules,readonly" --entrypoint /bin/sh $configuration.services.nakama.image -ec $guard 2>&1
    $exitCode = $LASTEXITCODE
    $expected = $(if ($case -eq 'populated') { 0 } else { 1 })
    if ($exitCode -ne $expected) { throw "Runtime startup guard failed $case ($exitCode vs $expected)." }
    $results += [pscustomobject]@{ case=$case; exitCode=$exitCode; output=($output -join [Environment]::NewLine) }
}
[IO.File]::WriteAllText((Join-Path $evidenceRoot 'results.json'), ($results | ConvertTo-Json -Depth 4))
Write-Output "Nakama startup guard: 1 positive / 2 negative cases passed. Generated evidence retained: $evidenceRoot"
