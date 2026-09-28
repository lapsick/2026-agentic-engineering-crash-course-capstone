<#
.SYNOPSIS
    Post-implement gate for a Spec Kit feature (the after_implement hook in
    .specify/extensions.yml -> /speckit-green-gate) over every test project referenced in the
    feature's tasks.md. The browser suite test/ToolShare.E2E.Tests is deliberately NOT part of
    this gate or its fix loops: it runs once, check-only, at the very end (scripts/speckit-e2e.ps1,
    the after_converge hook).

    1. Check: one `dotnet build ToolShare.slnx`, one module-boundary audit, then all test projects
       with --no-build in parallel (each Application/EF assembly has its own container).
    2. Fix (skipped with -NoFix or when green): fix-until-green.ps1 only on the red projects.
    3. Re-check once after any fixing — a fix for one project can break another.
    Already green = no agent call. The verdict is saved to green-runs/last-gate.json with the
    working-tree fingerprint; -ReuseVerdict returns it without running anything when nothing
    changed since (used by /speckit-code-review).

    Exit codes: 0 all GREEN, 1 some project not green (or none found),
    5 Docker is needed (Application/EF tests) but not running.

.EXAMPLE
    pwsh scripts/speckit-gate.ps1 -FeatureDir specs/004-lending -NoFix
    pwsh scripts/speckit-gate.ps1 -NoFix -ReuseVerdict
#>
param(
    [string]$FeatureDir,
    [int]$MaxIterations = 5,
    [string]$Model = 'sonnet',
    [int]$ThrottleLimit = 4,
    [switch]$NoFix,
    [switch]$ReuseVerdict
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
. "$PSScriptRoot/working-tree-snapshot.ps1"

if (-not $FeatureDir) {
    $FeatureDir = (pwsh -NoProfile -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks | ConvertFrom-Json).FEATURE_DIR
}
$feature = Split-Path -Leaf $FeatureDir

$referenced = @(
    Select-String -Path "$FeatureDir/tasks.md" -Pattern 'test/(ToolShare\.[A-Za-z.]+?\.Tests)/' -AllMatches |
        ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value }
)
# Cheapest first (the order fix loops run in): Domain (seconds, no Docker) -> Application/EF
# (Testcontainers). E2E is excluded even when tasks.md names it — see the synopsis.
$projects = @(
    $referenced | Sort-Object -Unique | Where-Object { $_ -ne 'ToolShare.E2E.Tests' -and (Test-Path "test/$_/$_.csproj") } |
        Sort-Object -Stable { $_ -notlike '*.Domain.Tests' }
)
if ($projects.Count -eq 0) {
    Write-Host "No test/ToolShare.*.Tests project in $FeatureDir/tasks.md — nothing to gate (Constitution V requires tests)." -ForegroundColor Yellow
    exit 1
}

# reviews/ is written after the gate, so it must not change the fingerprint.
$verdictFile = 'green-runs/last-gate.json'
if ($ReuseVerdict -and (Test-Path $verdictFile)) {
    $last = Get-Content $verdictFile -Raw | ConvertFrom-Json
    if ($last.feature -eq $feature -and $last.tree -eq (Get-WorkingTreeSnapshot -Exclude 'reviews').Tree) {
        Write-Host "Gate $($last.verdict) (reused from $($last.at) — working tree unchanged since) — $($last.detail)" -ForegroundColor $(if ($last.exit -eq 0) { 'Green' } else { 'Red' })
        Write-Host "Log: $($last.log)" -ForegroundColor DarkGray
        exit $last.exit
    }
}

if ($projects -notlike '*.Domain.Tests') {
    docker info *> $null
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Docker is not running — Application/EF test projects need it (Testcontainers). Start Docker and rerun.' -ForegroundColor Red
        exit 5
    }
}

$runDir = "green-runs/$(Get-Date -Format 'yyyyMMdd-HHmmss')-gate"
New-Item -ItemType Directory -Force $runDir | Out-Null
$environmentErrors = 'DockerUnavailableException|Docker is either not running|Cannot connect to the Docker daemon'

# Runs one test project against the already-built solution. A string, because
# ForEach-Object -Parallel can't take a script block through $using:.
$testOne = @'
param([string]$root, [string]$project, [string]$log, [string]$environmentErrors)
Set-Location $root
$out = @(& dotnet test "test/$project/$project.csproj" --no-build --nologo 2>&1 | ForEach-Object { "$_" })
$code = $LASTEXITCODE
$out | Set-Content $log -Encoding utf8
[pscustomobject]@{
    Project     = $project
    Exit        = $code
    Summary     = "$(@($out | Where-Object { $_ -match '^\s*(Passed|Failed)!' })[-1])".Trim()
    Failed      = @($out | Where-Object { $_ -match '^\s+Failed\s+\S' } |
        ForEach-Object { $_.Trim() -replace '^Failed\s+', '' -replace '\s+\[[^\]]*\]$', '' } | Select-Object -Unique)
    Environment = [bool]($out -match $environmentErrors)
}
'@

