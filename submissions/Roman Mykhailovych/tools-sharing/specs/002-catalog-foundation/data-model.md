# Phase 1 Data Model: Catalog Module

**Feature**: `002-catalog-foundation` | **Date**: 2026-07-28 | **Plan**: [plan.md](./plan.md)

Everything here lives in the PostgreSQL schema **`catalog`**, owned by `CatalogDbContext`. Foreign keys exist only inside this schema; user references (`CreatorId`, `LastModifierId`, `ChangedByUserId`) are plain `uuid` columns with **no** FK to `public."AbpUsers"` (Constitution III).

## Aggregate overview

```text
Category  (aggregate root)
   ▲ 1
   │            Tool  (aggregate root)
   └────────────  CategoryId                       ← FK within `catalog`
                     ▲ 1
                     │
                     └──  ToolInstance  (aggregate root)
                              ToolId               ← FK within `catalog`
                              ├── ToolInstancePhoto      (child entity, 0..MaxPerInstance)
                              └── ToolInstanceStateChange (child entity, append-only, 1..*)
```

`ToolInstance` is a **separate aggregate root** rather than a child of `Tool`: it has its own lifecycle, its own history, and it is the thing Lending will reference by id (see [contracts/catalog-public-contracts.md](./contracts/catalog-public-contracts.md)). Serial-number uniqueness is therefore catalog-wide and enforced by `ToolInstanceManager` plus a unique database index, not by an aggregate invariant.

---

## Enums (`ToolShare.Catalog.Domain.Shared`)

These are part of the published boundary — downstream modules see them transitively through `Catalog.Application.Contracts`.

### `ToolCondition`

| Value | Numeric | Meaning |
|---|---|---|
| `New` | 0 | Unused or as-new |
| `Good` | 1 | Fully serviceable, normal wear |
| `Worn` | 2 | Serviceable, visible wear, may need attention soon |
| `Damaged` | 3 | Not serviceable; not available |

Fixed 4-level scale from [specs/001-tool-library/spec.md](../001-tool-library/spec.md). No value may be added, removed or renumbered by this feature (FR-004).

### `ToolInstanceCirculationState`

| Value | Numeric | Meaning |
|---|---|---|
| `InCirculation` | 0 | Part of the active pool |
| `Retired` | 1 | Permanently withdrawn; record and history preserved |

`OnLoan` and `UnderMaintenance` are **out of scope** and are introduced by features 003+ (spec Assumptions). New members will be appended with higher numeric values so existing persisted data is unaffected.

---

## Entity: `Category`

`FullAuditedAggregateRoot<Guid>` — table `catalog."Categories"`.

| Property | Type | Constraints |
|---|---|---|
| `Id` | `Guid` | PK |
| `Name` | `string` | Required, 2–128 chars, display form as entered |
| `NormalizedName` | `string` | Required, 2–128, derived from `Name`, **unique index** |
| `Description` | `string?` | ≤ 512 chars |
| `ConcurrencyStamp` | `string` | From `AggregateRoot` — FR-010 |
| Audit props | — | `CreationTime`, `CreatorId`, `LastModificationTime`, `LastModifierId`, `IsDeleted`, … |

**Rules**
- `CR-01` — `Name` is required and trimmed; empty/whitespace is rejected.
- `CR-02` — `NormalizedName` is always recomputed by `CatalogTextNormalizer.Normalize(Name)` whenever `Name` is set; it is never assigned directly.
- `CR-03` — `NormalizedName` is unique across the catalog; a duplicate is rejected with a business exception (`Catalog:CategoryNameAlreadyExists`).
- `CR-04` — A category **cannot be deleted while any `Tool` references it** (FR-001). Enforced by `CategoryManager.DeleteAsync`, which counts tools first and throws `Catalog:CategoryHasTools`. Soft delete does not bypass this check.

---

## Entity: `Tool`

`FullAuditedAggregateRoot<Guid>` — table `catalog."Tools"`.

