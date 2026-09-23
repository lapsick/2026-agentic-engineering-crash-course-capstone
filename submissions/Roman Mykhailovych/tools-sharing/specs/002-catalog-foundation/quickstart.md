# Quickstart & Validation Guide

**Feature**: `002-catalog-foundation` | **Date**: 2026-07-28 | **Plan**: [plan.md](./plan.md)

How to run the ToolShare foundation + Catalog slice from a clean checkout and prove it works. Scenarios map one-to-one onto the spec's user stories and success criteria. This is a *validation* guide — implementation details live in [data-model.md](./data-model.md), [contracts/](./contracts/) and (later) `tasks.md`.

---

## Prerequisites

| Requirement | Verify with | Notes |
|---|---|---|
| .NET SDK 10.0.x | `dotnet --list-sdks` | `global.json` pins 10.0.301 (`rollForward: latestFeature`) |
| `dotnet-ef` **10.x** | `dotnet ef --version` | **Required.** A 9.x tool cannot scaffold a .NET 10 model — it fails with `MissingMethodException`. Fix: `dotnet tool update -g dotnet-ef --version 10.*` (only needed for *authoring* migrations, not for running the app) |
| Docker with Compose v2 | `docker compose version` | Needed for the container path **and** for the integration tests |
| Git | `git --version` | |
| Ports 5432 and 8080 free | `docker compose ps` | Adjust in `docker-compose.override.yml` if occupied |

The ABP CLI is **not** required to run the solution — it is a repo-local dotnet tool (`.config/dotnet-tools.json`) restored by `dotnet tool restore`, and only needed when regenerating template assets. The solution targets **.NET 10 with ABP 10.5.0**.

---

## Path A — Run in containers (SC-005: clean checkout → working catalog in < 15 min)

```bash
git clone <repo-url> tools-sharing
cd tools-sharing

docker compose up --build -d
docker compose logs -f app          # watch until "Now listening on: http://[::]:8080"
```

**What happens**: `postgres` starts and passes its `pg_isready` healthcheck; only then does `app` start. The `app` entrypoint runs `dotnet ToolShare.DbMigrator.dll` (because `RUN_MIGRATIONS=true`), which creates the `public` and `catalog` schemas and seeds the `admin` user, the `Librarian` role and the five starter categories — then `exec`s the Blazor host.

**Expected**: <http://localhost:8080> serves the login page. Sign in as `admin` / `1q2w3E*` and reach the catalog.

**Verify the schema split** (Constitution III):

```bash
docker compose exec postgres psql -U toolshare -d toolshare -c '\dn'
# expect: catalog, public

docker compose exec postgres psql -U toolshare -d toolshare -c '\dt catalog.*'
# expect: Categories, Tools, ToolInstances, ToolInstanceStateChanges, ToolInstancePhotos,
#         __EFMigrationsHistory
```

**Verify no cross-schema foreign key** — this must return **zero rows**:

```bash
docker compose exec postgres psql -U toolshare -d toolshare -c "
SELECT con.conname, ns1.nspname AS from_schema, ns2.nspname AS to_schema
FROM pg_constraint con
JOIN pg_class c1 ON c1.oid = con.conrelid  JOIN pg_namespace ns1 ON ns1.oid = c1.relnamespace
JOIN pg_class c2 ON c2.oid = con.confrelid JOIN pg_namespace ns2 ON ns2.oid = c2.relnamespace
WHERE con.contype = 'f' AND ns1.nspname <> ns2.nspname;"
```

**Verify migration idempotence (SC-006, FR-016)**:

```bash
docker compose run --rm --entrypoint "dotnet ToolShare.DbMigrator.dll" app
docker compose exec postgres psql -U toolshare -d toolshare \
  -c 'SELECT count(*) FROM catalog."Categories";'
# expect: still 5 — no duplicated seed rows, no schema error
```

**Verify FR-020 (database unreachable)**:

