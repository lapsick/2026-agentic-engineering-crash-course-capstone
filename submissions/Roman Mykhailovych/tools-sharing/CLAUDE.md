# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

ToolShare — an ABP Framework 10 (packages 10.6.0) / .NET 10 layered monolith, Blazor Web App
(InteractiveServer), PostgreSQL 16 via EF Core. Built as a **modular monolith**: a host module
(`ToolShare.*`) plus one module per feature area, each with its own PostgreSQL schema —
`ToolShare.Catalog.*` (tools, categories, tool instances), `ToolShare.Membership.*` (roster,
community rules, reliability rating), `ToolShare.Lending.*` (reservations/waitlist,
checkout/return, maintenance requests, librarian reports), and `ToolShare.Notifications.*`.
Lending's `Reservations` table uses a PostgreSQL `EXCLUDE USING gist` constraint (`btree_gist`,
added via raw SQL in its migration — no fluent-API equivalent) to make double-booking impossible
under concurrency. The project is driven by Spec Kit (`.specify/`, `specs/*/spec.md|plan.md|tasks.md`).

**`.specify/memory/constitution.md` is binding.** Read it before making architectural decisions;
every plan and PR is checked against it. Key points repeated here because they shape almost every
change:

- **Fixed stack, non-negotiable**: .NET 10, ABP free/OSS only (no ABP Commercial), Blazor
  InteractiveServer, PostgreSQL 16/EF Core. No alternative ORM, DB, or UI framework.
- **Strict module boundaries**: a module must never reference another module's `Domain` or
  `EntityFrameworkCore` project. The only allowed cross-module coupling is (a) types in the other
  module's `*.Application.Contracts`, and (b) integration events over `ILocalEventBus`.
- **Schema-level data isolation**: each module has its own `DbContext` and PostgreSQL schema; no
  cross-schema foreign keys. Reference another module's data by ID only.
- **Append-only history**: state-changing domain facts that matter for audit (loans, returns,
  condition/circulation changes) are recorded as new rows, never mutated or deleted in place.
- **Membership-gated, not merely authentication-gated**: every application-service call —
  including Catalog's browsing methods — is refused unless the caller resolves to an enrolled,
  **Active** `Member`, enforced by `MembershipMethodInvocationAuthorizationService` (a decorator
  over ABP's `IMethodInvocationAuthorizationService`).
- **Test-first, non-negotiable**: domain rules get unit tests with no database; application-layer
  behavior gets integration tests against a **real PostgreSQL via Testcontainers** — never SQLite
  or in-memory. A feature isn't done until its tests exist and pass. Test rules and commands:
  `.claude/rules/testing.md`.
- **Container-first**: must build/test via plain `dotnet` CLI (no IDE dependency); runs via
  `docker compose` (app + postgres).

## Commands

```bash
dotnet build ToolShare.slnx
dotnet test ToolShare.slnx          # Docker must be running (Testcontainers); no compose needed

# Local dev database (matches appsettings.json)
docker compose up -d
docker compose down                 # add -v to also drop the volume

# Create/migrate the database — must run from its own directory, or appsettings.json won't resolve
cd src/ToolShare.DbMigrator && dotnet run

# Run the Blazor host app
cd src/ToolShare.Blazor && dotnet run
```

## Architecture

### Module anatomy

Every module (host and feature modules) has the same ABP layer set; dependencies only flow
downward through this list:

- `*.Domain.Shared` — enums, error codes, constants, and any **integration event (ETO)** the
  module publishes (here, not in `Application.Contracts`, because `Domain` raises them via
  `AddLocalEvent` and cannot reference `Application.Contracts`).
- `*.Domain` — entities, aggregate roots, domain services, repository *interfaces*. Never
  referenced from outside the module.
- `*.EntityFrameworkCore` — the module's `DbContext` (own schema), EF configurations, repository
  *implementations*. Never referenced from outside the module.
- `*.Application.Contracts` — app-service interfaces, DTOs, permission definitions: the module's
  **published boundary**; other modules and the module's own Blazor UI depend on this only.
- `*.Application` — app-service implementations, mappers.
- `*.Blazor` — Razor components/pages, consuming only `Application.Contracts`.

`ToolShare.HttpApi` and `ToolShare.DbMigrator` sit at the host level; `DbMigrator` is the only
place migrations are applied — never implicitly at request time.

### Stability tiers on a module's public surface (`specs/002-catalog-foundation/contracts/README.md`)

- **Tier 1 — Public, frozen once shipped**: the interfaces/DTOs/ETOs/enums other modules depend on
  (e.g. `IToolInstanceLookupAppService`, `ToolInstanceLookupDto`, `ToolInstanceStateChangedEto`,
  `ToolCondition`, `ToolInstanceCirculationState`). Changes must be additive only (new optional
  members, new enum values with new numbers) — renaming, removing, or renumbering is breaking.
- **Tier 2 — Module-internal**: the rest of `Application.Contracts`, consumed only by that module's
  own Blazor project. Referencing it from another module is a Principle II violation.

Cross-module contract tests resolve the public app service and receive the ETO **without importing
the producing module's `Domain` or `EntityFrameworkCore` namespaces** — the absent import is the
assertion.

### Feature slices within a module

Inside `Domain`, `Application.Contracts`, and `Application`, code is organized by feature slice
(e.g. `Categories/`, `Tools/`, `ToolInstances/`; `Reservations/`, `Loans/`, `Maintenance/`,
`Reports/`), each with its entity, repository interface, app service, DTOs, and mapper. A slice
need not appear in every layer: Lending's read-only `Reports/` has no entity or repository.

### Spec Kit workflow

Features go through `specs/<NNN-feature-name>/`: `spec.md` → `plan.md` → `tasks.md` via the
`speckit-*` skills. A module's `Application.Contracts` are specified before or alongside their
consumers. `spec.md` holds a feature's functional requirements; the constitution holds only
cross-cutting principles — don't conflate the two.
