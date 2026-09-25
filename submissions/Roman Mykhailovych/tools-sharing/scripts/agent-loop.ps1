<#
.SYNOPSIS
    Test-driven agent loop: runs `dotnet test` plus the module-boundary audit,
    and while either is red hands the failures to a headless Claude Code agent
    (`claude -p`) to fix the production code — repeating until both are green
    or a stop condition fires.

.DESCRIPTION
    One iteration =
      1. `dotnet test <Project> [--filter <Filter>]` with a TRX logger, then
         scripts/check-module-boundaries.ps1 (Constitution II) unless
         -SkipBoundaryCheck. Both pass -> GREEN, stop. The boundary audit is
         part of "green" so the agent can't make a test pass by adding a
         ProjectReference to another module's Domain/EntityFrameworkCore.
      2. Failing tests (name, message, top of stack) are parsed from the TRX
         file (or build errors from the console output if it didn't compile);
         boundary violations are parsed from the audit's output.
      3. `claude -p` is invoked with those failures. It may only edit code
         under src/ and must fix ONE failure per iteration (small,
         individually verified steps — the loop re-runs the checks itself).
      4. Guard: if anything under test/ or the boundary audit script itself
         changed, the loop stops with VIOLATION — tests are the specification
         (constitution Principle V, .claude/rules/testing.md), and the checks
         must not be weakened to get a green run.
      5. The iteration (failures, agent summary, changed files, turns, cost,
         duration) is appended to a Markdown run log.

    Stop conditions / exit codes:
      0 GREEN           tests pass and no module-boundary violations
      1 MAX_ITERATIONS  still red after -MaxIterations agent runs
      2 NO_PROGRESS     the same set of failures two iterations in a row
      3 VIOLATION       the agent modified test/ or the boundary audit script
      4 AGENT_ERROR     `claude -p` failed or returned an error result

    Run artifacts land in <LogDir>/<timestamp>/: run.md (the Markdown log,
    including the agent's per-iteration diff), console.txt (the console
    output), and per iteration the TRX file, raw `dotnet test` output, the
    agent prompt, and the raw agent JSON result.

.EXAMPLE
    pwsh scripts/agent-loop.ps1 -Project test/ToolShare.Lending.Domain.Tests/ToolShare.Lending.Domain.Tests.csproj

.EXAMPLE
    pwsh scripts/agent-loop.ps1 -Project test/ToolShare.Lending.Application.Tests/ToolShare.Lending.Application.Tests.csproj -Filter "FullyQualifiedName~ToolShare.Lending.Reservations" -MaxIterations 8 -Model opus
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Project,

    [string]$Filter,

    [int]$MaxIterations = 5,

    # Passed to `claude --model`. Fixing one failing test is a narrow task, so
    # the cheaper model is the default; pass -Model opus for harder failures.
    [string]$Model = 'sonnet',

    [int]$MaxTurnsPerIteration = 25,

    [decimal]$MaxBudgetUsdPerIteration = 2,

    [string]$LogDir = 'loop-runs',

    [switch]$SkipBoundaryCheck
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$runId = Get-Date -Format 'yyyyMMdd-HHmmss'
$logRoot = if ([System.IO.Path]::IsPathRooted($LogDir)) { $LogDir } else { Join-Path $repoRoot $LogDir }
$runDir = Join-Path $logRoot $runId
New-Item -ItemType Directory -Force -Path $runDir | Out-Null
$logFile = Join-Path $runDir 'run.md'
$logDirRelative = ($LogDir.TrimEnd('/', '\') -replace '\\', '/') + '/'

# Written as a side effect of every run, never by the agent's own edits: this
# loop's logs and the PreToolUse/PostToolUse hook's action log.
$untrackedPrefixes = @($logDirRelative, '.agent-log/')

$boundaryScript = 'scripts/check-module-boundaries.ps1'

# Files the agent must never change: the tests (the specification) and the
# boundary audit (one of the checks deciding what "green" means).
$protectedPathPatterns = @('test/*', $boundaryScript)

$commandLine = "pwsh scripts/agent-loop.ps1 -Project $Project" +
    $(if ($Filter) { " -Filter `"$Filter`"" } else { '' }) +
    " -MaxIterations $MaxIterations -Model $Model" +
    $(if ($SkipBoundaryCheck) { ' -SkipBoundaryCheck' } else { '' })

function Write-Log([string]$text) {
    Add-Content -Path $logFile -Value $text -Encoding utf8
}

function Write-Step([string]$text, [string]$color = 'Gray') {
    Write-Host $text -ForegroundColor $color
    Add-Content -Path (Join-Path $runDir 'console.txt') -Value $text -Encoding utf8
}

# Path -> content hash for every file that differs from HEAD (modified or
# untracked), excluding $untrackedPrefixes. Comparing two
# snapshots tells exactly which files the agent touched, even when the
# working tree was already dirty before the loop started. With -CopyTo, the
# dirty files are also copied there so the agent's own change can be diffed
# afterwards (a file clean before the agent ran is diffed against HEAD).
function Get-WorkingTreeSnapshot([string]$CopyTo) {
    $snapshot = @{}
    $paths = @(git ls-files --modified --others --exclude-standard 2>$null) + @(git diff --name-only --diff-filter=D HEAD 2>$null)
    $relevantPaths = $paths | Where-Object { $path = $_; $path -and -not ($untrackedPrefixes | Where-Object { $path.StartsWith($_) }) }
    foreach ($path in ($relevantPaths | Sort-Object -Unique)) {
        $fullPath = Join-Path $repoRoot $path
        if (Test-Path $fullPath -PathType Leaf) {
            $snapshot[$path] = (Get-FileHash $fullPath -Algorithm SHA256).Hash
            if ($CopyTo) {
                $copyPath = Join-Path $CopyTo $path
                New-Item -ItemType Directory -Force -Path (Split-Path -Parent $copyPath) | Out-Null
                Copy-Item $fullPath $copyPath
            }
        }
        else {
            $snapshot[$path] = '<deleted>'
        }
    }
    return $snapshot
}

# Unified diff of just what the agent changed in one file during one iteration.
function Get-AgentDiff([string]$path, $before, [string]$beforeCopyDir, [string]$iterationDir) {
    $beforeFile = Join-Path $beforeCopyDir $path
    if (-not $before.ContainsKey($path)) {
        $beforeFile = Join-Path $iterationDir 'head-version.tmp'
        git show "HEAD:./$path" 2>$null | Set-Content -Path $beforeFile -Encoding utf8
    }
    $afterFile = Join-Path $repoRoot $path
    if (-not (Test-Path $afterFile)) { $afterFile = '/dev/null' }

    $body = git -c core.safecrlf=false diff --no-index --no-color --ignore-space-at-eol -- $beforeFile $afterFile 2>$null |
        Where-Object { $_ -notmatch '^(diff --git|index |--- |\+\+\+ |new file mode|deleted file mode)' }
    return @("--- a/$path", "+++ b/$path") + @($body)
}

function Get-ChangedFiles($before, $after) {
    $changed = foreach ($path in (@($before.Keys) + @($after.Keys) | Sort-Object -Unique)) {
        if ($before[$path] -ne $after[$path]) { $path }
    }
    return @($changed)
}

function Invoke-TestRun([int]$iteration) {
    $iterationDir = Join-Path $runDir ("iter-{0:D2}" -f $iteration)
    New-Item -ItemType Directory -Force -Path $iterationDir | Out-Null

    $arguments = @('test', $Project, '--logger', 'trx;LogFileName=results.trx', '--results-directory', $iterationDir)
    if ($Filter) { $arguments += @('--filter', $Filter) }

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $output = & dotnet @arguments 2>&1 | ForEach-Object { "$_" }
    $exitCode = $LASTEXITCODE
    $stopwatch.Stop()
    $output | Set-Content -Path (Join-Path $iterationDir 'dotnet-test.log') -Encoding utf8

    $result = [ordered]@{
        ExitCode = $exitCode
        Seconds  = [math]::Round($stopwatch.Elapsed.TotalSeconds, 1)
        Total    = 0
        Passed   = 0
        Failed   = @()
        BuildErrors = @()
    }

    $trxPath = Join-Path $iterationDir 'results.trx'
    if (Test-Path $trxPath) {
        [xml]$trx = Get-Content $trxPath -Raw
        $counters = $trx.TestRun.ResultSummary.Counters
        $result.Total = [int]$counters.total
        $result.Passed = [int]$counters.passed
        $result.Failed = @(
            $trx.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -eq 'Failed' } | ForEach-Object {
                $stack = "$($_.Output.ErrorInfo.StackTrace)" -split "`r?`n" | Where-Object { $_.Trim() } | Select-Object -First 4
                [pscustomobject]@{
                    Name    = $_.testName
                    Message = ("$($_.Output.ErrorInfo.Message)").Trim()
                    Stack   = ($stack -join "`n").Trim()
                }
            }
        )
    }
    elseif ($exitCode -ne 0) {
        $result.BuildErrors = @($output | Where-Object { $_ -match ': error ' } | Sort-Object -Unique | Select-Object -First 20)
    }

    return $result
}

