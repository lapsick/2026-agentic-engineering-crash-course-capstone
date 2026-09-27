<#
.SYNOPSIS
    Fix until green: runs `dotnet test <Project>` + the module-boundary audit and,
    while either is red, lets a headless `claude -p` fix src/ (one failure per
    iteration) until green.

    Exit codes: 0 GREEN, 1 MAX_ITERATIONS, 2 NO_PROGRESS (same failures twice),
    3 VIOLATION (agent touched test/ or the audit script), 4 AGENT_ERROR,
    5 ENVIRONMENT (Docker down — the agent is never invoked for that).
    Log: green-runs/<timestamp>/run.log

.EXAMPLE
    pwsh scripts/fix-until-green.ps1 -Project test/ToolShare.Lending.Domain.Tests/ToolShare.Lending.Domain.Tests.csproj
#>
param(
    [Parameter(Mandatory = $true)][string]$Project,
    [string]$Filter,
    [int]$MaxIterations = 5,
    [string]$Model = 'sonnet'
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)

$runDir = "green-runs/$(Get-Date -Format 'yyyyMMdd-HHmmss')"
New-Item -ItemType Directory -Force $runDir | Out-Null
$log = "$runDir/run.log"
$protected = 'test/*', 'scripts/check-module-boundaries.ps1'
$environmentErrors = 'DockerUnavailableException|Docker is either not running|Cannot connect to the Docker daemon'

function Say([string]$text, [string]$color = 'Gray') {
    Write-Host $text -ForegroundColor $color
    Add-Content $log $text -Encoding utf8
}

function Finish([string]$status, [int]$code) {
    Say "Status: $status — $i test runs, $agentRuns agent iterations, cost `$$([math]::Round($cost, 4))" $(if ($code -eq 0) { 'Green' } else { 'Yellow' })
    Say "Log: $log" 'DarkGray'
    exit $code
}

# Failing tests (name, message, top of stack), compile errors, and boundary violations — empty when green.
function Get-Failures([string]$dir) {
    $dotnetArgs = @('test', $Project, '--logger', 'trx;LogFileName=results.trx', '--results-directory', $dir)
    if ($Filter) { $dotnetArgs += '--filter', $Filter }
    $output = & dotnet @dotnetArgs 2>&1 | ForEach-Object { "$_" }
    $testExitCode = $LASTEXITCODE

    $failures = @()
    if (Test-Path "$dir/results.trx") {
        [xml]$trx = Get-Content "$dir/results.trx" -Raw
        $failures = @($trx.TestRun.Results.UnitTestResult | Where-Object outcome -eq 'Failed' | ForEach-Object {
            $stack = ("$($_.Output.ErrorInfo.StackTrace)" -split "`r?`n" | Where-Object { $_.Trim() } | Select-Object -First 3) -join "`n  "
            "$($_.testName)`n  $("$($_.Output.ErrorInfo.Message)".Trim())`n  $stack"
        })
    }
    if ($testExitCode -ne 0 -and $failures.Count -eq 0) {
        $failures = @($output | Where-Object { $_ -match ': error ' } | Sort-Object -Unique | Select-Object -First 20)
        if ($failures.Count -eq 0) { $failures = @("dotnet test exited $testExitCode") }
    }

    $audit = & pwsh -NoProfile -File scripts/check-module-boundaries.ps1 2>&1 | ForEach-Object { "$_" }
    if ($LASTEXITCODE -ne 0) {
        $failures += @($audit | Where-Object { $_ -match ' -> ' } | ForEach-Object { "module boundary violation (Constitution II): $($_.Trim())" })
    }
    return $failures
}

# Path -> hash of every modified/untracked file, to see exactly what the agent changed.
function Get-Snapshot {
    $snapshot = @{}
    git ls-files --modified --others --exclude-standard 2>$null |
        Where-Object { $_ -notmatch '^(green-runs|\.agent-log)/' } |
        ForEach-Object { $snapshot[$_] = if (Test-Path $_) { (Get-FileHash $_).Hash } else { 'deleted' } }
    return $snapshot
}

