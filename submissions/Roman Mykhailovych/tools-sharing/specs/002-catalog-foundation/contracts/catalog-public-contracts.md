# Catalog Public Contracts (Tier 1)

**Requirements**: FR-017, FR-019 | **Verified by**: SC-007

Assembly: `ToolShare.Catalog.Application.Contracts`
Namespace: `ToolShare.Catalog.ToolInstances`
Enums from: `ToolShare.Catalog` (`ToolShare.Catalog.Domain.Shared`)

This is the **entire** surface a downstream module (Lending, Maintenance, Notifications — features 003+) may take a compile-time dependency on. Everything else in Catalog is internal.

---

## `IToolInstanceLookupAppService`

```csharp
namespace ToolShare.Catalog.ToolInstances;

/// <summary>
/// Read-only lookup of tool instances for other modules.
/// Consumers must depend on this interface and its DTOs only —
/// never on Catalog's Domain or EntityFrameworkCore layer.
/// </summary>
public interface IToolInstanceLookupAppService : IApplicationService
{
    Task<ToolInstanceLookupDto?> FindAsync(Guid id);

    Task<List<ToolInstanceLookupDto>> GetByIdsAsync(IEnumerable<Guid> ids);

    Task<PagedResultDto<ToolInstanceLookupDto>> GetListAsync(ToolInstanceLookupFilterDto input);

    Task<bool> IsAvailableAsync(Guid id);
}
```

| Operation | Behavior | Authorization |
|---|---|---|
| `FindAsync(id)` | Returns the instance, or `null` when the id is unknown or soft-deleted. **Retired instances are returned** — a downstream module must be able to resolve a historical reference. | Authenticated |
| `GetByIdsAsync(ids)` | Batch form of `FindAsync`. Unknown ids are omitted (no exception, no `null` entries), so result count may be smaller than input count. Order is unspecified. Empty input → empty list. | Authenticated |
| `GetListAsync(input)` | Paged, filtered query. Default `MaxResultCount` 10, hard cap 100. | Authenticated |
| `IsAvailableAsync(id)` | `true` iff the instance exists and `IsAvailable` holds (see below). An unknown id returns `false` rather than throwing. | Authenticated |

**Contract guarantees**
- No method throws for "not found"; absence is expressed as `null`, an omitted element, or `false`.
- No method mutates state; all are safe to call from a read-only unit of work.
- Implemented by `ToolInstanceLookupAppService` in `ToolShare.Catalog.Application` — consumers resolve the **interface** from DI and never name the implementation.

---

## `ToolInstanceLookupDto`

```csharp
public class ToolInstanceLookupDto : EntityDto<Guid>
{
    public Guid ToolId { get; set; }
    public string ToolName { get; set; } = default!;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = default!;
    public string SerialNumber { get; set; } = default!;
    public ToolCondition Condition { get; set; }
    public ToolInstanceCirculationState CirculationState { get; set; }
    public bool IsAvailable { get; set; }
}
```

`ToolName` and `CategoryName` are denormalized into the DTO deliberately: they save every consumer a second round-trip merely to render a label, and they carry no coupling because they are values, not references.

### `IsAvailable` semantics

```text
IsAvailable  ⇔  CirculationState == InCirculation  &&  Condition != Damaged
```

This is **Catalog's** notion of availability: the instance is physically in the pool and serviceable. It deliberately says nothing about loans or maintenance bookings, which Catalog does not know about.

> **Forward compatibility note for Lending (003+).** Lending must compute its own borrowability as
> `catalogInstance.IsAvailable && !lending.HasActiveLoan(id)`. Catalog's meaning of `IsAvailable`
> will not change when Lending ships — that is precisely what makes this contract stable (FR-019).

---

## `ToolInstanceLookupFilterDto`

```csharp
public class ToolInstanceLookupFilterDto : PagedAndSortedResultRequestDto
{
    public Guid? ToolId { get; set; }
    public Guid? CategoryId { get; set; }
    public string? SerialNumber { get; set; }
    public ToolCondition? Condition { get; set; }
    public bool? OnlyAvailable { get; set; }
    public bool IncludeRetired { get; set; } = false;
}
```

| Field | Effect when set |
|---|---|
| `ToolId` | Only instances of that tool |
| `CategoryId` | Only instances whose tool belongs to that category |
| `SerialNumber` | Case-insensitive, accent-tolerant **contains** match on the normalized serial number |
| `Condition` | Exact condition match |
| `OnlyAvailable = true` | Only instances where `IsAvailable` |
| `IncludeRetired = false` (default) | Retired instances are excluded (spec FR-006) |

`Sorting` accepts `SerialNumber`, `Condition`, `CirculationState`, `CreationTime`; default `SerialNumber ASC`. An unrecognized sort field falls back to the default rather than throwing.

---

## Shared enums

Defined in `ToolShare.Catalog.Domain.Shared`, reachable by consumers transitively through `Catalog.Application.Contracts`. See [../data-model.md](../data-model.md) for the authoritative table.

```csharp
namespace ToolShare.Catalog;

public enum ToolCondition { New = 0, Good = 1, Worn = 2, Damaged = 3 }

public enum ToolInstanceCirculationState { InCirculation = 0, Retired = 1, OnLoan = 2, UnderMaintenance = 3 }
```

`ToolCondition` is frozen by the product spec. **`OnLoan` and `UnderMaintenance` were added by 004 (Lending)** — additive, at new numeric values, exactly as anticipated here; every existing consumer keeps working unchanged. Consumers must still treat unknown enum values defensively (`switch` with a `default` arm) rather than assuming exhaustiveness, since further values may be appended later.

### The inbound counterpart (added by 004)

`IToolInstanceLookupAppService` above is read-only. Feature 004 added a second Tier 1 interface,
`IToolInstanceCirculationReportingAppService`, through which Lending reports that an instance is now
on loan, returned, under maintenance, or repaired — the four operations that actually produce the two
new enum values above. See
[specs/004-lending/contracts/catalog-extension.md](../../004-lending/contracts/catalog-extension.md)
for its full signature and behavior table. Catalog itself remains unaware of what a loan or a
reservation is; it only accepts these as reported facts.

---

## Consumption example (illustrative — the future Lending module)

```csharp
public class LoanAppService : ApplicationService
{
    private readonly IToolInstanceLookupAppService _toolInstances; // Catalog.Application.Contracts

    public async Task RequestAsync(Guid toolInstanceId)
    {
        var instance = await _toolInstances.FindAsync(toolInstanceId)
            ?? throw new BusinessException("Lending:UnknownToolInstance");

        if (!instance.IsAvailable)
        {
            throw new BusinessException("Lending:ToolInstanceNotAvailable");
        }

        // Store only the identifier — no FK across schemas (Constitution III).
        await _loanRepository.InsertAsync(new Loan(GuidGenerator.Create(), toolInstanceId, CurrentUser.GetId()));
    }
}
```

Note what is absent: no `ToolInstance` entity, no `CatalogDbContext`, no repository, no cross-schema foreign key.