function Invoke-Check([string]$label) {
    Write-Host "`n[$label] build ToolShare.slnx + module-boundary audit, then $($projects -join ', ')" -ForegroundColor Cyan

    $build = @(& dotnet build ToolShare.slnx -nologo -v q -clp:ErrorsOnly 2>&1 | ForEach-Object { "$_" })
    $buildOk = $LASTEXITCODE -eq 0
    $build | Set-Content "$runDir/$label-build.log" -Encoding utf8
    $buildErrors = @($build | Where-Object { $_ -match ': error ' } | Sort-Object -Unique | Select-Object -First 20)

    $audit = @(& pwsh -NoProfile -File scripts/check-module-boundaries.ps1 2>&1 | ForEach-Object { "$_" })
    $auditOk = $LASTEXITCODE -eq 0
    $audit | Set-Content "$runDir/$label-boundaries.log" -Encoding utf8
    $violations = @($audit | Where-Object { $_ -match ' -> ' } | ForEach-Object { $_.Trim() })

    $results = @()
    if ($buildOk) {
        $logPrefix = "$root/$runDir/$label"
        $results += @($projects | ForEach-Object -ThrottleLimit $ThrottleLimit -Parallel {
            $prefix = $using:logPrefix
            & ([scriptblock]::Create($using:testOne)) $using:root $_ "$prefix-$_.log" $using:environmentErrors
        })
    }

    if (-not $buildOk) { Write-Host '    BUILD FAILED' -ForegroundColor Red; $buildErrors | ForEach-Object { Write-Host "      $_" -ForegroundColor Red } }
    if (-not $auditOk) { $violations | ForEach-Object { Write-Host "    module boundary violation (Constitution II): $_" -ForegroundColor Red } }
    foreach ($r in $results | Sort-Object { $projects.IndexOf($_.Project) }) {
        Write-Host "    $(if ($r.Exit -eq 0) { 'PASS' } else { 'FAIL' }) $($r.Project) — $($r.Summary)" -ForegroundColor $(if ($r.Exit -eq 0) { 'Green' } else { 'Red' })
        $r.Failed | Select-Object -First 10 | ForEach-Object { Write-Host "      x $_" -ForegroundColor Red }
    }

    return [pscustomobject]@{
        BuildOk     = $buildOk
        AuditOk     = $auditOk
        Results     = $results
        Red         = @($results | Where-Object Exit -ne 0 | ForEach-Object Project)
        Environment = [bool]($results | Where-Object Environment)
    }
}

Write-Host "Spec Kit gate — ${feature}: $($projects -join ', ')" -ForegroundColor Cyan
$check = Invoke-Check 'check'
if ($check.Environment) {
    Write-Host "`nGate ENVIRONMENT — Docker/Testcontainers unavailable; no agent was invoked. Logs: $runDir" -ForegroundColor Red
    exit 5
}

$loopStatus = @()
$isGreen = { param($c) $c.BuildOk -and $c.AuditOk -and $c.Red.Count -eq 0 }
if (-not (& $isGreen $check) -and -not $NoFix) {
    # A build failure hides which projects are red: loop all of them. Only the audit red: the first.
    $loopProjects = if (-not $check.BuildOk) { $projects } elseif ($check.Red.Count) { $check.Red } else { @($projects[0]) }
    foreach ($project in $loopProjects) {
        Write-Host "`n##### fix loop: $project" -ForegroundColor Cyan
        $loopArgs = @('-Project', "test/$project/$project.csproj", '-MaxIterations', $MaxIterations, '-Model', $Model)
        if ($check.AuditOk) { $loopArgs += '-SkipInitialAudit' }
        pwsh -NoProfile -File scripts/fix-until-green.ps1 @loopArgs
        $loopStatus += "$project (loop exit $LASTEXITCODE)"
    }
    $check = Invoke-Check 'recheck'
}

$failed = @()
if (-not $check.BuildOk) { $failed += 'build' }
if (-not $check.AuditOk) { $failed += 'module boundaries' }
$failed += $check.Red
$exit = if ($failed.Count) { 1 } else { 0 }
$verdict = if ($exit) { 'FAIL' } else { 'PASS' }
$detail = if ($exit) { $failed -join ', ' } else { $projects -join ', ' }

[ordered]@{
    feature = $feature
    tree    = (Get-WorkingTreeSnapshot -Exclude 'reviews').Tree
    verdict = $verdict
    exit    = $exit
    detail  = $detail
    at      = (Get-Date -Format 's')
    log     = $runDir
} | ConvertTo-Json | Set-Content $verdictFile -Encoding utf8

if ($loopStatus) { Write-Host "`nFix loops: $($loopStatus -join ', ')" -ForegroundColor DarkGray }
Write-Host "`nGate $verdict — $detail" -ForegroundColor $(if ($exit) { 'Red' } else { 'Green' })
Write-Host "Logs: $runDir" -ForegroundColor DarkGray
exit $exit
