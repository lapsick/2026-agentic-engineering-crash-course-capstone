<!--
Sync Impact Report
==================
Version change: 1.0.0 → 2.0.0
Bump rationale: MAJOR — Principle I (NON-NEGOTIABLE) redefines the mandated
stack from .NET 9 / ABP 9.3.x to .NET 10 / ABP 10.5.x, at the explicit
direction of the project owner during planning of 002-catalog-foundation.
ABP 10.x is the actively developed line; .NET 9 was the trailing release.
Adopting the new target before any production code exists avoids a costly
port later. Scope is confined to target framework, package versions and
container base images — no other principle (II–VI) is affected.

Principles defined (6):
  I.   Fixed Stack (NON-NEGOTIABLE)
  II.  Modular Monolith With Strict Boundaries
  III. Schema-Level Data Isolation; No Cross-Module FKs
  IV.  Append-Only History
  V.   Test-First Discipline (NON-NEGOTIABLE)
  VI.  IDE-Agnostic, Container-First

Sections defined:
  - Technology & Architecture Constraints (was placeholder SECTION_2)
  - Development Workflow (was placeholder SECTION_3)
  - Governance

Templates & artifacts reviewed for consistency:
  ✅ .specify/templates/plan-template.md — "Constitution Check" gate reads the
     constitution dynamically; no hard-coded principle references to update.
  ✅ .specify/templates/spec-template.md — generic; no constitution conflict.
  ✅ .specify/templates/tasks-template.md — updated: the "Tests are OPTIONAL"
     note now records that Principle V makes tests mandatory for this project.
  ✅ .claude/skills/speckit-*/SKILL.md — reviewed; no outdated/agent-specific
     references requiring changes.
  ✅ specs/001-tool-library, specs/002-catalog-foundation — no contradictions;
     002's stack (ABP/Blazor/PostgreSQL/DbMigrator/compose) matches Principles I & VI.

Deferred TODOs: none. All placeholder tokens resolved; both governance dates
supplied as 2026-07-27.
-->

# ToolShare Constitution

## Core Principles

### I. Fixed Stack (NON-NEGOTIABLE)
The system is built exclusively on .NET 10, ABP Framework 10.5.x (free/open-source),
Blazor Web App in InteractiveServer mode, and PostgreSQL 16 via EF Core.
Only free ABP modules are permitted; ABP Commercial modules are forbidden.
Replacing any of these components, or introducing an alternative (a different
ORM, database, or UI framework), is a constitution violation and requires an
explicit amendment. A fixed stack removes a whole class of decisions from every
feature and keeps generated code consistent across modules.

### II. Modular Monolith With Strict Boundaries
The system is a modular monolith structured as ABP modules. Each module is a
distinct set of projects (Domain, Domain.Shared, Application,
Application.Contracts, EntityFrameworkCore, Blazor). A module MUST NOT
reference another module's Domain or EntityFrameworkCore layer. The only
permitted cross-module coupling is through interfaces and DTOs exposed in
another module's *.Application.Contracts, and through integration events on
ILocalEventBus. Any cross-module interaction outside these two channels is a
violation. Strict boundaries are the entire point of a modular monolith;
without them it degrades into a big ball of mud wearing the costume of structure.

### III. Schema-Level Data Isolation; No Cross-Module FKs
Each module owns its own DbContext, mapped to its own PostgreSQL schema within
the shared database. Cross-schema foreign keys are forbidden. A module refers
to data owned by another module only by identifier, and consistency across
modules is maintained through integration events, never through database-level
referential integrity. This keeps modules independently evolvable and prevents
the database from silently becoming the coupling that the code forbids.

### IV. Append-Only History
State-changing domain facts that matter for audit (loans, returns, maintenance
records, instance state changes) are recorded as append-only history. Existing
history records MUST NOT be mutated or deleted; corrections are new records that
supersede prior ones. This guarantees an auditable trail and makes the past
reconstructable at any point.

### V. Test-First Discipline (NON-NEGOTIABLE)
Every module ships with tests using xUnit and the ABP test base. Domain rules
are covered by unit tests without a database. Application-layer behavior is
covered by integration tests running against real PostgreSQL via Testcontainers
— never against an in-memory or SQLite substitute, because schema mapping and
provider behavior are part of what is under test. A feature is not "done" until
its tests exist and pass.

### VI. IDE-Agnostic, Container-First
The solution MUST build and its tests MUST run through the dotnet CLI, with no
dependency on a specific IDE (both VS Code and Visual Studio are first-class,
neither is required). The application runs in Docker via docker compose
(app + postgres), built with a multi-stage Dockerfile. Migrations are applied
by a dedicated DbMigrator project, never implicitly at request time in
production. If it only works on one developer's machine or in one IDE, it is
broken.

## Technology & Architecture Constraints

Authentication and authorization use ABP's built-in IdentityModule and
permission system; hand-rolled auth is forbidden. Background work (reminders,
scheduled recalculations) uses ABP Background Workers/Jobs rather than ad-hoc
timers or external schedulers. Pure domain algorithms (e.g. eligibility checks,
rating calculations) MUST live in the Domain layer, free of EF Core and ABP
infrastructure dependencies, so they are unit-testable in isolation. Read models
that aggregate data are rebuilt by handling domain events, not by database
triggers or cross-schema queries.

## Development Workflow

Work proceeds one feature (one ABP module, or the shared foundation) at a time,
following the Spec Kit flow: Specify → Clarify → Plan → Tasks → Implement. A
module's public contracts (Application.Contracts) are defined before, or
alongside, the consumers that depend on them; downstream modules integrate
against those contracts and events, never against internal types. Each feature's
spec.md holds its functional requirements; this constitution holds only
cross-cutting principles, and the two MUST NOT be conflated.

## Governance

This constitution supersedes conflicting practices, plans, and generated code.
Every plan and implementation MUST be checked against these principles; a plan
that violates a principle is rejected or the constitution is formally amended
first — it is never silently overridden. Amendments require updating this
document, bumping the version, and noting the change. Added complexity (new
projects, new coupling, new infrastructure) MUST be justified against Principles
II and III before adoption; when in doubt, prefer the simpler option that keeps
module boundaries intact.

**Version**: 2.0.0 | **Ratified**: 2026-07-27 | **Last Amended**: 2026-07-28
