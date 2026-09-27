<#
.SYNOPSIS
    Post-implement gate for a Spec Kit feature (the after_implement hook in
    .specify/extensions.yml -> /speckit-green-gate): runs fix-until-green.ps1 on every
    test project referenced in the feature's tasks.md, Domain projects first.
    Already green = no agent call. -NoFix = check only.

    Exit codes: 0 all GREEN, 1 some project not green (or none found),
    5 Docker is needed (Application/EF tests) but not running.

.EXAMPLE
    pwsh scripts/speckit-gate.ps1 -FeatureDir specs/004-lending -NoFix
#>
param(
    [string]$FeatureDir,
    [int]$MaxIterations = 5,
    [switch]$NoFix
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)

if (-not $FeatureDir) {
    $FeatureDir = (pwsh -NoProfile -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks | ConvertFrom-Json).FEATURE_DIR
}

$projects = @(
    Select-String -Path "$FeatureDir/tasks.md" -Pattern 'test/(ToolShare\.[A-Za-z.]+?\.Tests)/' -AllMatches |
        ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value } |
        Sort-Object -Unique | Where-Object { Test-Path "test/$_/$_.csproj" } |
        Sort-Object -Stable { $_ -notlike '*.Domain.Tests' }
)
if ($projects.Count -eq 0) {
    Write-Host "No test/ToolShare.*.Tests project in $FeatureDir/tasks.md — nothing to gate (Constitution V requires tests)." -ForegroundColor Yellow
    exit 1
}

if ($projects -notlike '*.Domain.Tests') {
    docker info *> $null
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Docker is not running — Application/EF test projects need it (Testcontainers). Start Docker and rerun.' -ForegroundColor Red
        exit 5
    }
}

Write-Host "Spec Kit gate — $(Split-Path -Leaf $FeatureDir): $($projects -join ', ')" -ForegroundColor Cyan
$failed = @()
foreach ($project in $projects) {
    Write-Host "`n##### $project" -ForegroundColor Cyan
    pwsh -NoProfile -File scripts/fix-until-green.ps1 -Project "test/$project/$project.csproj" -MaxIterations $(if ($NoFix) { 0 } else { $MaxIterations })
    if ($LASTEXITCODE -ne 0) { $failed += "$project (exit $LASTEXITCODE)" }
}

if ($failed.Count -gt 0) {
    Write-Host "`nGate FAIL — $($failed -join ', ')" -ForegroundColor Red
    exit 1
}
Write-Host "`nGate PASS — $($projects -join ', ')" -ForegroundColor Green
