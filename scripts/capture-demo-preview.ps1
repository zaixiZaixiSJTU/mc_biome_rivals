[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ExecutablePath,
    [Parameter(Mandatory)][string]$ProjectPath,
    [ValidateSet('plains_forest', 'desert_badlands', 'snow_ice', 'cave_dark_forest', 'ocean_river', 'nether', 'end')]
    [string]$PlayerFaction = 'end',
    [ValidateSet('plains_forest', 'desert_badlands', 'snow_ice', 'cave_dark_forest', 'ocean_river', 'nether', 'end')]
    [string]$OpponentFaction = 'nether',
    [ValidateSet('standard_meadow', 'plains_sunrise', 'deep_caverns', 'nether_lava_sea', 'end_void', 'deep_ocean', 'desert_storm')]
    [string]$PreviewArena = 'standard_meadow',
    [switch]$PreviewHandHover,
    [switch]$PreviewUnaffordableCardSelection,
    [switch]$PreviewFullHand,
    [switch]$PreviewFullHandCombat,
    [switch]$PreviewHandInspection,
    [switch]$PreviewResponsiveHandInspection,
    [switch]$PreviewStatusInspection,
    [switch]$PreviewHudResources,
    [switch]$PreviewCardNotes,
    [ValidateSet('opponent', 'win', 'loss')][string]$PreviewHandState,
    [switch]$PreviewGroundReturnPulse,
    [switch]$PreviewWoodlandRally,
    [switch]$PreviewSummonReadiness,
    [switch]$PreviewEndReturnInteraction,
    [switch]$PreviewCombatInteraction,
    [switch]$PreviewCaveSpiderPoison,
    [switch]$PreviewGuardianPose,
    [switch]$PreviewBabySheepPose,
    [switch]$PreviewBlazePose,
    [switch]$PreviewStructureDragDeployment,
    [switch]$PreviewCraftingInteraction,
    [switch]$PreviewCardArrival,
    [switch]$PreviewChoiceInteraction,
    [switch]$PreviewArchaeologyChoice,
    [switch]$PreviewAttackFeedback,
    [switch]$PreviewButtonFeedback,
    [switch]$PreviewOpponentEnergy,
    [switch]$PreviewMatchOutcome,
    [switch]$PreviewTerminalWorld,
    [switch]$PreviewTntTrapOwnerWins,
    [switch]$PreviewOnlineStatus,
    [switch]$PreviewPolarBearWool,
    [switch]$PreviewPolarBearPose,
    [switch]$PreviewTurtlePose,
    [switch]$PreviewDesertVillagerSurface,
    [switch]$PreviewDarknessTargeting,
    [switch]$PreviewTurnBanner,
    [string]$CapturePath,
    [ValidateRange(320, 7680)][int]$CaptureWidth = 1920,
    [ValidateRange(320, 7680)][int]$CaptureHeight = 1080,
    [int]$TimeoutSeconds = 60
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$executablePath = [System.IO.Path]::GetFullPath($ExecutablePath)
$projectPath = [System.IO.Path]::GetFullPath($ProjectPath)
if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "Windows Demo Player was not found: $executablePath"
}
if (-not (Test-Path -LiteralPath $projectPath -PathType Container)) {
    throw "Unity project was not found: $projectPath"
}
if ($TimeoutSeconds -lt 1 -or $TimeoutSeconds -gt 600) {
    throw 'TimeoutSeconds must be between 1 and 600.'
}
$previewCount = 0
if ($PreviewGuardianPose) { $previewCount++ }
if ($PreviewBabySheepPose) { $previewCount++ }
if ($PreviewBlazePose) { $previewCount++ }
if ($PreviewCaveSpiderPoison) { $previewCount++ }
if ($PreviewPolarBearPose) { $previewCount++ }
if ($PreviewTurtlePose) { $previewCount++ }
if ($PreviewDesertVillagerSurface) { $previewCount++ }
if ($PreviewFullHandCombat) { $previewCount++ }
if ($PreviewHandInspection) { $previewCount++ }
if ($PreviewResponsiveHandInspection) { $previewCount++ }
if ($PreviewStatusInspection) { $previewCount++ }
if ($PreviewHudResources) { $previewCount++ }
if ($PreviewCardNotes) { $previewCount++ }
if ($PreviewHandState) { $previewCount++ }
foreach ($previewMode in @($PreviewHandHover, $PreviewUnaffordableCardSelection, $PreviewFullHand, $PreviewGroundReturnPulse, $PreviewWoodlandRally, $PreviewSummonReadiness, $PreviewEndReturnInteraction, $PreviewCombatInteraction, $PreviewStructureDragDeployment, $PreviewCraftingInteraction, $PreviewCardArrival, $PreviewChoiceInteraction, $PreviewArchaeologyChoice, $PreviewAttackFeedback, $PreviewButtonFeedback, $PreviewOpponentEnergy, $PreviewMatchOutcome, $PreviewTerminalWorld, $PreviewTntTrapOwnerWins, $PreviewOnlineStatus, $PreviewPolarBearWool, $PreviewDarknessTargeting, $PreviewTurnBanner)) {
    if ($previewMode) { $previewCount++ }
}
if ($previewCount -gt 1) {
    throw 'Choose one deterministic preview mode per capture.'
}
if ($PreviewBabySheepPose -and ($PlayerFaction -ne 'plains_forest' -or $OpponentFaction -ne 'plains_forest')) {
    throw 'PreviewBabySheepPose requires plains_forest on both sides.'
}
if ($PreviewGuardianPose -and ($PlayerFaction -ne 'ocean_river' -or $OpponentFaction -ne 'ocean_river')) {
    throw 'PreviewGuardianPose requires ocean_river on both sides.'
}
if ($PreviewBlazePose -and ($PlayerFaction -ne 'nether' -or $OpponentFaction -ne 'nether')) {
    throw 'PreviewBlazePose requires nether on both sides.'
}
if ($PreviewCaveSpiderPoison -and ($PlayerFaction -ne 'cave_dark_forest' -or $OpponentFaction -ne 'snow_ice')) {
    throw 'PreviewCaveSpiderPoison requires cave_dark_forest vs snow_ice.'
}
if ($PreviewPolarBearPose -and ($PlayerFaction -ne 'snow_ice' -or $OpponentFaction -ne 'snow_ice')) {
    throw 'PreviewPolarBearPose requires snow_ice on both sides.'
}
if ($PreviewTurtlePose -and ($PlayerFaction -ne 'ocean_river' -or $OpponentFaction -ne 'ocean_river')) {
    throw 'PreviewTurtlePose requires ocean_river on both sides.'
}
if ($PreviewDesertVillagerSurface -and ($PlayerFaction -ne 'desert_badlands' -or $OpponentFaction -ne 'desert_badlands')) {
    throw 'PreviewDesertVillagerSurface requires desert_badlands on both sides.'
}
if ($PreviewHandState -and ($PlayerFaction -ne 'end' -or $OpponentFaction -ne 'nether')) {
    throw 'PreviewHandState requires end vs nether; the local visual fixture selects these registered themes.'
}
if ($PreviewCombatInteraction -and ($PlayerFaction -ne 'plains_forest' -or $OpponentFaction -ne 'desert_badlands')) {
    throw 'PreviewCombatInteraction requires -PlayerFaction plains_forest and -OpponentFaction desert_badlands.'
}
if ($PreviewAttackFeedback -and ($PlayerFaction -ne 'plains_forest' -or $OpponentFaction -ne 'desert_badlands')) {
    throw 'PreviewAttackFeedback requires -PlayerFaction plains_forest and -OpponentFaction desert_badlands.'
}
if ($PreviewMatchOutcome -and ($PlayerFaction -ne 'ocean_river' -or $OpponentFaction -ne 'desert_badlands')) {
    throw 'PreviewMatchOutcome requires -PlayerFaction ocean_river and -OpponentFaction desert_badlands.'
}
if ($PreviewTerminalWorld -and ($PlayerFaction -ne 'ocean_river' -or $OpponentFaction -ne 'snow_ice')) {
    throw 'PreviewTerminalWorld requires -PlayerFaction ocean_river and -OpponentFaction snow_ice.'
}
if ($PreviewTntTrapOwnerWins -and $PlayerFaction -ne 'desert_badlands') {
    throw 'PreviewTntTrapOwnerWins requires -PlayerFaction desert_badlands.'
}
if ($PreviewStructureDragDeployment -and $PlayerFaction -ne 'desert_badlands') {
    throw 'PreviewStructureDragDeployment requires -PlayerFaction desert_badlands.'
}
if ($PreviewCraftingInteraction -and $PlayerFaction -ne 'desert_badlands') {
    throw 'PreviewCraftingInteraction requires -PlayerFaction desert_badlands.'
}
if ($PreviewCardArrival -and $PlayerFaction -ne 'plains_forest') {
    throw 'PreviewCardArrival requires -PlayerFaction plains_forest.'
}
if ($PreviewChoiceInteraction -and ($PlayerFaction -ne 'cave_dark_forest' -or $OpponentFaction -ne 'plains_forest')) {
    throw 'PreviewChoiceInteraction requires -PlayerFaction cave_dark_forest and -OpponentFaction plains_forest.'
}
if ($PreviewArchaeologyChoice -and $PlayerFaction -ne 'desert_badlands') {
    throw 'PreviewArchaeologyChoice requires -PlayerFaction desert_badlands.'
}
if ($PreviewWoodlandRally -and ($PreviewArena -ne 'deep_caverns' -or $PlayerFaction -ne 'plains_forest')) {
    throw 'PreviewWoodlandRally requires -PreviewArena deep_caverns and -PlayerFaction plains_forest.'
}
if ($PreviewSummonReadiness -and ($PlayerFaction -ne 'cave_dark_forest' -or $OpponentFaction -ne 'nether' -or
    ($PreviewArena -and $PreviewArena -ne 'standard_meadow'))) {
    throw 'PreviewSummonReadiness requires cave_dark_forest vs nether on standard_meadow.'
}