| Property | Type | Constraints |
|---|---|---|
| `Id` | `Guid` | PK |
| `CategoryId` | `Guid` | Required, **FK** → `catalog."Categories"`, `ON DELETE RESTRICT`, indexed |
| `Name` | `string` | Required, 2–256 chars |
| `NormalizedName` | `string` | Required, derived from `Name`, **indexed** (not unique — two tools may share a name in different categories) |
| `Description` | `string?` | ≤ 2 048 chars |
| `ConcurrencyStamp` | `string` | FR-010 |
| Audit props | — | — |

**Rules**
- `TR-01` — `Name` required and trimmed (FR-002).
- `TR-02` — Exactly one owning `CategoryId`; it must reference an existing category (checked in `ToolAppService` before persisting, in addition to the FK).
- `TR-03` — `NormalizedName` recomputed on every `Name` change; it backs the accent- and case-insensitive search (FR-007, SC-008).
- `TR-04` — A tool may be deleted only when it has no instances; otherwise `Catalog:ToolHasInstances`. Instances are retired, never orphaned (edge case in spec).

---

## Entity: `ToolInstance`

`FullAuditedAggregateRoot<Guid>` — table `catalog."ToolInstances"`.

| Property | Type | Constraints |
|---|---|---|
| `Id` | `Guid` | PK |
| `ToolId` | `Guid` | Required, **FK** → `catalog."Tools"`, `ON DELETE RESTRICT`, indexed |
| `SerialNumber` | `string` | Required, 1–64 chars, display form as entered |
| `NormalizedSerialNumber` | `string` | Required, derived, **unique index across the whole catalog** |
| `Condition` | `ToolCondition` | Required |
| `CirculationState` | `ToolInstanceCirculationState` | Required, defaults to `InCirculation` |
| `RetirementReason` | `string?` | ≤ 512; required when `CirculationState == Retired`, otherwise `null` |
| `RetiredAt` | `DateTime?` | UTC; set together with `RetirementReason` |
| `Notes` | `string?` | ≤ 1 024 chars |
| `Photos` | `ICollection<ToolInstancePhoto>` | Owned child collection, 0..`MaxPerInstance` |
| `StateHistory` | `ICollection<ToolInstanceStateChange>` | Owned child collection, append-only, ≥ 1 |
| `ConcurrencyStamp` | `string` | FR-010 |
| Audit props | — | — |

**Derived (not persisted)**
- `IsAvailable` ⇔ `CirculationState == InCirculation && Condition != Damaged`. Implemented as a pure domain property and reused by the search filter and the public lookup contract.

**Rules**
- `IR-01` — `SerialNumber` is required, trimmed, 1–64 chars, and matches `^[A-Za-z0-9][A-Za-z0-9\-_/\.]*$` (leading alphanumeric, then alphanumerics and `- _ / .`). Violations raise `Catalog:InvalidSerialNumber`.
- `IR-02` — `NormalizedSerialNumber = CatalogTextNormalizer.Normalize(SerialNumber)`; the unique index is on the normalized value, so `rh-001` and `RH-001` collide (FR-003, SC-004). Duplicates raise `Catalog:SerialNumberAlreadyExists`. `ToolInstanceManager` performs the pre-check for a friendly message; the index is the authority under concurrency.
- `IR-03` — Registration sets `CirculationState = InCirculation`, accepts any of the four `ToolCondition` values, and **appends the first history row** with both `Previous*` values `null`.
- `IR-04` — `ChangeCondition(newCondition, reason, changedAt, changedByUserId)`: rejected if `CirculationState == Retired` (`Catalog:InstanceIsRetired`); rejected if `newCondition == Condition` (`Catalog:ConditionUnchanged`); otherwise any of the four values is permitted in either direction, and one history row is appended.
- `IR-05` — `Retire(reason, retiredAt, retiredByUserId)`: allowed only from `InCirculation` (`Catalog:InstanceAlreadyRetired`); `reason` required, ≤ 512 chars; sets `CirculationState = Retired`, `RetirementReason`, `RetiredAt`, and appends one history row. `Retired` is **terminal** in this feature — no un-retire operation exists.
- `IR-06` — Retiring never deletes: the record, its photos and its full history stay retrievable (FR-006).
- `IR-07` — Every mutation covered by `IR-03`–`IR-05` also calls `AddLocalEvent(new ToolInstanceStateChangedEto(...))`, so the event is dispatched by the unit of work only if persistence succeeds (FR-018).
- `IR-08` — `AddPhoto` rejects a photo when the instance already holds `CatalogPhotoOptions.MaxPerInstance` photos (`Catalog:TooManyPhotos`). The first photo added becomes `IsPrimary`; removing the primary promotes the next by `DisplayOrder`.

