# Contract: `IReportAppService`

**Feature**: `006-librarian-reports` | **Tier**: 2 (module-internal) | **Date**: 2026-08-05

**Project**: `ToolShare.Lending.Application.Contracts` · **Namespace**: `ToolShare.Lending.Reports`

Consumed only by `ToolShare.Lending.Blazor`. Guarded in full by
`[Authorize(LendingPermissions.Reports.Default)]` — see
[lending-reports-permissions.md](./lending-reports-permissions.md).

---

## Interface

```csharp
namespace ToolShare.Lending.Reports;

/// <summary>
/// The three librarian reports (product spec US7). Every method is read-only:
/// no call here creates, modifies, or deletes a record (FR-011). Requires
/// <c>Lending.Reports</c> (contracts/lending-reports-permissions.md).
/// </summary>
public interface IReportAppService : IApplicationService
{
    /// <summary>FR-004–FR-006. Loans unreturned past their planned return date, most overdue first.</summary>
    Task<ListResultDto<OverdueLoanReportItemDto>> GetOverdueLoansAsync();

    /// <summary>FR-001–FR-003. Tools ranked by loan count; range optional (omitted = all time).</summary>
    Task<ListResultDto<ToolPopularityReportItemDto>> GetToolPopularityAsync(ToolPopularityReportInput input);

    /// <summary>FR-007, FR-008. Total cost of maintenance requests closed within the required range.</summary>
    Task<MaintenanceCostReportDto> GetMaintenanceCostAsync(MaintenanceCostReportInput input);
}
```

`ListResultDto<T>` rather than `PagedResultDto<T>`: these are aggregate views a librarian scans whole,
bounded by fleet size and by the community's open-loan count (both low hundreds), not feeds to page
through. SC-001's budget is comfortably met without paging, and adding it later is additive.

---

## Inputs

```csharp
public class ToolPopularityReportInput
{
    /// <summary>Inclusive lower bound on Loan.CheckedOutAt. Null = unbounded.</summary>
    public DateOnly? From { get; set; }

    /// <summary>Inclusive upper bound on Loan.CheckedOutAt. Null = unbounded.</summary>
    public DateOnly? To { get; set; }
}

public class MaintenanceCostReportInput
{
    /// <summary>
    /// Inclusive lower bound on MaintenanceRequest.ClosedAt. Required — but
    /// enforced by the guard, NOT by [Required]; see the note below.
    /// </summary>
    public DateOnly? From { get; set; }

    /// <summary>Inclusive upper bound on MaintenanceRequest.ClosedAt. Required, same enforcement.</summary>
    public DateOnly? To { get; set; }
}
```

> **Why these are nullable even though both bounds are mandatory.** `DateOnly` is a non-nullable value
> type, and `RequiredAttribute` rejects only `null` — so `[Required] DateOnly From` **always passes
> validation**. An omitted bound would bind to `default(DateOnly)` (`0001-01-01`) and silently produce a
> near-all-time total instead of a rejected request: a wrong answer presented as a right one, which is
> exactly what FR-009 exists to prevent. Making the properties nullable lets "not supplied" be
> representable, and the guard in `ReportAppService` rejects it explicitly. The popularity input is
> nullable for the opposite reason — there, omission is *meaningful* (all-time, FR-002).

`GetOverdueLoansAsync` takes no input by design: "currently overdue" is evaluated against
`Clock.Now`, and there is no meaningful parameter to vary (FR-005).

### Date-range rules (FR-009, both inputs)

| Rule | Behavior |
|---|---|
| `From > To` | `BusinessException(LendingDomainErrorCodes.InvalidReportDateRange)` — never an empty result |
| Both bounds inclusive | Against `DateTime` columns: `>= From && < To.AddDays(1)` |
| Popularity, both null | All-time (FR-002) |
| Popularity, one null | Open-ended on that side; the `From <= To` check applies only when both are present |
| Maintenance, either null | `BusinessException(LendingDomainErrorCodes.InvalidReportDateRange)`, thrown by the guard — the report is defined over a period, so an open-ended side is not a valid request |

The guard therefore does three things, in order: reject a null bound where the report requires one,
reject `From > To` when both are present, then hand the (possibly open-ended, for popularity) range to
the query.

---

## Outputs

```csharp
public class OverdueLoanReportItemDto
{
    public Guid LoanId { get; set; }
    public Guid MemberId { get; set; }
    public string? MemberDisplayName { get; set; }
    public Guid ToolInstanceId { get; set; }
    public string? ToolName { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime CheckedOutAt { get; set; }
    public DateOnly PlannedReturnDate { get; set; }
    public int DaysOverdue { get; set; }
}

public class ToolPopularityReportItemDto
{
    public Guid ToolId { get; set; }
    public string? ToolName { get; set; }
    public int LoanCount { get; set; }
}

public class MaintenanceCostReportDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public decimal TotalCost { get; set; }
    public int ClosedRequestCount { get; set; }
}
```

---

## Behavioral guarantees

These are the contract, not implementation notes — the integration tests assert each one.

| # | Guarantee | Requirement |
|---|---|---|
| B1 | No method mutates any record | FR-011 |
| B2 | Overdue membership is computed live from `Clock.Now`, never from the stored `Loan.IsOverdue` flag | FR-005, research R2 |
| B3 | A returned loan never appears in the overdue report, however late the return was | FR-005 |
| B4 | Overdue results are ordered by `DaysOverdue` descending | FR-006 |
| B5 | An empty result is a successful empty list / a `TotalCost` of `0m` — never an error, never null | FR-013, SC-003 |
| B6 | Loans of retired instances still count toward popularity | FR-003, SC-004 |
| B7 | Only `Closed` maintenance requests contribute cost; open ones contribute nothing | FR-008, SC-003 |
| B8 | `From > To` is rejected with `InvalidReportDateRange` on both range-taking methods | FR-009 |
| B9 | An unresolvable member or tool leaves the name `null` but never drops the row | spec Edge Cases |
| B10 | Each report is produced by a single call — no client-side consolidation | FR-010, SC-001 |

**On B9**: names are resolved through Catalog's and Membership's batch lookups, both of which omit
unknown ids rather than throwing (their published contracts say so explicitly). The report treats an
omission as `null` and renders the raw id, so a deactivated member's overdue loan and a loan against an
unresolvable instance both remain visible — which is what the spec's Edge Cases require.

---

## Cross-module calls made by the implementation

| Call | Module | When |
|---|---|---|
| `IToolInstanceLookupAppService.GetByIdsAsync(instanceIds)` | Catalog | Overdue report (names); popularity report (instance → tool fold, research R4) |
| `IMemberStandingAppService.GetByIdsAsync(memberIds)` | Membership | Overdue report (display names) |

Both are batch calls made once per report — never per row. The maintenance cost report makes no
cross-module call at all: it reads only Lending's own data.
