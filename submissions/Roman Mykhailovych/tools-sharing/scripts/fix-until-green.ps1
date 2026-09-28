<#
.SYNOPSIS
    Fix until green: runs `dotnet test <Project>` + the module-boundary audit and,
    while either is red, lets the headless toolshare-fixer agent (.claude/agents) fix src/
    (all listed failures per iteration) until green. -SkipInitialAudit: the caller already
    ran the audit green, so the first run skips it (it always runs after an agent edit).

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
    [string]$Model = 'sonnet',
    [switch]$SkipInitialAudit
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

    if ($i -eq 1 -and $SkipInitialAudit) { return $failures }
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

    # The rules live in .claude/agents/toolshare-fixer.md; the prompt is just this iteration's red list.
    $prompt = @"
Fix loop iteration $i of $MaxIterations. ``dotnet test $Project`` and/or the module-boundary audit are red:

$($failures -join "`n")
"@

    # --agent limits the tool schemas to the fixer's own; no MCP servers, no skill listing —
    # a much smaller fixed context re-read on every turn.
    $before = Get-Snapshot
    $json = $prompt | & claude -p --agent toolshare-fixer --output-format json --model $Model --permission-mode acceptEdits `
        --strict-mcp-config --disable-slash-commands `
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
    $verdicts = @($lines | Where-Object { $_ -match '(FIXED|BLOCKED):' })
    if (-not $verdicts) { $verdicts = @($lines[-1]) }
    Say "    agent ($($result.num_turns) turns, `$$([math]::Round([decimal]$result.total_cost_usd, 4))):" 'Magenta'
    $verdicts | ForEach-Object { Say "      $("$_".Trim().Trim('`'))" 'Magenta' }

    $changed = @(@($before.Keys) + @($after.Keys) | Sort-Object -Unique | Where-Object { $before[$_] -ne $after[$_] })
    $changed | ForEach-Object { Say "      ~ $_" 'DarkGray' }
    if ($changed | Where-Object { $file = $_; $protected | Where-Object { $file -like $_ } }) {
        Say '    agent modified protected files (test/ or the boundary audit)' 'Yellow'
        Finish 'VIOLATION' 3
    }
}
