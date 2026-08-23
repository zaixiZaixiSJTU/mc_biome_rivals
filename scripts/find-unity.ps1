[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$candidates = [System.Collections.Generic.List[string]]::new()

$hubRoots = @(
    (Join-Path $env:ProgramFiles 'Unity\Hub\Editor'),
    (Join-Path ${env:ProgramFiles(x86)} 'Unity\Hub\Editor'),
    'C:\Unity\Hub\Editor',
    'D:\Unity\Hub\Editor',
    'E:\Unity\Hub\Editor'
)

foreach ($root in $hubRoots) {
    if (-not (Test-Path -LiteralPath $root)) { continue }
    Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        $editor = Join-Path $_.FullName 'Editor\Unity.exe'
        if (Test-Path -LiteralPath $editor) { $candidates.Add($editor) }
    }
}

$registryRoots = @(
    'HKCU:\Software\Unity Technologies\Installer',
    'HKLM:\SOFTWARE\Unity Technologies\Installer',
    'HKLM:\SOFTWARE\WOW6432Node\Unity Technologies\Installer'
)
foreach ($root in $registryRoots) {
    if (-not (Test-Path $root)) { continue }
    foreach ($key in Get-ChildItem -Path $root -ErrorAction SilentlyContinue) {
        $properties = Get-ItemProperty -Path $key.PSPath -ErrorAction SilentlyContinue
        $locations = @($properties.Location, $properties.'Location x64') | Where-Object { $_ }
        foreach ($location in $locations) {
            $editor = Join-Path $location 'Editor\Unity.exe'
            if (Test-Path -LiteralPath $editor) { $candidates.Add($editor) }
        }
    }
}

$results = $candidates | Sort-Object -Unique
if (-not $results) {
    Write-Warning 'Unity.exe was not found in common Hub paths or the registry. Install Unity 6000.0.28f1c1, then open client-unity.'
    exit 1
}

$results | ForEach-Object { Write-Output $_ }
