[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$engine = Get-Content -LiteralPath (Join-Path $repoRoot 'server-nakama/src/rules/match-engine.ts') -Raw -Encoding UTF8
$router = Get-Content -LiteralPath (Join-Path $repoRoot 'server-nakama/src/matches/biome-rivals-match.ts') -Raw -Encoding UTF8
$feedback = Get-Content -LiteralPath (Join-Path $repoRoot 'client-unity/Assets/Game/Demo/Runtime/DemoOnlineFeedback.cs') -Raw -Encoding UTF8
$registrationPattern = 'new Reason\("(?<code>[A-Z_]+)",\s*"(?<source>(?:\\.|[^"\\])*)",\s*"(?<text>(?:\\.|[^"\\])*)"\)'

function Assert-FeedbackContract([string]$EngineText, [string]$RouterText, [string]$FeedbackText) {
    $expected = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $calls = [regex]::Matches($EngineText, "reject\(state,\s*'(?<code>[A-Z_]+)',\s*(?<expression>[\s\S]*?)\);")
    if ($calls.Count -ne [regex]::Matches($EngineText, "reject\(state,\s*'").Count) { throw 'Unparsed rule rejection call.' }
    $diagnosticCalls = 0
    foreach ($call in $calls) {
        $code = $call.Groups['code'].Value
        $expression = $call.Groups['expression'].Value.Trim()
        $literal = [regex]::Match($expression, "\A'(?<value>(?:\\.|[^'\\])*)'\z")
        $messages = @()
        if ($literal.Success) { $messages = @($literal.Groups['value'].Value.Replace("\'", "'")) }
        elseif ($code -eq 'INVALID_STATE' -and $expression -match "\A(?:nextViolations|violations)\.join\('; '\)\z") {
            # Invariant dumps are deliberately diagnostic-only, not gameplay reasons.
            $diagnosticCalls++
            continue
        }
        elseif ($code -eq 'INVALID_COMMAND' -and $expression -eq "command.type + ' requires a valid handCardInstanceId'") {
            $messages = @('DEPLOY_CARD requires a valid handCardInstanceId', 'PLAY_CARD requires a valid handCardInstanceId')
        }
        elseif ($code -eq 'INVALID_TARGET' -and $expression.Contains('?')) {
            $branches = [regex]::Matches($expression, "(?:\?|:)\s*'(?<value>(?:\\.|[^'\\])*)'")
            if ($branches.Count -ne 3) { throw 'Target rejection branches changed; register and review every player reason.' }
            $messages = @($branches | ForEach-Object { $_.Groups['value'].Value.Replace("\'", "'") })
        }
        else { throw "Unreviewed dynamic rule rejection expression: $code" }
        foreach ($message in $messages) { [void]$expected.Add($code + '|' + $message) }
    }
    if ($diagnosticCalls -ne 2) { throw 'Invariant diagnostic boundary changed; review explicitly.' }
    foreach ($pair in [regex]::Matches($RouterText, "code:\s*'(?<code>[A-Z_]+)',\s*message:\s*'(?<value>(?:\\.|[^'\\])*)'")) {
        [void]$expected.Add($pair.Groups['code'].Value + '|' + $pair.Groups['value'].Value.Replace("\'", "'"))
    }
    $registered = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($entry in [regex]::Matches($FeedbackText, $registrationPattern)) {
        $key = $entry.Groups['code'].Value + '|' + [regex]::Unescape($entry.Groups['source'].Value)
        if (-not $registered.Add($key)) { throw 'Duplicate player reason registration.' }
        if ($entry.Groups['text'].Value -notmatch '[\u4e00-\u9fff]') { throw 'Player reason has no Chinese explanation.' }
    }
    if (-not $registered.SetEquals($expected)) {
        $missing = @($expected | Where-Object { -not $registered.Contains($_) }).Count
        $stale = @($registered | Where-Object { -not $expected.Contains($_) }).Count
        throw "Player reason contract drift: $missing missing / $stale stale registrations."
    }
    return $registered.Count
}

$count = Assert-FeedbackContract $engine $router $feedback
$firstRegistration = [regex]::Match($feedback, $registrationPattern).Value
$missing = [regex]::new($registrationPattern).Replace($feedback, '', 1)
$controls = @(
    @{ Engine = $engine; Feedback = $missing },
    @{ Engine = $engine; Feedback = $feedback + "`n" + $firstRegistration },
    @{ Engine = $engine + "`nreject(state, 'INVALID_TARGET', 'new unregistered reason');"; Feedback = $feedback },
    @{ Engine = $engine + "`nreject(state, 'INVALID_TARGET', unknownReason());"; Feedback = $feedback }
)
foreach ($control in $controls) {
    $rejected = $false
    try { [void](Assert-FeedbackContract $control.Engine $router $control.Feedback) }
    catch { $rejected = $true }
    if (-not $rejected) { throw 'Feedback contract validator accepted a negative control.' }
}
Write-Output "Player feedback contract passed: $count authoritative reasons; diagnostic-only invariants; 4 negative controls; read-only."