```bash
docker compose stop postgres
docker compose up app          # app must fail fast with a clear Npgsql connection error
docker compose start postgres
```

Teardown: `docker compose down -v` (drops the database and blob volumes).

---

## Path B — Run locally with the dotnet CLI (Constitution VI: no IDE required)

```bash
docker compose up -d postgres            # database only

dotnet tool restore
dotnet restore
dotnet build ToolShare.sln -c Debug

dotnet run --project src/ToolShare.DbMigrator            # schema + seed
dotnet run --project src/ToolShare.Blazor               # https://localhost:44300
```

Adding a migration after a domain change (module context shown; the host context is analogous):

```bash
dotnet ef migrations add <Name> \
  --project src/ToolShare.Catalog.EntityFrameworkCore \
  --startup-project src/ToolShare.DbMigrator \
  --context CatalogDbContext
```

Never apply migrations from the running app — always through `ToolShare.DbMigrator` (FR-015).

---

## Path C — Run the tests

```bash
dotnet test ToolShare.sln
```

Docker must be running: every integration test project starts a `postgres:16-alpine` container via Testcontainers, applies the migrations once into a template database, and clones it per test class. There is **no** SQLite or in-memory provider anywhere in the solution — verify:

```bash
grep -ri "EntityFrameworkCore.Sqlite\|UseInMemoryDatabase" --include="*.csproj" --include="*.cs" test/ src/
# expect: no matches
```

Fast domain-only loop (no container, sub-second):

```bash
dotnet test test/ToolShare.Catalog.Domain.Tests
```

---

## Validation scenarios

Each scenario maps to a user story in [spec.md](./spec.md). Sign in as `admin` unless stated otherwise.

### V1 — Manage the tool catalog (US1 → SC-001, SC-004)

1. Go to **Catalog → Categories**; confirm the five seeded categories. Create **"Power Tools"** if absent.
2. Go to **Catalog → Tools**, create **"Rotary Hammer"** in *Power Tools*.
3. Open the tool, register instance **`RH-001`** with condition *Good*; register **`RH-002`** with condition *New*.
4. Attempt a third instance with serial **`rh-001`** → rejected with "already used" (normalization is case-insensitive, **SC-004**).
5. Attempt condition value outside the four-level scale → the UI only offers *New/Good/Worn/Damaged* (FR-004).
6. Upload a photo to `RH-001` → it appears in the gallery. Upload a 10 MB file → rejected as too large. Upload a `.txt` → rejected as unsupported format (FR-009).
7. Retire `RH-002` with reason *"Motor burnt out"* → marked retired, hidden from the default instance list, still retrievable with **Show retired**, and its history is intact (FR-006).

**Pass**: steps 1–3 complete in under 3 minutes without help (**SC-001**); steps 4, 6 show clear messages, not stack traces.

### V2 — Find and view tools (US2 → SC-002, SC-008)

1. Search `hamm` → *Rotary Hammer* is listed (partial, case-insensitive).
2. Filter by *Power Tools* → only that category's tools remain.
3. Search `zzzz` → an empty-state message, **not** an error (FR-008).
4. Create a tool named **"Дриль"**; search `дриль`, `ДРИЛЬ` and `дрiль` (Latin `i`) → each finds it (**SC-008**).
5. Open *Rotary Hammer* → instances listed with serial number, condition and photos; `RH-002` hidden until **Show retired**.
6. Set **Available only** → `RH-002` (retired) and any *Damaged* instance are excluded.

**Pass**: locating a specific tool and opening its instance detail takes under 30 seconds (**SC-002**).

### V3 — Secure, role-aware access (US3 → SC-003, SC-009)

1. Sign out, request `/catalog` directly → redirected to login, no catalog content leaks (FR-011).
2. In **Administration → Users**, create `member1` with **no** roles. Sign in as `member1`:
   - browsing, searching and viewing tool detail work;
   - **New / Edit / Retire / Add photo** are hidden;
   - invoking a management operation directly is denied server-side (FR-012, **SC-003**).
