[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$validator = Join-Path $PSScriptRoot 'assert-online-arena-report.ps1'
foreach ($id in @('standard_meadow','plains_sunrise','deep_caverns','nether_lava_sea','end_void','deep_ocean','desert_storm')) {
    $units = $(if ($id -eq 'deep_caverns') { 5 } elseif ($id -in @('nether_lava_sea','end_void')) { 3 } else { 4 })
    $probe = [pscustomobject]@{ arenaId = $id; unitSlotCount = $units; buildingSlotCount = 7 - $units; arenaUiVerified = $true }
    & $validator -ProbeA $probe -ProbeB $probe -ExpectedArena $id
    $probe | Add-Member -NotePropertyName performedDeploy -NotePropertyValue $true
    $probe | Add-Member -NotePropertyName performedUiDeploy -NotePropertyValue $true
    $probe | Add-Member -NotePropertyName firstUiDeploySlotIndex -NotePropertyValue ($units - 1)
    & $validator -ProbeA ($probe | ConvertTo-Json | ConvertFrom-Json) -ProbeB $probe -ExpectedArena $id -RequireUiDeployment
}
$base = [pscustomobject]@{ arenaId = 'deep_caverns'; unitSlotCount = 5; buildingSlotCount = 2; arenaUiVerified = $true }
$controls = @(
    @{ field = 'arenaId'; value = 'standard_meadow' },
    @{ field = 'arenaId'; value = $null },
    @{ field = 'unitSlotCount'; value = 4 },
    @{ field = 'unitSlotCount'; value = $null },
    @{ field = 'buildingSlotCount'; value = 3 },
    @{ field = 'buildingSlotCount'; value = $null },
    @{ field = 'arenaUiVerified'; value = $false },
    @{ field = 'arenaUiVerified'; value = $null },
    @{ field = 'arenaUiVerified'; value = 'true' }
)
foreach ($side in @('a','b')) {
    foreach ($control in $controls) {
        $changed = $base | ConvertTo-Json | ConvertFrom-Json
        $changed.($control.field) = $control.value
        $a = $(if ($side -eq 'a') { $changed } else { $base })
        $b = $(if ($side -eq 'b') { $changed } else { $base })
        $rejected = $false
        try { & $validator -ProbeA $a -ProbeB $b -ExpectedArena deep_caverns }
        catch { $rejected = $true }
        if (-not $rejected) { throw "Arena report negative control accepted: $side/$($control.field)" }
    }
}
$uiBase = [pscustomobject]@{ arenaId = 'deep_caverns'; unitSlotCount = 5; buildingSlotCount = 2; arenaUiVerified = $true; performedDeploy = $true; performedUiDeploy = $true; firstUiDeploySlotIndex = 4 }
$uiControls = @(
    @{ field = 'performedDeploy'; value = $null },
    @{ field = 'performedDeploy'; value = 'true' },
    @{ field = 'performedUiDeploy'; value = $false },
    @{ field = 'performedUiDeploy'; value = $null },
    @{ field = 'performedUiDeploy'; value = 'true' },
    @{ field = 'firstUiDeploySlotIndex'; value = 3 },
    @{ field = 'firstUiDeploySlotIndex'; value = $null },
    @{ field = 'firstUiDeploySlotIndex'; value = '4' }
)
foreach ($side in @('a','b')) {
    foreach ($control in $uiControls) {
        $changed = $uiBase | ConvertTo-Json | ConvertFrom-Json
        $changed.($control.field) = $control.value
        $a = $(if ($side -eq 'a') { $changed } else { $uiBase })
        $b = $(if ($side -eq 'b') { $changed } else { $uiBase })
        $rejected = $false
        try { & $validator -ProbeA $a -ProbeB $b -ExpectedArena deep_caverns -RequireUiDeployment }
        catch { $rejected = $true }
        if (-not $rejected) { throw "UI deployment negative control accepted: $side/$($control.field)" }
    }
}
$idle = $uiBase | ConvertTo-Json | ConvertFrom-Json
$idle.performedDeploy = $false; $idle.performedUiDeploy = $false; $idle.firstUiDeploySlotIndex = -1
& $validator -ProbeA $uiBase -ProbeB $idle -ExpectedArena deep_caverns -RequireUiDeployment
$rejected = $false
try { & $validator -ProbeA $idle -ProbeB $idle -ExpectedArena deep_caverns -RequireUiDeployment }
catch { $rejected = $true }
if (-not $rejected) { throw 'UI validator accepted two non-deploying clients.' }
Write-Output 'Online arena report validator: 15 positive / 35 negative controls passed.'
