# Catalog Integration Events (Tier 1)

**Requirements**: FR-018 | **Verified by**: SC-007
**Channel**: `ILocalEventBus` (Constitution II) | **Assembly**: `ToolShare.Catalog.Domain.Shared`

> **Placement correction (made during implementation)**: originally specified as living in `Application.Contracts`, matching the DTOs. That is unreachable from `Domain` — standard ABP layering only lets `Domain` reference `Domain.Shared`, never `Application.Contracts` — and `Domain` is exactly where `ToolInstance.Retire`/`ChangeCondition`/registration must call `AddLocalEvent(...)`. The ETO therefore lives in `Domain.Shared`, alongside `ToolCondition`/`ToolInstanceCirculationState`, which is also where ABP's own modules conventionally put ETOs for this reason. It is still part of the Tier 1 public boundary — `Application.Contracts` re-exposes it transitively, exactly like the enums.

Catalog publishes exactly **one** event in this feature. It is the worked example that establishes the intermodule integration pattern for every later module.

---

## `ToolInstanceStateChangedEto`

```csharp
namespace ToolShare.Catalog.ToolInstances;

/// <summary>
/// Raised whenever a tool instance's condition and/or circulation state changes,
/// including its initial registration and its retirement.
/// </summary>
[Serializable]
public class ToolInstanceStateChangedEto
{
    public Guid ToolInstanceId { get; set; }
    public Guid ToolId { get; set; }
    public string SerialNumber { get; set; } = default!;

    public ToolCondition? PreviousCondition { get; set; }
    public ToolCondition NewCondition { get; set; }

    public ToolInstanceCirculationState? PreviousCirculationState { get; set; }
    public ToolInstanceCirculationState NewCirculationState { get; set; }

    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; }          // UTC
    public Guid? ChangedByUserId { get; set; }
}
```

### When it is raised

| Trigger | `Previous*` | `New*` | `Reason` |
|---|---|---|---|
| Instance registered | both `null` | the chosen condition, `InCirculation` | `null` |
| Condition changed | previous condition, current circulation state | new condition, unchanged circulation state | optional note |
| Instance retired | current condition, `InCirculation` | unchanged condition, `Retired` | **required** retirement reason |

One domain transition produces exactly one event and exactly one `ToolInstanceStateChange` history row — the two are written together (see `HR-02`, `IR-07` in [../data-model.md](../data-model.md)).

### Publication semantics

The event is added on the aggregate root, not published imperatively:

```csharp
// inside ToolInstance.Retire(...)
AddLocalEvent(new ToolInstanceStateChangedEto { /* … */ });
```

This gives three guarantees consumers may rely on:

1. **Transactional** — ABP's unit of work dispatches added local events only after `SaveChanges` succeeds. A handler never observes a change that was rolled back.
2. **In-process and synchronous** — handlers run in the same process and the same request scope. A handler that throws will surface to the caller; handlers must therefore be fast and must not perform long-running work.
3. **At-most-once per transition** — no retry or outbox. Work that must survive a crash belongs in a durable mechanism (ABP Background Jobs), not directly in a local handler.

`ChangedAt` is the UTC timestamp taken from ABP's `IClock` at the moment of the domain transition — not the dispatch time.

### Subscription

```csharp
public class ToolInstanceStateChangedHandler
    : ILocalEventHandler<ToolInstanceStateChangedEto>, ITransientDependency
{
    public async Task HandleEventAsync(ToolInstanceStateChangedEto eventData)
    {
        // React using identifiers only — never load Catalog entities directly.
        if (eventData.NewCirculationState == ToolInstanceCirculationState.Retired)
        {
            await _loanRepository.CancelOpenReservationsAsync(eventData.ToolInstanceId);
        }
    }
}
```

Subscribing requires a reference to `ToolShare.Catalog.Application.Contracts` and nothing else. Handlers are discovered by ABP's conventional registration; no wiring in the Catalog module is needed, so **Catalog never learns who its consumers are** — the property that keeps FR-019 true.

### Compatibility rules

- Additive changes only: new **optional** properties may be added; existing ones are never renamed, removed or retyped.
- New `ToolInstanceCirculationState` values (`OnLoan`, `UnderMaintenance` in 003+) will flow through this same event at new numeric values. Handlers must use a `default` arm rather than assuming the current two-value set.
- The event carries **identifiers and values, never entity references** — required by Constitution III, since consumers live in other schemas.

### Test hook for SC-007

`ToolShare.Catalog.Application.Tests` registers a probe handler in its ABP test module, registers an instance and then retires it, and asserts two events were received with the expected `Previous*`/`New*` pairs — importing only `ToolShare.Catalog.ToolInstances` from the contracts assembly.