### State transitions

```text
                       register(condition ∈ {New,Good,Worn,Damaged})
                              │
                              ▼
                     ┌──────────────────┐
     changeCondition │  InCirculation   │  ── retire(reason) ──▶  ┌──────────┐
     (any → any,     │  Condition: any  │                          │ Retired  │  (terminal
      not same,      └──────────────────┘                          └──────────┘   in this
      loops back)             ▲   │                                      ▲        feature)
                              └───┘                                      │
                                                        changeCondition / retire → rejected
```

Every arrow — including the initial `register` — appends exactly one `ToolInstanceStateChange` row and raises exactly one `ToolInstanceStateChangedEto`.

---

## Entity: `ToolInstanceStateChange` (append-only)

`Entity<Guid>` inside the `ToolInstance` aggregate — table `catalog."ToolInstanceStateChanges"`.

| Property | Type | Constraints |
|---|---|---|
| `Id` | `Guid` | PK |
| `ToolInstanceId` | `Guid` | Required, **FK** → `catalog."ToolInstances"`, `ON DELETE CASCADE`, indexed with `ChangedAt` |
| `PreviousCondition` | `ToolCondition?` | `null` only on the registration row |
| `NewCondition` | `ToolCondition` | Required |
| `PreviousCirculationState` | `ToolInstanceCirculationState?` | `null` only on the registration row |
| `NewCirculationState` | `ToolInstanceCirculationState` | Required |
| `Reason` | `string?` | ≤ 512; required for retirement, optional otherwise |
| `ChangedAt` | `DateTime` | UTC, required |
| `ChangedByUserId` | `Guid?` | No FK (Constitution III) |

**Rules**
- `HR-01` — **Append-only** (Constitution IV): all properties have private setters and are assigned once in the constructor. No update or delete method exists on the entity, the aggregate, or `IToolInstanceRepository`. Corrections are recorded as new rows.
- `HR-02` — Rows are written only through `ToolInstance`'s own transition methods, never constructed by an application service — so state and history cannot diverge.
- `HR-03` — Ordering is `ChangedAt` ascending, tie-broken by `CreationTime`/`Id`; the first row always has `Previous* == null`.
- `HR-04` — A row is emitted even when only one dimension moves; the unchanged dimension repeats its current value in both `Previous*` and `New*`.

---

## Entity: `ToolInstancePhoto`

`Entity<Guid>` inside the `ToolInstance` aggregate — table `catalog."ToolInstancePhotos"`.

| Property | Type | Constraints |
|---|---|---|
| `Id` | `Guid` | PK |
| `ToolInstanceId` | `Guid` | Required, **FK** → `catalog."ToolInstances"`, `ON DELETE CASCADE`, indexed |
| `BlobName` | `string` | Required, ≤ 256; key in the `ToolInstancePhotoContainer` blob container |
| `FileName` | `string` | Required, ≤ 256; original upload name, shown to users |
| `ContentType` | `string` | Required, ≤ 128; one of `CatalogPhotoOptions.AllowedContentTypes` |
| `SizeBytes` | `long` | > 0 and ≤ `CatalogPhotoOptions.MaxSizeBytes` |
| `DisplayOrder` | `int` | ≥ 0; controls gallery order |
| `IsPrimary` | `bool` | At most one `true` per instance |

**Rules**
- `PR-01` — Content type must be in the allowed list, else `Catalog:UnsupportedPhotoFormat` (FR-009).
- `PR-02` — Size must be ≤ `MaxSizeBytes` (default 5 MB), else `Catalog:PhotoTooLarge` (FR-009).
- `PR-03` — Binary content lives in ABP BlobStoring (FileSystem provider), never in the database; only `BlobName` is persisted.
- `PR-04` — Deleting a photo removes both the row and its blob; deleting/retiring never cascades to blobs for a *retired* instance, whose photos remain viewable (FR-006).