# Runs the Constitution II audit in a child pwsh (it calls `exit`) and parses
# its "  Referencer -> Referenced  (file)" violation lines.
function Invoke-BoundaryCheck([int]$iteration) {
    if ($SkipBoundaryCheck) {
        return [pscustomobject]@{ Ok = $true; Skipped = $true; Violations = @(); Seconds = 0 }
    }

    $iterationDir = Join-Path $runDir ("iter-{0:D2}" -f $iteration)
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $output = & pwsh -NoProfile -File (Join-Path $repoRoot $boundaryScript) 2>&1 | ForEach-Object { "$_" }
    $exitCode = $LASTEXITCODE
    $stopwatch.Stop()
    $output | Set-Content -Path (Join-Path $iterationDir 'boundary-check.log') -Encoding utf8

    $violations = @($output | Where-Object { $_ -match '^\s+\S+ -> \S+' } | ForEach-Object { $_.Trim() })
    if ($exitCode -ne 0 -and $violations.Count -eq 0) {
        # The audit itself failed without reporting violations — surface its output as-is.
        $violations = @($output | Where-Object { $_.Trim() } | Select-Object -First 10)
    }

    return [pscustomobject]@{
        Ok         = $exitCode -eq 0
        Skipped    = $false
        Violations = $violations
        Seconds    = [math]::Round($stopwatch.Elapsed.TotalSeconds, 1)
    }
}

