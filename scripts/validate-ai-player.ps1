[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$HumanExecutablePath,
    [Parameter(Mandatory)][string]$AgentExecutablePath,
    [Parameter(Mandatory)][string]$ProjectPath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateRange(1,65535)][int]$ServerPort = 17350,
    [ValidateRange(30,240)][int]$TimeoutSeconds = 150
)
$ErrorActionPreference = 'Stop'
$humanManifest = & "$PSScriptRoot/assert-demo-player-source.ps1" -ExecutablePath $HumanExecutablePath -ProjectPath $ProjectPath
$agentManifest = & "$PSScriptRoot/assert-demo-player-source.ps1" -ExecutablePath $AgentExecutablePath -ProjectPath $ProjectPath
if (-not $humanManifest.developmentBuild) { throw 'The human diagnostic probe requires a Development Player.' }
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Refusing to overwrite AI validation artifacts.' }
[System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$runId = [Guid]::NewGuid().ToString('N')
$reportPath = Join-Path $OutputDirectory 'human.json'
$humanLog = Join-Path $OutputDirectory 'human.log'
$agentLog = Join-Path $OutputDirectory 'agent.log'
$screenshotPath = Join-Path $OutputDirectory 'human.png'
$humanProcess = $null
$agentProcess = $null
$environmentNames = @('HTTP_PROXY','HTTPS_PROXY','ALL_PROXY','http_proxy','https_proxy','all_proxy',
    'BIOME_RIVALS_NAKAMA_HOST','BIOME_RIVALS_NAKAMA_PORT','BIOME_RIVALS_NAKAMA_SCHEME')
$savedEnvironment = @{}
foreach ($name in $environmentNames) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $null, 'Process') }
    [Environment]::SetEnvironmentVariable('BIOME_RIVALS_NAKAMA_HOST', '127.0.0.1', 'Process')
    [Environment]::SetEnvironmentVariable('BIOME_RIVALS_NAKAMA_PORT', [string]$ServerPort, 'Process')
    [Environment]::SetEnvironmentVariable('BIOME_RIVALS_NAKAMA_SCHEME', 'http', 'Process')
    $agentArgs = @('-batchmode','-nographics','-autoOnline','-aiPlayer',
        '-previewPlayerFaction','plains_forest','-nakamaDeviceId',"ai-agent-$runId",
        '-logFile',('"{0}"' -f $agentLog))
    $humanArgs = @('-batchmode','-screen-width','1920','-screen-height','1080','-autoOnline','-autoOnlineAction',
        '-previewPlayerFaction','desert_badlands','-nakamaDeviceId',"ai-human-$runId",
        '-onlineProbe',('"{0}"' -f $reportPath),'-captureOnline',('"{0}"' -f $screenshotPath),
        '-captureWidth','1920','-captureHeight','1080','-quitAfterOnlineProbe',
        '-logFile',('"{0}"' -f $humanLog))
    $agentProcess = Start-Process -FilePath $AgentExecutablePath -ArgumentList $agentArgs -PassThru -WindowStyle Hidden
    $humanProcess = Start-Process -FilePath $HumanExecutablePath -ArgumentList $humanArgs -PassThru -WindowStyle Hidden
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        $humanProcess.Refresh(); $agentProcess.Refresh()
        if ($agentProcess.HasExited) { throw "Agent Player exited unexpectedly: $($agentProcess.ExitCode)" }
        if ($humanProcess.HasExited) { break }
        Start-Sleep -Milliseconds 500
    }
    $humanProcess.Refresh()
    if (-not $humanProcess.HasExited) { throw 'Human/AI match validation timed out; no service restart attempted.' }
    if ($humanProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $reportPath)) { throw 'Human probe failed.' }
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if (-not $report.ok -or $report.matchStatus -ne 'FINISHED' -or -not $report.performedEndTurn -or
        -not $report.performedAttack -or -not $report.performedConcede -or -not (Test-Path -LiteralPath $screenshotPath)) {
        throw 'The player did not complete a real basic match against the agent.'
    }
    $accepted = @(Select-String -LiteralPath $agentLog -Pattern 'Agent accepted: ([A-Z_]+); revision=([0-9]+)\.')
    if (-not (Select-String -LiteralPath $agentLog -SimpleMatch $report.matchId -Quiet)) {
        throw 'The agent log does not identify the same authoritative match as the human report.'
    }
    $types = @($accepted | ForEach-Object { $_.Matches[0].Groups[1].Value })
    foreach ($required in @('MULLIGAN','ENTER_COMBAT','END_TURN')) {
        if ($types -notcontains $required) { throw "Agent did not receive an authoritative $required acknowledgement." }
    }
    if (Select-String -LiteralPath $agentLog -Pattern 'Agent stopped:|Agent action stopped at revision') {
        throw 'Agent policy or operation execution failed; inspect the retained log.'
    }
    $validation = [ordered]@{
        ok = $true; matchId = $report.matchId; revision = $report.revision
        scenario = 'human-command-probe-vs-independent-agent'; serverPort = $ServerPort
        sourceFileCount = @($agentManifest.inputs).Count; agentDevelopmentBuild = $agentManifest.developmentBuild
        humanExitCode = $humanProcess.ExitCode; agentAcceptedTypes = $types
        agentAcceptedCount = $accepted.Count; agentNaturallyExited = $false
        screenshotSha256 = (Get-FileHash -LiteralPath $screenshotPath).Hash
        humanReportSha256 = (Get-FileHash -LiteralPath $reportPath).Hash
    }
    $validation | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'validation.json') -Encoding utf8
    Write-Output "Human/AI validation passed: $($report.matchId), agent accepted $($accepted.Count) commands. Evidence: $OutputDirectory"
}
finally {
    # Only processes created by this invocation are stopped; server/data remain untouched.
    foreach ($ownedProcess in @($humanProcess,$agentProcess)) {
        if ($null -eq $ownedProcess) { continue }
        $ownedProcess.Refresh()
        if (-not $ownedProcess.HasExited) { Stop-Process -Id $ownedProcess.Id -ErrorAction SilentlyContinue }
    }
    foreach ($name in $environmentNames) {
        if ($null -eq $savedEnvironment[$name]) { Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue }
        else { [Environment]::SetEnvironmentVariable($name, [string]$savedEnvironment[$name], 'Process') }
    }
}