$manifestPath = "$executablePath.build-manifest.json"
$buildManifest = & (Join-Path $PSScriptRoot 'assert-demo-player-source.ps1') -ExecutablePath $executablePath -ProjectPath $projectPath
if (($PreviewHudResources -or $PreviewCardNotes) -and ($buildManifest.developmentBuild -isnot [bool] -or -not $buildManifest.developmentBuild)) {
    throw 'HUD resource and card-notes captures require an explicitly verified Development Player.'
}
if ($PreviewStatusInspection -and ($buildManifest.developmentBuild -isnot [bool] -or -not $buildManifest.developmentBuild)) {
    throw 'PreviewStatusInspection requires an explicitly verified Development Player.'
}
if ($PreviewBabySheepPose -and ($buildManifest.developmentBuild -isnot [bool] -or -not $buildManifest.developmentBuild)) {
    throw 'PreviewBabySheepPose requires an explicitly verified Development Player.'
}
if ($PreviewGuardianPose -and ($buildManifest.developmentBuild -isnot [bool] -or -not $buildManifest.developmentBuild)) {
    throw 'PreviewGuardianPose requires an explicitly verified Development Player.'
}
if ($PreviewBlazePose -and ($buildManifest.developmentBuild -isnot [bool] -or -not $buildManifest.developmentBuild)) {
    throw 'PreviewBlazePose requires an explicitly verified Development Player.'
}
if ($PreviewCaveSpiderPoison -and ($buildManifest.developmentBuild -isnot [bool] -or -not $buildManifest.developmentBuild)) {
    throw 'PreviewCaveSpiderPoison requires an explicitly verified Development Player.'
}
if ($PreviewTurtlePose -and ($buildManifest.developmentBuild -isnot [bool] -or -not $buildManifest.developmentBuild)) {
    throw 'PreviewTurtlePose requires an explicitly verified Development Player.'
}
if ($PreviewDesertVillagerSurface -and ($buildManifest.developmentBuild -isnot [bool] -or -not $buildManifest.developmentBuild)) {
    throw 'PreviewDesertVillagerSurface requires an explicitly verified Development Player.'
}