function New-AgentPrompt($testRun, $boundary, [int]$iteration) {
    $filterText = if ($Filter) { " --filter `"$Filter`"" } else { '' }
    $sections = @()
    if ($testRun.BuildErrors.Count -gt 0) {
        $sections += "The solution does not compile:`n" + (($testRun.BuildErrors | ForEach-Object { "- $_" }) -join "`n")
    }
    elseif ($testRun.Failed.Count -gt 0) {
        $sections += "Failing tests ($($testRun.Failed.Count) of $($testRun.Total)):`n" + (($testRun.Failed | ForEach-Object {
            "- $($_.Name)`n  Message: $($_.Message)`n  Stack:`n$((($_.Stack -split "`n") | ForEach-Object { "    $_" }) -join "`n")"
        }) -join "`n")
    }
    if (-not $boundary.Ok) {
        $sections += "Module boundary violations reported by ``pwsh $boundaryScript`` (Constitution II — a module must not reference another module's Domain or EntityFrameworkCore project; cross-module coupling is allowed only via the other module's *.Application.Contracts or ILocalEventBus integration events):`n" +
            (($boundary.Violations | ForEach-Object { "- $_" }) -join "`n")
    }
    $failureText = $sections -join "`n`n"

    return @"
You are one iteration ($iteration of max $MaxIterations) of an automated fix loop in the ToolShare repository.
The loop ran ``dotnet test $Project$filterText`` and the module-boundary audit, and they are not both green.

