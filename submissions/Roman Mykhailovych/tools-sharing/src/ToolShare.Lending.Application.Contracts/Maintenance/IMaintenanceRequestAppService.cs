using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using ToolShare.Catalog;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ToolShare.Lending.Maintenance;

/// <summary>
/// Closing an open maintenance request (US3) requires <c>Lending.Maintenance.Close</c>;
/// reporting one out-of-band (008) requires <c>Lending.Maintenance.Report</c>
/// (contracts/lending-permissions.md, 008 contracts/lending-maintenance.md); the
/// roster view requires <c>Lending.Loans</c> (maintenance visibility is part of
/// "seeing what's going on with the fleet," not a separate grant).
/// </summary>
public interface IMaintenanceRequestAppService : IApplicationService
{
    Task<MaintenanceRequestDto> CloseAsync(Guid id, CloseMaintenanceRequestDto input);

    Task<PagedResultDto<MaintenanceRequestDto>> GetOpenListAsync();

    /// <summary>
    /// 008: sends an instance that is in circulation and not on loan to
    /// maintenance, cancelling its not-yet-collected reservations and
    /// withdrawing any outstanding waitlist offer. Refused, with nothing
    /// written, when the instance is retired, on loan, already has an open
    /// request, or the observed condition is better than its current one.
    /// </summary>
    Task<MaintenanceRequestDto> ReportAsync(ReportMaintenanceDto input);
}

public class CloseMaintenanceRequestDto
{
    [Required]
    [Range(0, double.MaxValue)]
    public decimal Cost { get; set; }
}

/// <summary>008 FR-001–FR-003.</summary>
public class ReportMaintenanceDto
{
    [Required]
    public Guid ToolInstanceId { get; set; }

    [Required]
    public ToolCondition ObservedCondition { get; set; }

    [Required]
    [StringLength(LendingDomainSharedConsts.MaintenanceReportReasonMaxLength)]
    public string Reason { get; set; } = default!;
}

public class MaintenanceRequestDto : EntityDto<Guid>
{
    public Guid ToolInstanceId { get; set; }

    /// <summary>008 FR-015.</summary>
    public MaintenanceRequestOrigin Origin { get; set; }

    /// <summary>Set for <see cref="MaintenanceRequestOrigin.ReturnTriggered"/>; null for out-of-band requests.</summary>
    public Guid? TriggeringLoanId { get; set; }

    /// <summary>Out-of-band only: the reporting Librarian's member id.</summary>
    public Guid? ReportedByMemberId { get; set; }

    /// <summary>Out-of-band only.</summary>
    public string? ReportReason { get; set; }

    /// <summary>Out-of-band only.</summary>
    public ToolCondition? ObservedCondition { get; set; }

    public MaintenanceRequestStatus Status { get; set; }

    /// <summary>For an out-of-band request, also the moment of the report.</summary>
    public DateTime OpenedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public decimal? Cost { get; set; }
}