3. Assign `member1` the **Librarian** role, sign in again → the same operations now succeed.
4. On a fresh database, `admin` can perform every Catalog management action immediately (**SC-009**).

### V4 — Reproducible run & schema evolution (US4 → SC-005, SC-006)

Covered by Path A: `docker compose up --build`, the schema/`\dn` checks, the idempotence re-run, and the unreachable-database check. Confirm end-to-end elapsed time from clone to a working catalog is under 15 minutes (**SC-005**).

### V5 — Published Catalog boundary (US5 → SC-007)

Automated in `ToolShare.Catalog.Application.Tests`; run it explicitly:

```bash
dotnet test test/ToolShare.Catalog.Application.Tests --filter "FullyQualifiedName~PublicContract"
```

The test acts as a stand-in downstream module and asserts:

1. `IToolInstanceLookupAppService.FindAsync` returns the instance with correct `IsAvailable`, and `IsAvailableAsync` returns `false` for an unknown id — see [contracts/catalog-public-contracts.md](./contracts/catalog-public-contracts.md).
2. Registering an instance raises `ToolInstanceStateChangedEto` with both `Previous*` values `null`; retiring it raises a second event with `NewCirculationState = Retired` and the stated reason — see [contracts/catalog-events.md](./contracts/catalog-events.md).
3. The test file imports **only** `ToolShare.Catalog.ToolInstances` from the contracts assembly — no `Domain` or `EntityFrameworkCore` namespace (**SC-007**).

Manual boundary audit (must return **zero rows**):

```bash
grep -rn "Catalog.Domain\b\|Catalog.EntityFrameworkCore" \
  --include="*.csproj" src/ToolShare.Blazor src/ToolShare.Application src/ToolShare.Domain
# ToolShare.DbMigrator is the composition root and is expected to reference the EF project — exclude it.
```

### V6 — Concurrency and append-only history (FR-010, Constitution IV)

1. Open the same tool in two browser tabs; edit and save in tab 1, then save tab 2 → tab 2 shows a conflict message, and tab 1's change is **not** silently overwritten (FR-010).
2. On an instance, change condition *Good → Worn*, then *Worn → Damaged*, then retire it. Open **History** → four rows in chronological order (registration + three transitions), the first with empty previous values.
3. Confirm no UI or API path edits or deletes a history row:

```bash
docker compose exec postgres psql -U toolshare -d toolshare \
  -c 'SELECT count(*) FROM catalog."ToolInstanceStateChanges";'
# perform another condition change, re-run: the count must only ever increase
```

---

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `The specified SDK version was not found` | .NET 10 SDK missing | Install SDK 10.0.x — `global.json` pins it deliberately so builds are identical everywhere |
| `dotnet ef migrations add` fails with `MissingMethodException: … ArgumentIsEmpty` | `dotnet-ef` is 9.x against a .NET 10 runtime | `dotnet tool update -g dotnet-ef --version 10.*` — EF design-time tooling is not forward-compatible |
| `app` exits immediately, log shows an Npgsql connection failure | Database not ready or wrong credentials | Confirm `docker compose ps` shows `postgres` healthy; check `ConnectionStrings__Default` |
| Tests hang or fail with a Docker endpoint error | Docker not running | Start Docker Desktop/daemon; Testcontainers requires it |
| `relation "catalog.Tools" does not exist` | Migrations not applied | Run `dotnet run --project src/ToolShare.DbMigrator` (Path B) or restart `app` with `RUN_MIGRATIONS=true` |
| Photos vanish after `docker compose down -v` | Blob volume removed | Expected — `-v` deletes the blob volume along with the database |
| Login rejects `admin` / `1q2w3E*` | Password changed, or seeded before a config override | Reset via `docker compose down -v` then `up --build`, or change it in the Identity UI |