$i = 0
$agentRuns = 0
$cost = 0
$lastSignature = $null
Say "Fix until green — $Project$(if ($Filter) { " [$Filter]" }), model $Model, max $MaxIterations iterations" 'Cyan'

while ($true) {
    $i++
    $dir = "$runDir/iter-$i"
    New-Item -ItemType Directory -Force $dir | Out-Null

    $failures = @(Get-Failures $dir)
    if ($failures.Count -eq 0) {
        Say "[$i] GREEN — tests and module boundaries pass" 'Green'
        Finish 'GREEN' 0
    }

    Say "[$i] RED — $($failures.Count) failure(s):" 'Red'
    $failures | ForEach-Object { Say "    x $(($_ -split "`n")[0])" 'Red' }

    if ($failures -match $environmentErrors) { Finish 'ENVIRONMENT' 5 }

    $signature = ($failures | ForEach-Object { ($_ -split "`n")[0] } | Sort-Object) -join '|'
    if ($signature -eq $lastSignature) { Finish 'NO_PROGRESS' 2 }
    $lastSignature = $signature

    if ($i -gt $MaxIterations) { Finish 'MAX_ITERATIONS' 1 }

    $prompt = @"
Automated fix loop, iteration $i of $MaxIterations, in the ToolShare repository. ``dotnet test $Project`` and/or the module-boundary audit (scripts/check-module-boundaries.ps1) are red:

$($failures -join "`n")

Fix exactly ONE failure — the first one listed — with a minimal change to production code under src/. Read the failing test first; the XML doc comments on the production code describe the intended rule.
- Never modify anything under test/ or scripts/check-module-boundaries.ps1 — the loop aborts if you do.
- Never add a ProjectReference to another module's Domain or EntityFrameworkCore project; cross-module code goes through *.Application.Contracts or ILocalEventBus events (Constitution II).
- You may run dotnet build; don't run the tests — the loop does.
End with exactly one line: FIXED: <file> — <what was wrong and what you changed>  or  BLOCKED: <reason>
"@

    $before = Get-Snapshot
    $json = $prompt | & claude -p --output-format json --model $Model --permission-mode acceptEdits `
        --allowedTools Read Edit Glob Grep 'Bash(dotnet build:*)' `
        --disallowedTools Write 'Bash(git:*)' 'Bash(dotnet test:*)' `
        --max-turns 25 --max-budget-usd 2 2>&1
    $json | Set-Content "$dir/agent.json" -Encoding utf8
    $agentRuns++
    $after = Get-Snapshot

    $result = try { ($json -join "`n") | ConvertFrom-Json } catch { $null }
    if (-not $result -or $result.is_error) {
        Say '    agent failed — see agent.json' 'Yellow'
        Finish 'AGENT_ERROR' 4
    }
    $cost += [decimal]$result.total_cost_usd

    $lines = @("$($result.result)" -split "`r?`n" | Where-Object { $_.Trim() })
    $verdict = @($lines | Where-Object { $_ -match '(FIXED|BLOCKED):' })[-1]
    if (-not $verdict) { $verdict = $lines[-1] }
    Say "    agent ($($result.num_turns) turns, `$$([math]::Round([decimal]$result.total_cost_usd, 4))): $("$verdict".Trim().Trim('`'))" 'Magenta'

    $changed = @(@($before.Keys) + @($after.Keys) | Sort-Object -Unique | Where-Object { $before[$_] -ne $after[$_] })
    $changed | ForEach-Object { Say "      ~ $_" 'DarkGray' }
    if ($changed | Where-Object { $file = $_; $protected | Where-Object { $file -like $_ } }) {
        Say '    agent modified protected files (test/ or the boundary audit)' 'Yellow'
        Finish 'VIOLATION' 3
    }
}