$failureText

Your task for THIS iteration: fix exactly ONE of these — the first one listed (or the compile errors) — by changing production code under src/. Fix a boundary violation by removing the forbidden ProjectReference and switching the code to the other module's Application.Contracts or an integration event.

Rules:
- NEVER modify, skip or delete anything under test/. The tests are the specification (constitution Principle V, .claude/rules/testing.md). The loop aborts if test/ changes.
- NEVER modify $boundaryScript, and never add a ProjectReference from one module to another module's Domain or EntityFrameworkCore project to make something compile or pass. The loop aborts if the audit script changes.
- Read the failing test first to understand the expected behavior, then the production code it exercises. The XML doc comments on the production code describe the intended rule.
- Keep the change minimal: fix the defect, don't refactor or touch unrelated code, don't fix other failing tests in advance — the next iteration handles them.
- You may run ``dotnet build`` to check compilation. Do not run the tests — the loop does that after you finish.
- End your reply with exactly one line: ``FIXED: <file> — <what was wrong and what you changed>`` or ``BLOCKED: <reason>``.
"@
}

function Invoke-Agent([string]$prompt, [int]$iteration) {
    $iterationDir = Join-Path $runDir ("iter-{0:D2}" -f $iteration)
    $prompt | Set-Content -Path (Join-Path $iterationDir 'agent-prompt.md') -Encoding utf8

    $arguments = @(
        '-p',
        '--output-format', 'json',
        '--model', $Model,
        '--permission-mode', 'acceptEdits',
        '--allowedTools', 'Read', 'Edit', 'Glob', 'Grep', 'Bash(dotnet build:*)',
        '--disallowedTools', 'Write', 'Bash(dotnet test:*)', 'Bash(git:*)',
        '--max-turns', "$MaxTurnsPerIteration",
        '--max-budget-usd', "$MaxBudgetUsdPerIteration"
    )

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $raw = ($prompt | & claude @arguments 2>&1 | ForEach-Object { "$_" }) -join "`n"
    $exitCode = $LASTEXITCODE
    $stopwatch.Stop()
    $raw | Set-Content -Path (Join-Path $iterationDir 'agent-result.json') -Encoding utf8

    $parsed = $null
    try { $parsed = $raw | ConvertFrom-Json } catch { }

    return [pscustomobject]@{
        ExitCode = $exitCode
        IsError  = ($null -eq $parsed) -or [bool]$parsed.is_error -or $exitCode -ne 0
        Result   = if ($parsed) { "$($parsed.result)".Trim() } else { $raw.Trim() }
        Turns    = if ($parsed) { $parsed.num_turns } else { $null }
        CostUsd  = if ($parsed) { [math]::Round([decimal]$parsed.total_cost_usd, 4) } else { $null }
        Seconds  = [math]::Round($stopwatch.Elapsed.TotalSeconds, 1)
    }
}

function Get-VerdictLine([string]$agentText) {
    $line = ($agentText -split "`r?`n" | Where-Object { $_ -match '^\s*`?(FIXED|BLOCKED):' } | Select-Object -Last 1)
    if ($line) { return $line.Trim().Trim('`') }
    return ($agentText -split "`r?`n" | Where-Object { $_.Trim() } | Select-Object -Last 1)
}

# --- run ---------------------------------------------------------------------

Write-Log "# Agent loop run $runId"
Write-Log ''
Write-Log "- Command: ``$commandLine``"
Write-Log "- Started: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Write-Log "- Branch / HEAD: $(git rev-parse --abbrev-ref HEAD) / $(git rev-parse --short HEAD)"
Write-Log "- Model: $Model, max $MaxIterations iterations, max $MaxTurnsPerIteration turns and `$$MaxBudgetUsdPerIteration per iteration"
Write-Log ''