---

## Configuration: `CatalogPhotoOptions`

Bound from the `Catalog:Photos` configuration section (`ToolShare.Catalog.Application`).

| Option | Default | Purpose |
|---|---|---|
| `MaxSizeBytes` | `5242880` (5 MB) | `PR-02` |
| `AllowedContentTypes` | `["image/jpeg","image/png","image/webp"]` | `PR-01` |
| `MaxPerInstance` | `5` | `IR-08` |

---

## Domain services

### `CategoryManager` (`ToolShare.Catalog.Domain`)
- `CreateAsync(name, description)` — enforces `CR-01`–`CR-03`.
- `ChangeNameAsync(category, newName)` — re-checks `CR-03` excluding self.
- `DeleteAsync(category)` — enforces `CR-04`.

### `ToolInstanceManager` (`ToolShare.Catalog.Domain`)
- `CreateAsync(toolId, serialNumber, condition, notes)` — enforces `IR-01`–`IR-03`.
- `ChangeSerialNumberAsync(instance, newSerialNumber)` — re-checks `IR-01`/`IR-02` excluding self.

Transition behavior (`ChangeCondition`, `Retire`, `AddPhoto`, `RemovePhoto`) lives on the `ToolInstance` entity itself, not in the manager, because it needs no repository access — keeping it unit-testable without a database (Constitution V and the Technology & Architecture Constraints section).

### `CatalogTextNormalizer` (`ToolShare.Catalog.Domain.Shared`)
Pure static function used by `CR-02`, `TR-03`, `IR-02` and by the search query builder so that stored and queried values normalize identically:
trim → collapse inner whitespace → `FormD` → drop `NonSpacingMark` → invariant upper-case → `FormC`.

---

## Repositories (interfaces in `ToolShare.Catalog.Domain`)

| Interface | Beyond `IRepository<T, Guid>` |
|---|---|
| `ICategoryRepository` | `FindByNormalizedNameAsync`, `AnyToolAssignedAsync(categoryId)` |
| `IToolRepository` | `GetPagedListAsync(normalizedFilter, categoryId, onlyAvailable, includeRetiredInstances, sorting, skip, take)`, `GetCountAsync(...)`, `GetWithInstancesAsync(id)` |
| `IToolInstanceRepository` | `FindByNormalizedSerialNumberAsync`, `AnyBySerialNumberAsync(normalized, excludedId)`, `GetListByToolIdAsync(toolId, includeRetired)`, `GetWithDetailsAsync(id)` (photos + history) |

No repository exposes update or delete for `ToolInstanceStateChange` (`HR-01`). Implementations live in `ToolShare.Catalog.EntityFrameworkCore`.

---

## Seed data (`CatalogDataSeedContributor`, `ToolShare.Catalog.Domain`)

Idempotent (FR-016): each category is inserted only if no row with that `NormalizedName` exists.

| Name | Description |
|---|---|
| Power Tools | Electric and battery-powered tools |
| Hand Tools | Non-powered hand tools |
| Garden | Garden and yard equipment |
| Measuring | Measuring and levelling instruments |
| Ladders & Access | Ladders, steps and access equipment |

No tools or instances are seeded — the catalog starts empty so US1 is demonstrable end-to-end.

---

## Indexes and constraints summary

| Table | Index / constraint | Kind |
|---|---|---|
| `Categories` | `NormalizedName` | **unique** |
| `Tools` | `NormalizedName` | non-unique (search) |
| `Tools` | `CategoryId` | non-unique + FK `RESTRICT` |
| `ToolInstances` | `NormalizedSerialNumber` | **unique** (catalog-wide, SC-004) |
| `ToolInstances` | `ToolId` | non-unique + FK `RESTRICT` |
| `ToolInstances` | `(CirculationState, Condition)` | non-unique (availability filter) |
| `ToolInstanceStateChanges` | `(ToolInstanceId, ChangedAt)` | non-unique + FK `CASCADE` |
| `ToolInstancePhotos` | `ToolInstanceId` | non-unique + FK `CASCADE` |

All soft-deletable tables additionally carry ABP's `IsDeleted` filter. **No index or constraint crosses the `catalog` schema boundary.**