if (-not $CapturePath) {
    $CapturePath = Join-Path $repoRoot "Temp\DemoAcceptance\demo-$PlayerFaction-1920x1080.png"
}
$CapturePath = [System.IO.Path]::GetFullPath($CapturePath)
if ([System.IO.Path]::GetExtension($CapturePath) -ine '.png') {
    throw "CapturePath must end in .png: $CapturePath"
}
$captureDirectory = Split-Path -Parent $CapturePath
[System.IO.Directory]::CreateDirectory($captureDirectory) | Out-Null
$logPath = [System.IO.Path]::ChangeExtension($CapturePath, '.player.log')
$reportPath = [System.IO.Path]::ChangeExtension($CapturePath, '.json')
$existingOutputs = @($CapturePath, $logPath, $reportPath) | Where-Object { Test-Path -LiteralPath $_ }
if ($existingOutputs.Count -gt 0) {
    throw "Refusing to overwrite existing capture artifacts: $($existingOutputs -join ', ')"
}

$quotedCapturePath = '"{0}"' -f $CapturePath
$quotedLogPath = '"{0}"' -f $logPath
$arguments = @(
    '-screen-width', [string]$CaptureWidth,
    '-screen-height', [string]$CaptureHeight,
    '-screen-fullscreen', '0',
    '-previewPlayerFaction', $PlayerFaction,
    '-previewOpponentFaction', $OpponentFaction,
    '-previewArena', $PreviewArena,
    '-captureDemo', $quotedCapturePath,
    '-captureWidth', [string]$CaptureWidth,
    '-captureHeight', [string]$CaptureHeight,
    '-logFile', $quotedLogPath
)
if ($PreviewHandHover) { $arguments += '-previewHandHover' }
if ($PreviewUnaffordableCardSelection) { $arguments += '-previewUnaffordableCardSelection' }
if ($PreviewFullHand) { $arguments += '-previewFullHand' }
if ($PreviewFullHandCombat) { $arguments += @('-previewFullHand', '-previewFullHandCombat') }
if ($PreviewHandInspection) { $arguments += @('-previewFullHand', '-previewFullHandCombat', '-previewHandInspection') }
if ($PreviewResponsiveHandInspection) { $arguments += @('-previewFullHand', '-previewFullHandCombat', '-previewResponsiveHandInspection') }
if ($PreviewStatusInspection) { $arguments += '-previewStatusInspection' }
if ($PreviewHudResources) { $arguments += '-previewHudResources' }
if ($PreviewCardNotes) { $arguments += '-previewCardNotes' }
if ($PreviewHandState) { $arguments += @('-previewHandState', $PreviewHandState) }
if ($PreviewGroundReturnPulse) { $arguments += '-previewGroundReturnPulse' }
if ($PreviewWoodlandRally) { $arguments += '-previewWoodlandRally' }
if ($PreviewSummonReadiness) { $arguments += '-previewSummonReadiness' }
if ($PreviewEndReturnInteraction) { $arguments += '-previewEndReturnInteraction' }
if ($PreviewCombatInteraction) { $arguments += '-previewCombatInteraction' }
if ($PreviewCaveSpiderPoison) { $arguments += '-previewCaveSpiderPoison' }
if ($PreviewGuardianPose) { $arguments += '-previewGuardianPose' }
if ($PreviewBabySheepPose) { $arguments += '-previewBabySheepPose' }
if ($PreviewBlazePose) { $arguments += '-previewBlazePose' }
if ($PreviewStructureDragDeployment) { $arguments += '-previewStructureDragDeployment' }
if ($PreviewCraftingInteraction) { $arguments += '-previewCraftingInteraction' }
if ($PreviewCardArrival) { $arguments += '-previewCardArrival' }
if ($PreviewChoiceInteraction) { $arguments += '-previewChoiceInteraction' }
if ($PreviewArchaeologyChoice) { $arguments += '-previewArchaeology' }
if ($PreviewAttackFeedback) { $arguments += '-previewAttackFeedback' }
if ($PreviewButtonFeedback) { $arguments += '-previewButtonFeedback' }
if ($PreviewOpponentEnergy) { $arguments += '-previewOpponentEnergy' }
if ($PreviewMatchOutcome) { $arguments += '-previewMatchOutcome' }
if ($PreviewTerminalWorld) { $arguments += '-previewTerminalWorld' }
if ($PreviewTntTrapOwnerWins) { $arguments += '-previewTntTrapOwnerWins' }
if ($PreviewOnlineStatus) { $arguments += '-previewOnlineStatus' }
if ($PreviewPolarBearWool) { $arguments += '-previewPolarBearWool' }
if ($PreviewPolarBearPose) { $arguments += '-previewPolarBearPose' }
if ($PreviewTurtlePose) { $arguments += '-previewTurtlePose' }
if ($PreviewDesertVillagerSurface) { $arguments += '-previewDesertVillagerSurface' }
if ($PreviewDarknessTargeting) { $arguments += '-previewDarkness' }
if ($PreviewTurnBanner) { $arguments += '-previewTurnBanner' }
$process = Start-Process -FilePath $executablePath -ArgumentList $arguments `
    -WorkingDirectory (Split-Path -Parent $executablePath) -WindowStyle Hidden -PassThru
try {
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
        throw "Demo screenshot capture timed out after $TimeoutSeconds seconds. See $logPath"
    }
    $playerExitCode = $process.ExitCode
    if ($playerExitCode -ne 0) {
        throw "Demo Player exited with code $playerExitCode. See $logPath"
    }
}
finally {
    $process.Dispose()
}

if (-not (Test-Path -LiteralPath $CapturePath -PathType Leaf)) {
    throw "Demo Player exited successfully but did not create the screenshot: $CapturePath"
}
if (-not (Test-Path -LiteralPath $logPath -PathType Leaf)) {
    throw "Demo Player did not create its requested log: $logPath"
}
$log = Get-Content -LiteralPath $logPath -Raw -Encoding UTF8
if (($PreviewHudResources -or $PreviewCardNotes) -and -not $log.Contains('HUD resource reading settled: True; equipped trident; temporary +2; buried 10; actual notes open/return; unchanged revision/hand; input restored.')) {
    throw 'HUD resource/card-notes capture requires actual rule state, reading clicks, unchanged gameplay and input restoration.'
}
if ($PreviewStatusInspection -and -not $log.Contains('Status inspection settled: True; actual entry/scroll/return/reopen; full message; unchanged revision/hand; gameplay locked.')) {
    throw 'Status inspection must prove actual entry/scroll/return/reopen and immutable gameplay before screenshot acceptance.'
}
if ($PreviewBabySheepPose -and (-not $log.Contains('Baby sheep interaction preview settled: True (hand UI + original slot deployment/attacker/target raycasts).') -or
    -not $log.Contains('Baby sheep pose audit: True (both sides, 6 original cubes, source proportions/texture, dye mask, idle and original slots).'))) {
    throw 'Baby sheep source pose/UI interaction audit did not pass.'
}
if ($PreviewBlazePose -and (-not $log.Contains('Blaze interaction preview settled: True (hand UI + original slot deployment/attacker/target raycasts).') -or
    -not $log.Contains('Blaze pose audit: True (both sides, 13 original cubes, three moving source rings, material and original slots).'))) {
    throw 'Blaze source pose/UI interaction audit did not pass.'
}
if ($PreviewGuardianPose -and (-not $log.Contains('Guardian interaction preview settled: True (hand UI + original slot deployment/attacker/target raycasts).') -or
    -not $log.Contains('Guardian pose audit: True (both sides, 22 original cubes, eye/tail/spikes, idle/material and original slots).'))) {
    throw 'Both-side guardian source pose and UI interaction audit did not pass.'
}
if ($PreviewCaveSpiderPoison -and -not $log.Contains('Spider interaction preview settled: True (hand UI + original 3D slot deployment/attacker/target raycasts + poison).')) {
    throw 'Spider UI/deployment/attack/poison preview did not pass.'
}
if (-not $log.Contains("Demo screenshot saved: $CapturePath")) {
    throw "Player log does not confirm the requested screenshot path: $logPath"
}
if ($PreviewOnlineStatus -and -not $log.Contains('Online status preview settled: reconnect attempt 999; factionSelectorsLocked=True; opponentSelectorsLocked=True; labelsMuted=True')) {
    throw "Player did not reach the reconnect UI preview with both faction selectors visibly locked: $logPath"
}
if ($PreviewTurtlePose -and -not $log.Contains('Turtle source binding audit: True (both Minecraft models, shell horizontal, children upright, original slots raycast).')) {
    throw 'Both-side turtle source-binding audit did not settle; inspect the actual Player log.'
}
if ($PreviewDesertVillagerSurface -and -not $log.Contains('Desert villager surface audit: True (both sides, skin plus biome, idle and original slot raycast).')) {
    throw 'Both-side villager surface audit did not pass.'
}
if ($PreviewPolarBearPose -and (-not $log.Contains('Polar bear pose preview settled: True (both sides, Minecraft models, idle retained).') -or
    -not $log.Contains('Polar bear runtime audit: True (both initial facings retained; original unit slots raycast).'))) {
    throw 'Both-side polar bear pose preview did not settle; inspect the actual Player log.'
}
if (($PreviewPolarBearWool -or $PreviewPolarBearPose) -and -not $log.Contains('Polar bear wool preview settled: True (attack 4/health 7; remaining Wool selected; label 3 lines, text height 54).')) {
    throw "Player did not complete the Polar Bear + Wool modifier and multi-line nameplate preview: $logPath"
}
if ($PreviewDarknessTargeting -and -not $log.Contains('Darkness preview settled: True (two exact Suspicious Sand instances consumed; DARK active; Snowball target selected).')) {
    throw "Player did not consume both exact Suspicious Sand instances and open the Snowball target-selection state: $logPath"
}
if ($PreviewHandHover -and -not $log.Contains('Hand hover preview settled: True')) {
    throw "Player did not reach the expected 1.22 hand-hover state before capture: $logPath"
}
if ($PreviewUnaffordableCardSelection -and -not $log.Contains('Unaffordable card inspection preview settled: True')) {
    throw "Player did not select and inspect an unaffordable card without spending energy: $logPath"
}
if (($PreviewFullHand -or $PreviewFullHandCombat) -and -not $log.Contains('Full hand layout preview settled: True (cards=7, registered long titles clear cost sockets, outer cards inside stone hand plate, larger 166x216 layout).')) {
    throw "Player did not fit all seven hand cards inside the stone hand plate: $logPath"
}
if ($PreviewFullHandCombat -and -not $log.Contains('Full hand readability preview settled: True (state=combat; cards=7; input locked=True).')) {
    throw "Player did not retain readable information and locked input during actual combat: $logPath"
}
if ($PreviewHandInspection -and -not $log.Contains('Hand inspection preview settled: True (cards=7; last=ed_008; full rules; actual UI clicks; gameplay locked).')) {
    throw "Player did not read the long description through actual read-only UI controls with gameplay locked: $logPath"
}
if ($PreviewResponsiveHandInspection) {
    foreach ($size in @('1424x714','1024x768','1280x720','1920x1080',"${CaptureWidth}x${CaptureHeight}")) {
        if (-not $log.Contains("Responsive hand inspection settled: True; screen=$size;")) { throw "Actual responsive hand UI did not settle at $size." }
    }
}
if ($PreviewHandState -and -not $log.Contains("Hand state readability preview settled: True (state=$PreviewHandState; cards=7; read-only UI opened/paginated/closed; gameplay locked).")) {
    throw "Player did not preserve the requested actual local hand state and input locks: $logPath"
}
if ($PreviewFullHand -and -not $log.Contains('Full hand readability preview settled: True (state=active; cards=7; input locked=False).')) {
    throw "Player did not retain readable information and active hand input: $logPath"
}
if ($PreviewGroundReturnPulse -and -not $log.Contains('Ground return pulse preview settled: True')) {
    throw "Player did not find an empty player-side unit slot for the ground-pulse preview: $logPath"
}
if ($PreviewWoodlandRally -and -not $log.Contains('Woodland rally preview settled: True (unit slots=5; summoned=tk_004; slot=3; drew=pf_001; drawn card details refreshed=True).')) {
    throw "Player did not complete Woodland Rally's summon-then-draw chain through the only empty deep-caverns slot or leave details on the drawn card: $logPath"
}
if ($PreviewSummonReadiness -and -not $log.Contains('Summon readiness preview settled: True (mansion=等待己方回合; fortress=结束阶段就绪; opponent redstone=1).')) {
    throw 'Summon readiness preview did not settle through legal deployment and turn resolution.'
}
if ($PreviewEndReturnInteraction -and -not $log.Contains('End return interaction preview settled: True (3D slot raycast + pointer down/up + UI raycast gate + scene callbacks; returned discount 2).')) {
    throw "Player did not complete the end-biome 3D/UI raycasts, pointer down/up, deploy, cast, and target interaction with the expected discount: $logPath"
}
if ($PreviewCombatInteraction -and -not $log.Contains('Combat interaction preview settled: True (click-to-select fallback + invalid drop preserved hand/energy + hand drag-and-drop deploy + turn buttons + attacker/target raycasts; round 2, energy 7, attacker health 1).')) {
    throw "Player did not complete the local deploy, turn cycle, attacker/target selection, and combat resolution: $logPath"
}
if ($PreviewStructureDragDeployment -and -not $log.Contains('Structure drag deployment preview settled: True (building slots 2-3 occupied, slot 1 empty, energy 0).')) {
    throw "Player did not complete the two-cell structure drag deployment with correct occupied range and energy: $logPath"
}
if ($PreviewCraftingInteraction -and -not $log.Contains('Crafting interaction preview settled: True (GraphicRaycaster selected crafting payment; 3D pointer deployed a 10/10 temple into building slots 1-2; materials discarded; energy 1/1; buried=2).')) {
    throw "Player did not select crafting payment, consume its exact materials, and deploy the crafted structure through UI/3D input: $logPath"
}
if ($PreviewCardArrival -and -not $log.Contains('Hand card arrival preview settled: True (new instance faded in; existing instance stayed still; input restored).')) {
    throw "Player did not complete the new-hand-card arrival animation and input restoration: $logPath"
}
if ($PreviewChoiceInteraction -and -not $log.Contains('Choice interaction preview settled: True (GraphicRaycaster click selected the private top card; choice remains pending).')) {
    throw "Player did not complete the private top-card choice interaction through the UI event system: $logPath"
}
if ($PreviewArchaeologyChoice -and -not $log.Contains('Archaeology three-card choice preview settled: True (GraphicRaycaster selected option 2; options=3; rendered=3; kind=ARCHAEOLOGY_TOP_3).')) {
    throw "Player did not render and select the expected three-card archaeology choice: $logPath"
}
if ($PreviewAttackFeedback -and -not $log.Contains('Attack feedback preview settled: True (golem health 5, target alive False, phase Combat).')) {
    throw "Player did not resolve the attack feedback preview to the expected surviving attacker and defeated target: $logPath"
}
if ($PreviewAttackFeedback -and -not $log.Contains('Local action status hides internal revision: True.')) {
    throw "Player exposed an internal local-match revision in its player-facing status: $logPath"
}
if ($PreviewButtonFeedback -and -not $log.Contains('Button feedback preview pressed state: True')) {
    throw "Player did not reach the expected animated button pressed state before capture: $logPath"
}
if ($PreviewOpponentEnergy -and -not $log.Contains("Opponent energy preview settled: True (round 2; opponent energy 2/2; HUD '敌方红石 ◆ 2/2').")) {
    throw "Player did not render the expected authoritative local opponent energy state before capture: $logPath"
}
if ($PreviewTurnBanner -and -not $log.Contains('Turn banner motion preview settled: True')) {
    throw "Player did not capture the expected visible snapped turn-banner motion frame: $logPath"
}
if ($PreviewMatchOutcome -and -not $log.Contains('Match outcome preview settled: True (result=win; player life 30; opponent life 0).')) {
    throw "Player did not end the match with the expected hero attack and winner projection: $logPath"
}
if ($PreviewTerminalWorld -and (-not $log.Contains('Match outcome preview settled: True (result=win; player life 30; opponent life 0).') -or
    -not $log.Contains('Terminal world preview settled: True (reactive buildings remained; gameplay highlights cleared; pointer disabled).'))) {
    throw "Terminal-world preview did not clear gameplay cues after a legal lethal hero attack: $logPath"
}
if ($PreviewTntTrapOwnerWins -and -not $log.Contains('DB-007 simultaneous lethal preview settled: True (result=win; player life 0; opponent life 0).')) {
    throw "Player did not award the simultaneous TNT lethal to the buried-card owner: $logPath"
}
if ($log -match '(?m)(NullReferenceException|MissingReferenceException|Fatal Error|Crash!!!)') {
    throw "Player log contains a runtime failure. See $logPath"
}

$stream = [System.IO.File]::OpenRead($CapturePath)
try {
    $header = [byte[]]::new(24)
    $read = $stream.Read($header, 0, $header.Length)
}
finally {
    $stream.Dispose()
}
$pngSignature = [byte[]](137, 80, 78, 71, 13, 10, 26, 10)
$validPng = $read -eq 24
for ($index = 0; $validPng -and $index -lt $pngSignature.Length; $index++) {
    if ($header[$index] -ne $pngSignature[$index]) { $validPng = $false }
}
if (-not $validPng) {
    throw "Screenshot is not a valid PNG: $CapturePath"
}
$widthBytes = [byte[]]$header[16..19]
$heightBytes = [byte[]]$header[20..23]
[Array]::Reverse($widthBytes)
[Array]::Reverse($heightBytes)
$width = [BitConverter]::ToInt32($widthBytes, 0)
$height = [BitConverter]::ToInt32($heightBytes, 0)
if ($width -ne $CaptureWidth -or $height -ne $CaptureHeight) {
    throw "Screenshot dimensions are ${width}x${height}; expected ${CaptureWidth}x${CaptureHeight}."
}

$hash = (Get-FileHash -LiteralPath $CapturePath -Algorithm SHA256).Hash
$previewLabel = if ($PreviewTurnBanner) { 'turn-banner-slide-in' } elseif ($PreviewOpponentEnergy) { 'opponent-energy-hud' } elseif ($PreviewButtonFeedback) { 'button-pressed-feedback' } elseif ($PreviewUnaffordableCardSelection) { 'unaffordable-card-inspection' } elseif ($PreviewHandHover) { 'hand-card-hover' } elseif ($PreviewFullHand) { 'full-hand-layout' } elseif ($PreviewGroundReturnPulse) { 'ground-return-pulse' } elseif ($PreviewWoodlandRally) { 'deep-caverns-woodland-rally' } elseif ($PreviewSummonReadiness) { 'summon-readiness' } elseif ($PreviewEndReturnInteraction) { 'end-return-interaction' } elseif ($PreviewCombatInteraction) { 'combat-interaction' } elseif ($PreviewStructureDragDeployment) { 'structure-drag-deployment' } elseif ($PreviewCraftingInteraction) { 'crafting-interaction' } elseif ($PreviewCardArrival) { 'hand-card-arrival' } elseif ($PreviewChoiceInteraction) { 'card-choice-interaction' } elseif ($PreviewArchaeologyChoice) { 'archaeology-choice' } elseif ($PreviewAttackFeedback) { 'attack-feedback' } elseif ($PreviewTerminalWorld) { 'terminal-world-lock' } elseif ($PreviewMatchOutcome) { 'match-outcome' } elseif ($PreviewTntTrapOwnerWins) { 'db007-simultaneous-lethal' } elseif ($PreviewOnlineStatus) { 'long-online-status' } elseif ($PreviewPolarBearWool) { 'polar-bear-wool-modifiers' } elseif ($PreviewDarknessTargeting) { 'darkness-targeting' } else { 'default' }
if ($PreviewPolarBearPose) { $previewLabel = 'polar-bear-binding-pose' }
if ($PreviewTurtlePose) { $previewLabel = 'turtle-source-binding-pose' }
if ($PreviewFullHandCombat) { $previewLabel = 'full-hand-combat-readability' }
if ($PreviewHandInspection) { $previewLabel = 'read-only-hand-inspection' }
if ($PreviewResponsiveHandInspection) { $previewLabel = 'responsive-read-only-hand-inspection' }
if ($PreviewStatusInspection) { $previewLabel = 'read-only-status-inspection' }
if ($PreviewHudResources) { $previewLabel = 'equipped-temporary-buried-hud' }
if ($PreviewCardNotes) { $previewLabel = 'read-only-card-notes' }
if ($PreviewHandState) { $previewLabel = "local-hand-state-$PreviewHandState" }
if ($PreviewCaveSpiderPoison) { $previewLabel = 'spider-ui-deployment-attack-poison' }
if ($PreviewGuardianPose) { $previewLabel = 'guardian-source-pose-ui-interaction' }
if ($PreviewBabySheepPose) { $previewLabel = 'baby-sheep-source-pose-ui-interaction' }
if ($PreviewBlazePose) { $previewLabel = 'blaze-source-orbit-ui-interaction' }
$manifest = [ordered]@{
    playerFaction = $PlayerFaction
    opponentFaction = $OpponentFaction
    arenaId = $PreviewArena
    preview = $previewLabel
    unityVersion = $buildManifest.unityVersion
    builtAtUtc = $buildManifest.builtAtUtc
    projectPath = $projectPath
    buildManifestPath = $manifestPath
    sourceFileCount = $buildManifest.inputs.Count
    width = $width
    height = $height
    playerExitCode = $playerExitCode
    screenshotPath = $CapturePath
    screenshotSha256 = $hash
    playerLogPath = $logPath
}
[System.IO.File]::WriteAllText(
    $reportPath,
    (($manifest | ConvertTo-Json -Depth 4) + [Environment]::NewLine),
    [System.Text.UTF8Encoding]::new($false)
)
Write-Output "Demo screenshot validation passed: ${width}x${height}, SHA-256 $hash"
Write-Output "Screenshot: $CapturePath"
Write-Output "Report: $reportPath"