Write-Step "Agent loop $runId — $Project$(if ($Filter) { " [$Filter]" })$(if ($SkipBoundaryCheck) { ' (boundary check skipped)' })" 'Cyan'
Write-Step "Log: $logFile" 'DarkGray'

$status = $null
$summaryRows = @()
$previousFailureSignature = $null
$totalCost = [decimal]0
$iteration = 0
$agentRuns = 0

while ($true) {
    $iteration++
    Write-Step ''
    Write-Step ("=== Iteration {0} — running tests" -f $iteration) 'Cyan'

    $testRun = Invoke-TestRun $iteration
    $failedCount = if ($testRun.BuildErrors.Count -gt 0) { 'build failed' } else { "$($testRun.Failed.Count)" }
    $testsGreen = $testRun.ExitCode -eq 0

    $boundary = Invoke-BoundaryCheck $iteration
    $boundaryText = if ($boundary.Skipped) { 'skipped' } elseif ($boundary.Ok) { 'ok' } else { "$($boundary.Violations.Count) violation(s)" }

    Write-Log "## Iteration $iteration"
    Write-Log ''
    Write-Log "**Tests:** exit $($testRun.ExitCode), $($testRun.Total) total, $($testRun.Passed) passed, failed: $failedCount ($($testRun.Seconds) s)"
    Write-Log ''
    Write-Log "**Module boundaries:** $boundaryText"
    Write-Log ''

    if ($testsGreen -and $boundary.Ok) {
        Write-Step "    GREEN: $($testRun.Passed)/$($testRun.Total) passed ($($testRun.Seconds) s), module boundaries: $boundaryText" 'Green'
        $summaryRows += "| $iteration | $($testRun.Total) / $($testRun.Passed) / 0 | $boundaryText | — | — | — | — |"
        $status = 'GREEN'
        break
    }

    if ($testsGreen) {
        Write-Step "    tests green: $($testRun.Passed)/$($testRun.Total) passed ($($testRun.Seconds) s)" 'Green'
    }
    else {
        Write-Step "    RED: $($testRun.Total) total, failed: $failedCount ($($testRun.Seconds) s)" 'Red'
    }
    foreach ($failure in $testRun.Failed) { Write-Step "      x $($failure.Name)" 'Red' }
    foreach ($buildError in $testRun.BuildErrors) { Write-Step "      x $buildError" 'Red' }
    if (-not $boundary.Ok) {
        Write-Step "    BOUNDARIES RED: $boundaryText" 'Red'
        foreach ($violation in $boundary.Violations) { Write-Step "      x $violation" 'Red' }
    }

    foreach ($failure in $testRun.Failed) {
        Write-Log "- ``$($failure.Name)`` — $(($failure.Message -split "`r?`n")[0])"
    }
    foreach ($buildError in $testRun.BuildErrors) { Write-Log "- build: ``$buildError``" }
    foreach ($violation in $boundary.Violations) { Write-Log "- boundary: ``$violation``" }
    Write-Log ''

    $failureSignature = (@($testRun.Failed | ForEach-Object { $_.Name }) + @($testRun.BuildErrors) + @($boundary.Violations) | Sort-Object) -join '|'
    if ($failureSignature -eq $previousFailureSignature) {
        Write-Step '    NO_PROGRESS: same failures as the previous iteration' 'Yellow'
        $summaryRows += "| $iteration | $($testRun.Total) / $($testRun.Passed) / $failedCount | $boundaryText | — | — | — | stopped: no progress |"
        $status = 'NO_PROGRESS'
        break
    }
    $previousFailureSignature = $failureSignature

    if ($iteration -gt $MaxIterations) {
        Write-Step "    MAX_ITERATIONS: still red after $MaxIterations agent runs" 'Yellow'
        $summaryRows += "| $iteration | $($testRun.Total) / $($testRun.Passed) / $failedCount | $boundaryText | — | — | — | stopped: max iterations |"
        $status = 'MAX_ITERATIONS'
        break
    }

    Write-Step "    agent ($Model) working..." 'Magenta'
    $iterationDir = Join-Path $runDir ("iter-{0:D2}" -f $iteration)
    $beforeCopyDir = Join-Path $iterationDir 'before'
    $before = Get-WorkingTreeSnapshot -CopyTo $beforeCopyDir
    $agent = Invoke-Agent (New-AgentPrompt $testRun $boundary $iteration) $iteration
    $agentRuns++
    $after = Get-WorkingTreeSnapshot
    $changedFiles = Get-ChangedFiles $before $after
    if ($agent.CostUsd) { $totalCost += $agent.CostUsd }

    $verdict = Get-VerdictLine $agent.Result
    Write-Step "    agent: $($agent.Turns) turns, `$$($agent.CostUsd), $($agent.Seconds) s" 'Magenta'
    Write-Step "    $verdict" 'Magenta'
    foreach ($file in $changedFiles) { Write-Step "      ~ $file" 'DarkGray' }

    Write-Log "**Agent:** $($agent.Turns) turns, `$$($agent.CostUsd), $($agent.Seconds) s"
    Write-Log ''
    Write-Log "> $verdict"
    Write-Log ''
    $changedFilesText = if ($changedFiles.Count) { ($changedFiles | ForEach-Object { '`' + $_ + '`' }) -join ', ' } else { 'none' }
    Write-Log "**Changed files:** $changedFilesText"
    Write-Log ''
    foreach ($file in $changedFiles) {
        Write-Log '```diff'
        Write-Log ((Get-AgentDiff $file $before $beforeCopyDir $iterationDir) -join "`n")
        Write-Log '```'
        Write-Log ''
    }
    if (Test-Path $beforeCopyDir) { Remove-Item -Recurse -Force $beforeCopyDir }
    Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $iterationDir 'head-version.tmp')

    $summaryRows += "| $iteration | $($testRun.Total) / $($testRun.Passed) / $failedCount | $boundaryText | $($agent.Turns) | `$$($agent.CostUsd) | $($changedFiles -join ', ') | $($verdict -replace '\|', '/') |"

    if ($agent.IsError) {
        Write-Step '    AGENT_ERROR: claude -p failed or returned an error result' 'Yellow'
        $status = 'AGENT_ERROR'
        break
    }

    $protectedChanges = @($changedFiles | Where-Object { $file = $_; $protectedPathPatterns | Where-Object { $file -like $_ } })
    if ($protectedChanges.Count -gt 0) {
        Write-Step "    VIOLATION: agent modified protected files: $($protectedChanges -join ', ')" 'Yellow'
        Write-Log "**VIOLATION:** agent modified protected files: $($protectedChanges -join ', ')"
        Write-Log ''
        $status = 'VIOLATION'
        break
    }
}

Write-Log '## Summary'
Write-Log ''
Write-Log "**Status: $status** after $iteration test runs / $agentRuns agent iterations, total agent cost `$$totalCost"
Write-Log ''
Write-Log '| Iteration | Tests total / passed / failed | Module boundaries | Agent turns | Cost | Changed files | Agent verdict |'
Write-Log '|---|---|---|---|---|---|---|'
$summaryRows | ForEach-Object { Write-Log $_ }
Write-Log ''
Write-Log "- Finished: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"

Write-Step ''
Write-Step "Status: $status — $iteration test runs, $agentRuns agent iterations, total cost `$$totalCost" $(if ($status -eq 'GREEN') { 'Green' } else { 'Yellow' })
Write-Step "Log: $logFile" 'DarkGray'

switch ($status) {
    'GREEN'          { exit 0 }
    'MAX_ITERATIONS' { exit 1 }
    'NO_PROGRESS'    { exit 2 }
    'VIOLATION'      { exit 3 }
    default          { exit 4 }
}
