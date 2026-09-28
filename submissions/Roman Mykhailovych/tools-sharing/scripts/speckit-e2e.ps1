<#
.SYNOPSIS
    Final browser acceptance for a Spec Kit feature — the after_converge hook in
    .specify/extensions.yml (speckit.e2e.check -> /speckit-e2e-check). Runs the Playwright suite
    test/ToolShare.E2E.Tests exactly ONCE, check-only: no fix loop, no agent. It is the last step
    before commit/PR: implement -> green gate -> review -> fixes -> gate -> converge -> E2E.

    - SKIPPED (exit 0) when the feature's tasks.md touches no src/ToolShare.*Blazor/ code (-Force
      runs it anyway).
    - REFUSED (exit 1) while tasks.md still has open "- [ ]" tasks (converge added work) or the
      green gate is not PASS for the current working tree — checked via
      `speckit-gate.ps1 -NoFix -ReuseVerdict`, which is instant when nothing changed since the
      last gate run.
    - On failure every red test leaves <Class>.<Test>.trace.zip + .png in the run folder; open a
      trace with `pwsh test/ToolShare.E2E.Tests/bin/Debug/net10.0/playwright.ps1 show-trace <zip>`.

    Run folder: green-runs/<timestamp>-e2e/. Verdict: green-runs/last-e2e.json.
    Exit codes: 0 PASS or SKIPPED, 1 FAIL or REFUSED, 5 Docker not running.

.EXAMPLE
    pwsh scripts/speckit-e2e.ps1
    pwsh scripts/speckit-e2e.ps1 -FeatureDir specs/008-out-of-band-maintenance -Force
#>
param(
    [string]$FeatureDir,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not $FeatureDir) {
    $FeatureDir = (pwsh -NoProfile -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks | ConvertFrom-Json).FEATURE_DIR
}
$feature = Split-Path -Leaf $FeatureDir
$tasks = "$FeatureDir/tasks.md"

if (-not $Force -and -not (Select-String -Path $tasks -Pattern 'src/ToolShare\.([A-Za-z]+\.)?Blazor/' -Quiet)) {
    Write-Host "E2E SKIPPED — $feature touches no Blazor code (-Force to run anyway)." -ForegroundColor DarkGray
    exit 0
}

$open = @(Select-String -Path $tasks -Pattern '^\s*- \[ \]')
if ($open.Count -gt 0) {
    Write-Host "E2E REFUSED — $($open.Count) open task(s) in tasks.md; finish them with /speckit-implement first." -ForegroundColor Red
    exit 1
}

pwsh -NoProfile -File scripts/speckit-gate.ps1 -FeatureDir $FeatureDir -NoFix -ReuseVerdict
if ($LASTEXITCODE -eq 5) { exit 5 }
if ($LASTEXITCODE -ne 0) {
    Write-Host 'E2E REFUSED — the green gate is not PASS for this working tree; run /speckit-green-gate first.' -ForegroundColor Red
    exit 1
}

docker info *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Host 'Docker is not running — the E2E suite needs it (PostgreSQL via Testcontainers). Start Docker and rerun.' -ForegroundColor Red
    exit 5
}

$runDir = "green-runs/$(Get-Date -Format 'yyyyMMdd-HHmmss')-e2e"
New-Item -ItemType Directory -Force $runDir | Out-Null
$env:TOOLSHARE_E2E_ARTIFACTS = (Resolve-Path $runDir).Path

Write-Host "`nE2E — $feature (once, check-only)" -ForegroundColor Cyan
& dotnet test test/ToolShare.E2E.Tests/ToolShare.E2E.Tests.csproj --nologo `
    --logger 'trx;LogFileName=results.trx' --results-directory $runDir 2>&1 |
    ForEach-Object { "$_" } | Tee-Object -Variable output | Out-Host
$code = $LASTEXITCODE
$output = @($output)
$output | Set-Content "$runDir/dotnet-test.log" -Encoding utf8

if ($output -match 'DockerUnavailableException|Docker is either not running|Cannot connect to the Docker daemon') {
    Write-Host "E2E ENVIRONMENT — Docker/Testcontainers unavailable. Log: $runDir" -ForegroundColor Red
    exit 5
}

$summary = "$(@($output | Where-Object { $_ -match '^\s*(Passed|Failed)!' })[-1])".Trim()
$failed = @($output | Where-Object { $_ -match '^\s+Failed\s+\S' } |
    ForEach-Object { $_.Trim() -replace '^Failed\s+', '' -replace '\s+\[[^\]]*\]$', '' } | Select-Object -Unique)
$traces = @(Get-ChildItem $runDir -Filter '*.trace.zip' | ForEach-Object { "$runDir/$($_.Name)" })
$verdict = if ($code -eq 0) { 'PASS' } else { 'FAIL' }

[ordered]@{
    feature = $feature
    verdict = $verdict
    exit    = [int]($code -ne 0)
    summary = $summary
    failed  = $failed
    traces  = $traces
    at      = (Get-Date -Format 's')
    log     = $runDir
} | ConvertTo-Json | Set-Content 'green-runs/last-e2e.json' -Encoding utf8

Write-Host "`nE2E $verdict — $summary" -ForegroundColor $(if ($code -eq 0) { 'Green' } else { 'Red' })
$failed | ForEach-Object { Write-Host "  x $_" -ForegroundColor Red }
$traces | ForEach-Object { Write-Host "  trace: $_" -ForegroundColor DarkGray }
Write-Host "Log: $runDir" -ForegroundColor DarkGray
exit $(if ($code -eq 0) { 0 } else { 1 })
