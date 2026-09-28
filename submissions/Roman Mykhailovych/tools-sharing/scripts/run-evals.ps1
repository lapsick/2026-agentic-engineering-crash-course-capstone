<#
.SYNOPSIS
    Evals for the agentic tooling, graded deterministically:
      fixer    — seeded bugs in domain code; does scripts/fix-until-green.ps1 get the tests green
                 without touching test/, and does its fix match the original code?
      reviewer — seeded constitution/convention violations plus clean changes; does the
                 toolshare-reviewer agent (.claude/agents) catch the defect at the right
                 severity, and approve the clean ones?

    Every run uses a throwaway `git worktree`, so the working tree is never touched. The tooling
    under test is HEAD, or with -WorkingTree a snapshot commit of the current working tree
    (uncommitted tooling; nothing is staged or committed). Cases: evals/<suite>/cases/*.json.
    Results: evals/results/<suite>-<timestamp>.md (+ per-case logs next to it).
    -ValidateOnly: no agent calls ($0) — fixer checks each mutation turns its tests red,
    reviewer checks each case's edit anchors apply.
    -Parallel N: up to N cases at once, each in its own worktree (a child run of this script);
    the cost ceiling is checked before each case starts, so in-flight cases can overshoot it.
    Exit code: 0 all cases passed, 1 otherwise.

.EXAMPLE
    pwsh scripts/run-evals.ps1 -Suite fixer -ValidateOnly
    pwsh scripts/run-evals.ps1 -Suite reviewer -Parallel 5
    pwsh scripts/run-evals.ps1 -Suite fixer -WorkingTree
    pwsh scripts/run-evals.ps1 -Suite fixer -Case 'lending-*' -Model haiku
#>
param(
    [Parameter(Mandatory = $true)][ValidateSet('fixer', 'reviewer')][string]$Suite,
    [string]$Case = '*',
    # fixer: passed to fix-until-green.ps1 (default sonnet); reviewer: overrides the agent's model.
    [string]$Model,
    [int]$MaxIterations = 5,
    [decimal]$MaxCostUsd = 15,
    [int]$Parallel = 3,
    [switch]$WorkingTree,
    [switch]$ValidateOnly,
    # Internal — set by the parallel parent for its child runs.
    [string]$Ref = 'HEAD',
    [string]$ResultsDir,
    [string]$RowFile
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location $projectRoot

$started = Get-Date
$stamp = $started.ToString('yyyyMMdd-HHmmss')
$resultsDir = if ($ResultsDir) { $ResultsDir } else { "evals/results/$Suite-$stamp$(if ($ValidateOnly) { '-validate' })" }
New-Item -ItemType Directory -Force $resultsDir | Out-Null
$cases = @(Get-ChildItem "evals/$Suite/cases" -Filter "$Case.json" | Sort-Object Name)
if ($cases.Count -eq 0) { throw "No cases match evals/$Suite/cases/$Case.json" }

if ($WorkingTree -and -not $RowFile) {
    . "$PSScriptRoot/working-tree-snapshot.ps1"
    $Ref = (Get-WorkingTreeSnapshot).Commit
}
$refLabel = "$((git rev-parse --short $Ref).Trim())$(if ($WorkingTree) { ' (working-tree snapshot)' })"
$runParallel = $Parallel -gt 1 -and $cases.Count -gt 1 -and -not $RowFile

# Throwaway worktree; the project lives at the same relative path inside it. The random suffix
# keeps parallel child runs started in the same second apart.
$worktree = $null
if (-not $runParallel) {
    $prefix = (git rev-parse --show-prefix).Trim()
    $worktree = Join-Path $env:TEMP "toolshare-evals/wt-$stamp-$([guid]::NewGuid().ToString('N').Substring(0, 6))"
    foreach ($attempt in 1..3) {
        git worktree add --detach $worktree $Ref 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) { break }
        Start-Sleep -Milliseconds (500 * $attempt)   # concurrent `worktree add` can hit a git lock
    }
    if ($LASTEXITCODE -ne 0) { throw "git worktree add $Ref failed" }
    $wt = Join-Path $worktree $prefix
}

function Set-Edits($edits) {
    foreach ($edit in $edits) {
        $path = Join-Path $wt $edit.file
        $bytes = [IO.File]::ReadAllBytes($path)
        $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
        $text = [IO.File]::ReadAllText($path)
        $newline = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
        $find = $edit.find.Replace("`n", $newline)
        $count = ([regex]::Matches($text, [regex]::Escape($find))).Count
        if ($count -ne 1) { throw "anchor in $($edit.file) matched $count times (must be exactly 1): $($edit.find)" }
        $text = $text.Replace($find, $edit.replace.Replace("`n", $newline))
        [IO.File]::WriteAllText($path, $text, [Text.UTF8Encoding]::new($hasBom))
    }
}

function Reset-Worktree {
    git -C $wt checkout -- . 2>&1 | Out-Null
    git -C $wt clean -fdq -- . 2>&1 | Out-Null
}

function Test-SameAsHead([string]$file) {
    $current = [IO.File]::ReadAllText((Join-Path $wt $file)) -replace "`r`n", "`n"
    $original = (git -C $wt show "HEAD:./$($file -replace '\\', '/')" 2>$null) -join "`n"
    return $current.TrimEnd() -eq $original.TrimEnd()
}

# Maps each finding line of a review to the severity section it appears in.
function Get-Sections([string]$review) {
    $sections = @{ Blocking = ''; Important = ''; 'Nice-to-have' = '' }
    $current = $null
    foreach ($line in $review -split "`r?`n") {
        if ($line -match '^[\s#*>_-]*(Blocking|Important|Nice-to-have)\b') { $current = $Matches[1] }
        elseif ($line -match '^\s*REVIEW:') { $current = $null }
        if ($current) { $sections[$current] += "$line`n" }
    }
    return $sections
}

$rows = @()
$totalCost = [decimal]0
if ($runParallel) {
    $state = [hashtable]::Synchronized(@{ Cost = [decimal]0 })
    $script = "$PSScriptRoot/run-evals.ps1"
    $childArgs = @('-Suite', $Suite, '-Ref', $Ref, '-ResultsDir', $resultsDir, '-MaxIterations', $MaxIterations)
    if ($Model) { $childArgs += '-Model', $Model }
    if ($ValidateOnly) { $childArgs += '-ValidateOnly' }
    if ($WorkingTree) { $childArgs += '-WorkingTree' }
    Write-Host "Evals $Suite — $($cases.Count) cases, $Parallel in parallel, ref $refLabel" -ForegroundColor Cyan
    $rows = @($cases | ForEach-Object -ThrottleLimit $Parallel -Parallel {
        $name = $_.BaseName
        $state = $using:state
        $dir = "$using:projectRoot/$using:resultsDir/$name"
        New-Item -ItemType Directory -Force $dir | Out-Null
        if ($state.Cost -ge $using:MaxCostUsd) {
            return [pscustomobject]@{ Case = $name; Result = 'SKIPPED'; Detail = "cost ceiling `$$($using:MaxCostUsd) reached"; Cost = 0 }
        }
        $childArgs = $using:childArgs
        & pwsh -NoProfile -File $using:script @childArgs -Case $name -RowFile "$dir/row.json" *> "$dir/run.log"
        $row = if (Test-Path "$dir/row.json") { @(Get-Content "$dir/row.json" -Raw | ConvertFrom-Json)[0] }
            else { [pscustomobject]@{ Case = $name; Result = 'ERROR'; Detail = "no result — see $name/run.log"; Cost = 0 } }
        [System.Threading.Monitor]::Enter($state.SyncRoot)
        try { $state.Cost += [decimal]$row.Cost } finally { [System.Threading.Monitor]::Exit($state.SyncRoot) }
        Write-Host "    $name => $($row.Result) — $($row.Detail)" -ForegroundColor $(if ($row.Result -match '^(PASS|VALID)') { 'Green' } else { 'Yellow' })
        $row
    } | Sort-Object Case)
    $totalCost = $state.Cost
}
try {
    foreach ($file in $(if ($runParallel) { @() } else { $cases })) {
        $name = $file.BaseName
        $spec = Get-Content $file.FullName -Raw | ConvertFrom-Json
        $caseDir = "$resultsDir/$name"
        New-Item -ItemType Directory -Force $caseDir | Out-Null
        Write-Host "`n##### $Suite/$name — $($spec.description)" -ForegroundColor Cyan

        if ($totalCost -ge $MaxCostUsd) {
            $rows += [pscustomobject]@{ Case = $name; Result = 'SKIPPED'; Detail = "cost ceiling `$$MaxCostUsd reached"; Cost = 0 }
            continue
        }

        try { Set-Edits $spec.edits }
        catch {
            $rows += [pscustomobject]@{ Case = $name; Result = 'BROKEN CASE'; Detail = "$_"; Cost = 0 }
            Reset-Worktree
            continue
        }

        if ($Suite -eq 'fixer' -and $ValidateOnly) {
            $out = & dotnet test (Join-Path $wt $spec.project) 2>&1 | ForEach-Object { "$_" }
            $red = $LASTEXITCODE -ne 0
            $out | Set-Content "$caseDir/dotnet-test.log" -Encoding utf8
            $rows += [pscustomobject]@{ Case = $name; Result = $(if ($red) { 'VALID' } else { 'INVALID' }); Detail = $(if ($red) { 'mutation turns the tests red' } else { 'tests stay green — they do not catch this mutation' }); Cost = 0 }
        }
        elseif ($Suite -eq 'fixer') {
            $loopArgs = @('-Project', $spec.project, '-MaxIterations', $MaxIterations)
            if ($Model) { $loopArgs += '-Model', $Model }
            & pwsh -NoProfile -File (Join-Path $wt 'scripts/fix-until-green.ps1') @loopArgs 2>&1 |
                ForEach-Object { "$_" } | Tee-Object -Variable loopOutput | Out-Host
            $exitCode = $LASTEXITCODE
            $loopOutput | Set-Content "$caseDir/console.log" -Encoding utf8

            $status = "$(@($loopOutput | Where-Object { $_ -match '^Status: ' })[-1])" -replace '^Status: ', ''
            $cost = if ($status -match 'cost \$([\d.]+)') { [decimal]$Matches[1] } else { 0 }
            $totalCost += $cost
            $firstRunRed = [bool]($loopOutput | Where-Object { $_ -match '^\[1\] RED' })
            # The agent's end state vs the original code, kept for inspecting non-exact fixes.
            git -C $wt diff HEAD -- src/ test/ 2>$null | Set-Content "$caseDir/src.diff" -Encoding utf8
            $testTouched = [bool](git -C $wt status --porcelain -- test/)
            $otherSrc = @(git -C $wt status --porcelain -- src/ | ForEach-Object { ($_.Substring(3)).Trim('"') -replace '^.*?/src/', 'src/' } |
                Where-Object { $_ -notin @($spec.edits.file | ForEach-Object { $_ -replace '\\', '/' }) })
            $exact = $otherSrc.Count -eq 0 -and -not (@($spec.edits.file | Sort-Object -Unique) | Where-Object { -not (Test-SameAsHead $_) })

            $result = if (-not $firstRunRed) { 'INVALID' }
                elseif ($exitCode -eq 0 -and -not $testTouched) { if ($exact) { 'PASS (exact)' } else { 'PASS' } }
                else { 'FAIL' }
            $rows += [pscustomobject]@{ Case = $name; Result = $result; Detail = "$status$(if ($testTouched) { '; test/ modified' })"; Cost = $cost }
        }
        elseif ($ValidateOnly) {
            $rows += [pscustomobject]@{ Case = $name; Result = 'VALID'; Detail = 'edit anchors apply'; Cost = 0 }
        }
        else {
            $scope = ($spec.edits.file | Sort-Object -Unique) -join ', '
            $prompt = "Review the uncommitted changes in this repository.`nBase ref: HEAD`nScope: $scope`nFeature dir: none`nGate verdict: n/a — the tests were not run for this review; do not run them."
            $claudeArgs = @('-p', '--agent', 'toolshare-reviewer', '--output-format', 'json', '--max-turns', '40',
                '--allowedTools', 'Read', 'Grep', 'Glob', 'Bash(git diff:*)', 'Bash(git status:*)', 'Bash(git ls-files:*)', 'Bash(git show:*)', 'Bash(git log:*)',
                '--disallowedTools', 'Edit', 'Write')
            if ($Model) { $claudeArgs += '--model', $Model }
            Push-Location $wt
            try { $json = ($prompt | & claude @claudeArgs 2>&1 | ForEach-Object { "$_" }) -join "`n" } finally { Pop-Location }
            $json | Set-Content "$caseDir/agent.json" -Encoding utf8
            $parsed = try { $json | ConvertFrom-Json } catch { $null }
            $review = "$($parsed.result)"
            $review | Set-Content "$caseDir/review.md" -Encoding utf8
            $cost = if ($parsed) { [decimal]$parsed.total_cost_usd } else { 0 }
            $totalCost += $cost

            $verdictLine = "$(@($review -split "`r?`n" | Where-Object { $_ -match 'REVIEW:\s*(APPROVE|CHANGES_REQUESTED)' })[-1])".Trim()
            $sections = Get-Sections $review
            $mentioned = { param($text) -not ($spec.mustMention | Where-Object { $group = $_; -not ($group | Where-Object { $text -match [regex]::Escape($_) }) }) }
            $result = if (-not $parsed -or $parsed.is_error -or -not $verdictLine) { 'ERROR' }
                elseif ($spec.expect -eq 'Clean') { if ($verdictLine -match 'APPROVE') { 'PASS (approved)' } else { 'FAIL (false positive)' } }
                elseif (& $mentioned $sections.Blocking) { if ($spec.expect -eq 'Blocking' -and $verdictLine -notmatch 'CHANGES_REQUESTED') { 'FAIL (verdict)' } else { 'PASS (caught)' } }
                elseif (& $mentioned ($sections.Important + $sections.Blocking)) { if ($spec.expect -eq 'Important') { 'PASS (caught)' } else { 'PARTIAL (under-rated)' } }
                elseif (& $mentioned $review) { 'PARTIAL (mentioned, not as a finding)' }
                else { 'FAIL (missed)' }
            $rows += [pscustomobject]@{ Case = $name; Result = $result; Detail = "expected $($spec.expect); $verdictLine"; Cost = [math]::Round($cost, 4) }
        }

        Write-Host "    => $($rows[-1].Result) — $($rows[-1].Detail)" -ForegroundColor $(if ($rows[-1].Result -match '^(PASS|VALID)') { 'Green' } else { 'Yellow' })
        Reset-Worktree
    }
}
finally {
    Set-Location $projectRoot
    if ($worktree) {
        # `git worktree remove` needs git 2.17+; deleting the directory + prune works everywhere.
        Remove-Item -Recurse -Force $worktree -ErrorAction SilentlyContinue
        git worktree prune 2>&1 | Out-Null
    }
}

# Child run of a parallel parent: hand the rows back, the parent writes the report.
if ($RowFile) {
    ConvertTo-Json -InputObject @($rows) -Depth 3 | Set-Content $RowFile -Encoding utf8
    exit 0
}

$passed = @($rows | Where-Object { $_.Result -match '^(PASS|VALID)' }).Count
$summary = "$passed/$($rows.Count) passed, cost `$$([math]::Round($totalCost, 4))"
$report = @(
    "# Evals — $Suite$(if ($ValidateOnly) { ' (validate only)' })",
    '',
    "- Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), ref ``$refLabel``, model: $(if ($Model) { $Model } elseif ($Suite -eq 'fixer') { 'sonnet (default)' } else { 'agent default' }), duration $([math]::Round(((Get-Date) - $started).TotalMinutes, 1)) min",
    "- Command: ``pwsh scripts/run-evals.ps1 -Suite $Suite$(if ($Case -ne '*') { " -Case '$Case'" })$(if ($Model) { " -Model $Model" })$(if ($WorkingTree) { ' -WorkingTree' })$(if ($Parallel -ne 3) { " -Parallel $Parallel" })$(if ($ValidateOnly) { ' -ValidateOnly' })``",
    "- **Result: $summary**",
    '',
    '| Case | Result | Detail | Cost |',
    '|---|---|---|---|'
) + ($rows | ForEach-Object { "| $($_.Case) | $($_.Result) | $($_.Detail -replace '\|', '/') | `$$($_.Cost) |" })
$report | Set-Content "$resultsDir.md" -Encoding utf8

Write-Host "`nEvals $Suite — $summary" -ForegroundColor $(if ($passed -eq $rows.Count) { 'Green' } else { 'Yellow' })
Write-Host "Report: $resultsDir.md" -ForegroundColor DarkGray
exit $(if ($passed -eq $rows.Count) { 0 } else { 1 })
