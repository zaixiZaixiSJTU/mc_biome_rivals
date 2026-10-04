[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ExecutablePath,
    [Parameter(Mandatory)][string]$ProjectPath,
    [Parameter(Mandatory)][string]$CecilLibraryPath,
    [ValidateSet('Development','NonDevelopment')][string]$ExpectedMode = 'NonDevelopment'
)

$ErrorActionPreference = 'Stop'
$manifest = & (Join-Path $PSScriptRoot 'assert-demo-player-source.ps1') -ExecutablePath $ExecutablePath -ProjectPath $ProjectPath
$expectedDevelopment = $ExpectedMode -eq 'Development'
if ($manifest.developmentBuild -isnot [bool] -or $manifest.developmentBuild -ne $expectedDevelopment -or
    [string]::IsNullOrWhiteSpace($manifest.buildOptions)) { throw 'Build mode metadata is missing or does not match the requested audit mode.' }
if (-not (Test-Path -LiteralPath $CecilLibraryPath -PathType Leaf)) { throw 'A local Mono.Cecil metadata reader is required.' }
[void][System.Reflection.Assembly]::LoadFrom([System.IO.Path]::GetFullPath($CecilLibraryPath))
$playerPath = [System.IO.Path]::GetFullPath($ExecutablePath)
$managedRoot = Join-Path ([System.IO.Path]::GetDirectoryName($playerPath)) (([System.IO.Path]::GetFileNameWithoutExtension($playerPath)) + '_Data\Managed')
$compiledTypes = @{}
$assemblies = [System.Collections.Generic.List[IDisposable]]::new()
$assemblyHashes = @{}
function Add-CompiledTypes($definitions) {
    foreach ($definition in $definitions) {
        $compiledTypes[$definition.FullName] = $definition
        Add-CompiledTypes $definition.NestedTypes
    }
}
$developmentTypes = @(
    'BiomeRivals.Networking.IMatchInboundDeliveryDiagnostics',
    'BiomeRivals.Networking.IMatchTestFixtureDiagnostics',
    'BiomeRivals.Networking.MatchTestFixtureResult',
    'BiomeRivals.Networking.AuthoritativeMatchGateway/InboundHoldScope',
    'BiomeRivals.Networking.AuthoritativeMatchGateway/HeldInboundMessage',
    'BiomeRivals.Demo.DemoOnlineBuildingProbe',
    'BiomeRivals.Demo.DemoOnlineBuildingReport',
    'BiomeRivals.Demo.DemoOnlineDeploymentAudit',
    'BiomeRivals.Demo.DemoDeploymentRejectionProof',
    'BiomeRivals.Demo.DemoOnlineDrawProbe',
    'BiomeRivals.Demo.DemoOnlineDrawReport',
    'BiomeRivals.Demo.DemoOnlineEndReturnProbe',
    'BiomeRivals.Demo.DemoOnlineEndReturnReport',
    'BiomeRivals.Demo.DemoOnlineGoatProbe',
    'BiomeRivals.Demo.DemoOnlineGoatReport',
    'BiomeRivals.Demo.DemoOnlinePendingProbe',
    'BiomeRivals.Demo.DemoOnlinePendingReport',
    'BiomeRivals.Demo.DemoSceneController/CompatibilityAuditReport',
    'BiomeRivals.Demo.DemoSceneController/CardPaperAuditReport',
    'BiomeRivals.Demo.DemoSceneController/EntityCatalogueReport',
    'BiomeRivals.Demo.DemoSceneController/EntityCatalogueEntry'
)
$productionTypes = @(
    'BiomeRivals.Core.MatchStateStore',
    'BiomeRivals.Networking.AuthoritativeMatchGateway',
    'BiomeRivals.Networking.NakamaMatchTransport',
    'BiomeRivals.Networking.ServerCompatibilityFailure',
    'BiomeRivals.Demo.DemoOnlineMatchSession',
    'BiomeRivals.Demo.DemoSceneController',
    'BiomeRivals.Demo.CardUI',
    'BiomeRivals.Demo.DemoHudTypography',
    'BiomeRivals.Demo.DemoReadableSummary',
    'BiomeRivals.Demo.DemoOnlineFeedback',
    'BiomeRivals.Demo.DemoCardArtProvider',
    'BiomeRivals.Networking.IPlayerOperations',
    'BiomeRivals.Networking.PlayerActionRequest',
    'BiomeRivals.Networking.PlayerObservation',
    'BiomeRivals.Networking.IMatchAgentPolicy',
    'BiomeRivals.Networking.MatchAgentRunner',
    'BiomeRivals.Demo.BasicMatchAgentPolicy'
)
try {
    foreach ($name in @('BiomeRivals.Core','BiomeRivals.Networking','BiomeRivals.Demo')) {
        $path = Join-Path $managedRoot ($name + '.dll')
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Compiled Mono runtime assembly missing: $path. This verifier does not support IL2CPP." }
        $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($path)
        $assemblies.Add($assembly)
        Add-CompiledTypes $assembly.MainModule.Types
        $assemblyHashes[$name] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    }
    foreach ($name in $productionTypes) {
        if (-not $compiledTypes.ContainsKey($name)) { throw "Production runtime type is missing: $name" }
    }
    foreach ($name in $developmentTypes) {
        if ($compiledTypes.ContainsKey($name) -ne $expectedDevelopment) { throw "Compiled development boundary mismatch: $name ($ExpectedMode)" }
    }
    $gateway = $compiledTypes['BiomeRivals.Networking.AuthoritativeMatchGateway']
    $hasHoldMethod = @($gateway.Methods | Where-Object Name -eq 'HoldIncomingMatchMessages').Count -gt 0
    if ($hasHoldMethod -ne $expectedDevelopment) { throw 'Compiled receive-hold method leaked or is missing.' }
    $scene = $compiledTypes['BiomeRivals.Demo.DemoSceneController']
    $animator = $compiledTypes['BiomeRivals.Demo.DemoEntityIdleAnimator']
    if (@($animator.Methods | Where-Object Name -eq 'SampleForAudit').Count -ne [int]$expectedDevelopment) {
        throw 'Deterministic entity sampler leaked or is missing.'
    }
    foreach ($method in @('OpenHandInspection','CloseHandInspection','RefreshHandInspection','OpenChoiceRules','OpenStatusInspection','OpenCardNotes','CloseStatusInspection','RefreshStatusInspection','ShowCompatibilityFailure','ShowOnlineException','SetAgentPolicy')) {
        if (@($scene.Methods | Where-Object Name -eq $method).Count -ne 1) { throw "Production read-only card UI method is missing: $method" }
    }
    $statusCapture = @($scene.Methods | Where-Object Name -eq 'PrepareStatusInspectionCapture')
    if ($statusCapture.Count -ne 1) { throw 'Status capture gate is missing.' }
    $hasStatusIterator = @($statusCapture[0].CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'System.Runtime.CompilerServices.IteratorStateMachineAttribute' }).Count -eq 1
    if ($hasStatusIterator -ne $expectedDevelopment) { throw 'Status capture implementation leaked or is missing for the selected mode.' }
    $feedbackCapture = @($scene.Methods | Where-Object Name -eq 'PrepareOnlineFeedbackCapture')
    if ($feedbackCapture.Count -ne 1) { throw 'Online feedback capture gate is missing.' }
    $hasFeedbackIterator = @($feedbackCapture[0].CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'System.Runtime.CompilerServices.IteratorStateMachineAttribute' }).Count -eq 1
    if ($hasFeedbackIterator -ne $expectedDevelopment) { throw 'Online feedback reading diagnostics leaked or are missing.' }
    foreach ($method in @('PrepareCardPaperCapture','PrepareChoiceRulesCapture')) {
        $capture = @($scene.Methods | Where-Object Name -eq $method)
        if ($capture.Count -ne 1) { throw "Card reading capture gate is missing: $method" }
        $hasIterator = @($capture[0].CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'System.Runtime.CompilerServices.IteratorStateMachineAttribute' }).Count -eq 1
        if ($hasIterator -ne $expectedDevelopment) { throw "Card reading diagnostic boundary failed: $method" }
    }
    if (@($scene.Methods | Where-Object Name -eq 'AuditSceneCardPaper').Count -ne [int]$expectedDevelopment) { throw 'Card paper diagnostic method boundary failed.' }
    $resourceCapture = @($scene.Methods | Where-Object Name -eq 'PrepareHudResourceCapture')
    if ($resourceCapture.Count -ne 1) { throw 'HUD resource capture gate is missing.' }
    $hasResourceIterator = @($resourceCapture[0].CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'System.Runtime.CompilerServices.IteratorStateMachineAttribute' }).Count -eq 1
    if ($hasResourceIterator -ne $expectedDevelopment) { throw 'HUD resource capture implementation leaked or is missing.' }
    foreach ($method in @('SetupHudResourceScenario','AuditHudResourceReading')) {
        if (@($scene.Methods | Where-Object Name -eq $method).Count -ne [int]$expectedDevelopment) { throw "HUD resource diagnostic method boundary mismatch: $method" }
    }
    # All Player modes must resolve packaged art without cwd/source search. Editor-only paths must be compiled out.
    foreach ($name in $compiledTypes.Keys | Where-Object { $_ -eq 'BiomeRivals.Demo.DemoCardArtProvider' -or $_.StartsWith('BiomeRivals.Demo.DemoCardArtProvider/') }) {
        foreach ($method in $compiledTypes[$name].Methods) {
            if (-not $method.HasBody) { continue }
            foreach ($instruction in $method.Body.Instructions) {
                $operand = [string]$instruction.Operand
                if ($operand -eq 'Generated' -or $operand -eq 'Assets' -or $operand -eq 'client-unity' -or
                    $operand.Contains('System.IO.Directory::GetCurrentDirectory')) { throw 'Compiled Player card art still contains source/working-directory fallback.' }
            }
        }
    }
    [pscustomobject]@{
        ok = $true
        mode = $ExpectedMode
        sourceFileCount = $manifest.inputs.Count
        cardArtHasNoSourceFallback = $true
        checkedDevelopmentTypes = $developmentTypes
        checkedProductionTypes = $productionTypes
        assemblyHashes = $assemblyHashes
    }
} finally {
    foreach ($assembly in $assemblies) { $assembly.Dispose() }
}
