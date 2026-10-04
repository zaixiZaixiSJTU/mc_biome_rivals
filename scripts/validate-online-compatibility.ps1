[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ExecutablePath,
    [Parameter(Mandatory)][string]$ProjectPath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$ServerHost = '127.0.0.1',
    [ValidateRange(1,65535)][int]$ServerPort = 18350,
    [ValidateSet('http','https')][string]$ServerScheme = 'http',
    [ValidateRange(320,7680)][int]$CaptureWidth = 1280,
    [ValidateRange(320,7680)][int]$CaptureHeight = 720,
    [ValidateRange(10,180)][int]$TimeoutSeconds = 90
)

$ErrorActionPreference = 'Stop'
$manifest = & (Join-Path $PSScriptRoot 'assert-demo-player-source.ps1') -ExecutablePath $ExecutablePath -ProjectPath $ProjectPath
if ($manifest.developmentBuild -isnot [bool] -or -not $manifest.developmentBuild) {
    throw 'Expected-negative compatibility UI validation requires a verified Development Player.'
}
if ([string]::IsNullOrWhiteSpace($ServerHost)) { throw 'ServerHost must be set.' }
$proofRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $proofRoot) { throw 'Refusing to overwrite compatibility evidence.' }
New-Item -ItemType Directory -Path $proofRoot | Out-Null
$reportPath = Join-Path $proofRoot 'report.json'
$capturePath = Join-Path $proofRoot 'failure.png'
$logPath = Join-Path $proofRoot 'player.log'
$variables = @('HTTP_PROXY','HTTPS_PROXY','ALL_PROXY','BIOME_RIVALS_NAKAMA_HOST','BIOME_RIVALS_NAKAMA_PORT','BIOME_RIVALS_NAKAMA_SCHEME')
$previous = @{}
foreach ($variable in $variables) { $previous[$variable] = [Environment]::GetEnvironmentVariable($variable, 'Process') }
$process = $null
try {
    foreach ($variable in @('HTTP_PROXY','HTTPS_PROXY','ALL_PROXY')) { [Environment]::SetEnvironmentVariable($variable, $null, 'Process') }
    [Environment]::SetEnvironmentVariable('BIOME_RIVALS_NAKAMA_HOST', $ServerHost, 'Process')
    [Environment]::SetEnvironmentVariable('BIOME_RIVALS_NAKAMA_PORT', [string]$ServerPort, 'Process')
    [Environment]::SetEnvironmentVariable('BIOME_RIVALS_NAKAMA_SCHEME', $ServerScheme, 'Process')
    $arguments = @('-batchmode','-screen-width',"$CaptureWidth",'-screen-height',"$CaptureHeight",'-autoOnline',
        '-onlineExpectedCompatibilityFailure','-quitAfterOnlineProbe','-nakamaDeviceId',('compatibility-reading-' + [Guid]::NewGuid().ToString('N')),
        '-onlineProbe',('"{0}"' -f $reportPath),'-captureOnline',('"{0}"' -f $capturePath),
        '-captureWidth',"$CaptureWidth",'-captureHeight',"$CaptureHeight",'-logFile',('"{0}"' -f $logPath))
    $process = Start-Process -FilePath ([IO.Path]::GetFullPath($ExecutablePath)) -ArgumentList $arguments -PassThru -WindowStyle Hidden
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while (-not $process.HasExited -and [DateTime]::UtcNow -lt $deadline) { [void]$process.WaitForExit(5000) }
    if (-not $process.HasExited) { throw 'Expected-negative compatibility Player exceeded its deadline; see preserved player.log.' }
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Compatibility Player exited $($process.ExitCode); see preserved player.log." }
    $report = Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($field in @('success','authoritativeStateAbsent','realtimeSocketAbsent','matchmakerAbsent','matchAbsent',
        'localStateUnchanged','headerLayoutValid','hintLayoutValid','fullReadingValid','fullReadingReadOnly','fullReadingReturnValid','fullReadingScrollValid')) {
        if ($report.$field -isnot [bool] -or -not $report.$field) { throw "Compatibility evidence failed or is missing: $field" }
    }
    if ($report.canSendCommands -isnot [bool] -or $report.canSendCommands -or
        $report.canIssueCommand -isnot [bool] -or $report.canIssueCommand -or $report.phase -ne 'Failed' -or
        $report.localRevisionBefore -ne $report.localRevisionAfter) { throw 'Compatibility command/state gate failed.' }
    $expectedEndpoint = [UriBuilder]::new($ServerScheme, $ServerHost, $ServerPort).Uri.GetLeftPart([UriPartial]::Authority)
    if ($report.endpoint -ne $expectedEndpoint -or $report.screenWidth -ne $CaptureWidth -or $report.screenHeight -ne $CaptureHeight) {
        throw 'Compatibility endpoint or actual window dimensions do not match this run.'
    }
    Add-Type -AssemblyName System.Drawing
    $bitmap = [Drawing.Bitmap]::new($capturePath)
    try { if ($bitmap.Width -ne $CaptureWidth -or $bitmap.Height -ne $CaptureHeight) { throw 'Compatibility screenshot dimensions differ.' } }
    finally { $bitmap.Dispose() }
    [pscustomobject]@{ ok = $true; kind = $report.kind; endpoint = $report.endpoint;
        sourceFileCount = $manifest.inputs.Count; exitCode = $process.ExitCode;
        readingScrollRequired = $report.fullReadingScrollRequired;
        reportSha256 = (Get-FileHash -LiteralPath $reportPath -Algorithm SHA256).Hash;
        screenshotSha256 = (Get-FileHash -LiteralPath $capturePath -Algorithm SHA256).Hash }
}
finally {
    if ($process) {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
        $process.Dispose()
    }
    foreach ($variable in $variables) { [Environment]::SetEnvironmentVariable($variable, $previous[$variable], 'Process') }
}
