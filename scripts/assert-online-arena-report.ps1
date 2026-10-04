[CmdletBinding()]
param(
    [Parameter(Mandatory)]$ProbeA,
    [Parameter(Mandatory)]$ProbeB,
    [Parameter(Mandatory)][ValidateSet('standard_meadow','plains_sunrise','deep_caverns','nether_lava_sea','end_void','deep_ocean','desert_storm')]
    [string]$ExpectedArena,
    [switch]$RequireUiDeployment
)
$ErrorActionPreference = 'Stop'
$units = $(if ($ExpectedArena -eq 'deep_caverns') { 5 } elseif ($ExpectedArena -in @('nether_lava_sea','end_void')) { 3 } else { 4 })
foreach ($probe in @($ProbeA,$ProbeB)) {
    if ($probe.arenaId -ne $ExpectedArena -or $probe.unitSlotCount -ne $units -or
        $probe.buildingSlotCount -ne 7 - $units -or $probe.arenaUiVerified -isnot [bool] -or $probe.arenaUiVerified -ne $true) {
        throw 'Unity reports do not verify the expected authoritative arena geometry.'
    }
}
if ($RequireUiDeployment) {
    $deployed = @(@($ProbeA,$ProbeB) | Where-Object { $_.performedDeploy -is [bool] -and $_.performedDeploy })
    if ($deployed.Count -lt 1) { throw 'No client verified a UI deployment.' }
    foreach ($probe in @($ProbeA,$ProbeB)) {
        if ($probe.performedDeploy -isnot [bool]) { throw 'UI deployment report lacks an explicit deployment result.' }
        $isIntegerIndex = $probe.firstUiDeploySlotIndex -is [int] -or $probe.firstUiDeploySlotIndex -is [long]
        if (-not $probe.performedDeploy) {
            if ($probe.performedUiDeploy -isnot [bool] -or $probe.performedUiDeploy -or -not $isIntegerIndex -or
                $probe.firstUiDeploySlotIndex -ne -1) { throw 'Non-deploying client has inconsistent UI proof.' }
            continue
        }
        if ($probe.performedUiDeploy -isnot [bool] -or $probe.performedUiDeploy -ne $true -or
            -not $isIntegerIndex -or $probe.firstUiDeploySlotIndex -ne $units - 1) {
            throw 'A deployed unit bypassed the UI or did not exercise the final authoritative cell.'
        }
    }
}
