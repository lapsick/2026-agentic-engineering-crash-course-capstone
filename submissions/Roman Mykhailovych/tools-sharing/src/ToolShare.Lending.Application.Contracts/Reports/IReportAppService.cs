using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ToolShare.Lending.Reports;

/// <summary>
/// The three librarian reports (product spec US7). Every method is read-only:
/// no call here creates, modifies, or deletes a record (FR-011). Requires
/// <c>Lending.Reports</c> (contracts/lending-reports-permissions.md).
/// Tier 2 — consumed only by <c>ToolShare.Lending.Blazor</c>.
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

public class ToolPopularityReportInput
{
    /// <summary>Inclusive lower bound on <c>Loan.CheckedOutAt</c>. Null = unbounded (FR-002).</summary>
    public DateOnly? From { get; set; }

    /// <summary>Inclusive upper bound on <c>Loan.CheckedOutAt</c>. Null = unbounded (FR-002).</summary>
    public DateOnly? To { get; set; }
}

/// <summary>
/// Both bounds are mandatory, yet both are nullable — deliberately.
/// <see cref="DateOnly"/> is a non-nullable value type, so <c>[Required]</c>
/// can never fail on it and an omitted bound would bind to
/// <c>default(DateOnly)</c> (<c>0001-01-01</c>), silently producing a
/// near-all-time total presented as a valid period result. Nullability makes
/// "not supplied" representable so the guard in <c>ReportAppService</c> can
/// reject it explicitly (FR-009).
/// </summary>
public class MaintenanceCostReportInput
{
    /// <summary>Inclusive lower bound on <c>MaintenanceRequest.ClosedAt</c>. Required — enforced by the guard, not by <c>[Required]</c>.</summary>
    public DateOnly? From { get; set; }

    /// <summary>Inclusive upper bound on <c>MaintenanceRequest.ClosedAt</c>. Required, same enforcement.</summary>
    public DateOnly? To { get; set; }
}

/// <summary>
/// A currently-overdue loan (FR-004, FR-006). A <c>null</c> name means the
/// referenced record could not be resolved through its owning module's lookup;
/// the row is still returned, with the raw id, rather than dropped (B9).
/// </summary>
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

/// <summary>One tool and its loan count for the selected range (FR-001–FR-003). Tools with no loans in range are absent (research R5).</summary>
public class ToolPopularityReportItemDto
{
    public Guid ToolId { get; set; }

    public string? ToolName { get; set; }

    public int LoanCount { get; set; }
}

/// <summary>
/// One question about one period, so a single object rather than a list
/// (FR-007, FR-013). <see cref="ClosedRequestCount"/> makes "zero because
/// nothing closed" legible next to "zero because everything cost zero".
/// </summary>
public class MaintenanceCostReportDto
{
    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    public decimal TotalCost { get; set; }

    public int ClosedRequestCount { get; set; }
}
