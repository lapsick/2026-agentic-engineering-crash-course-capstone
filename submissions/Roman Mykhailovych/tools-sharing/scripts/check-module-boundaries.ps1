<#
.SYNOPSIS
    Constitution II boundary audit: no project under src/ may reference a
    module's Domain or EntityFrameworkCore project by ProjectReference,
    except that module's own project (self-reference doesn't count) and the
    two host composition roots (ToolShare.Blazor, ToolShare.DbMigrator),
    which are the only places a module is wired into the running app.

    One narrower host exemption: ToolShare.EntityFrameworkCore may reference
    a module's *EntityFrameworkCore* project (never its Domain). Since 003,
    every module's schema is consolidated into the host's ToolShareDbContext
    via [ReplaceDbContext(typeof(I<Module>DbContext))] + builder.Configure<Module>(),
    with all migrations living in src/ToolShare.EntityFrameworkCore/Migrations
    (documented deviation: specs/004-lending/tasks.md T036,
    specs/005-notifications/tasks.md T028). That is host-level composition,
    not one feature module reaching into another.

    Only src/ is scanned — Constitution II targets the shipped application;
    a module's own test project referencing its own Domain/EntityFrameworkCore
    directly (to seed data, drive migrations, etc.) is an accepted,
    documented test-only exception (see tasks.md T050's note), not a
    violation of the module boundary.

.DESCRIPTION
    Generalized beyond just the Catalog module: for every "*.Domain.csproj"
    and "*.EntityFrameworkCore.csproj" under src/, finds every other src/
    project that references it and flags any referencer that isn't (a) a
    project belonging to the same module, (b) one of the host roots, or
    (c) the host EF project referencing a module EF project.

    Exit code 0 = no violations, non-zero = violations found (or the script
    itself failed unexpectedly).
#>

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$srcDir = Join-Path $repoRoot 'src'

$allowedReferencers = @('ToolShare.Blazor', 'ToolShare.DbMigrator')

# Referencer -> the only boundary-project suffix it may reference in another
# module (see the ToolShareDbContext consolidation note in the synopsis).
$allowedLayerReferencers = @{ 'ToolShare.EntityFrameworkCore' = '.EntityFrameworkCore' }

# Longest-suffix-first so ".Application.Contracts" strips before ".Application" would.
$layerSuffixes = @('.Application.Contracts', '.EntityFrameworkCore', '.Domain.Shared', '.Application', '.Domain', '.Blazor', '.HttpApi')

function Get-ModuleName([string]$projectFileBaseName) {
    # Strips the ABP layer suffix to group a project with its own module's
    # other layers, e.g. "ToolShare.Catalog.Domain" and
    # "ToolShare.Catalog.Application" both -> "ToolShare.Catalog"; "ToolShare.Domain"
    # and "ToolShare.Application" (the host) both -> "ToolShare".
    foreach ($suffix in $layerSuffixes) {
        if ($projectFileBaseName.EndsWith($suffix)) {
            return $projectFileBaseName.Substring(0, $projectFileBaseName.Length - $suffix.Length)
        }
    }
    return $projectFileBaseName
}

$boundaryProjects = Get-ChildItem -Path $srcDir -Recurse -Filter '*.csproj' |
    Where-Object { $_.BaseName -match '\.(Domain|EntityFrameworkCore)$' }

if ($boundaryProjects.Count -eq 0) {
    Write-Host "No *.Domain.csproj / *.EntityFrameworkCore.csproj projects found under $srcDir — nothing to audit."
    exit 0
}

$allProjects = Get-ChildItem -Path $srcDir -Recurse -Filter '*.csproj'

$violations = @()

foreach ($boundaryProject in $boundaryProjects) {
    $boundaryModule = Get-ModuleName $boundaryProject.BaseName
    $boundaryFileName = $boundaryProject.Name

    foreach ($candidate in $allProjects) {
        if ($candidate.FullName -eq $boundaryProject.FullName) {
            continue
        }

        $candidateModule = Get-ModuleName $candidate.BaseName
        if ($candidateModule -eq $boundaryModule) {
            # Same module referencing its own Domain/EntityFrameworkCore
            # project (e.g. Catalog.Application -> Catalog.Domain) is normal
            # internal layering, not a cross-module violation.
            continue
        }

        $content = Get-Content -Path $candidate.FullName -Raw
        if ($content -match [regex]::Escape($boundaryFileName)) {
            if ($allowedReferencers -contains $candidate.BaseName) {
                continue
            }

            $allowedSuffix = $allowedLayerReferencers[$candidate.BaseName]
            if ($allowedSuffix -and $boundaryProject.BaseName.EndsWith($allowedSuffix)) {
                continue
            }

            $violations += [pscustomobject]@{
                Referencer = $candidate.BaseName
                References = $boundaryProject.BaseName
                File       = $candidate.FullName.Substring($repoRoot.Length + 1)
            }
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Host "Module boundary violations found (Constitution II):" -ForegroundColor Red
    $violations | ForEach-Object {
        Write-Host "  $($_.Referencer) -> $($_.References)  ($($_.File))" -ForegroundColor Red
    }
    exit 1
}

Write-Host "No module boundary violations found. Checked $($boundaryProjects.Count) Domain/EntityFrameworkCore project(s) against $($allProjects.Count) src/ projects." -ForegroundColor Green
exit 0
