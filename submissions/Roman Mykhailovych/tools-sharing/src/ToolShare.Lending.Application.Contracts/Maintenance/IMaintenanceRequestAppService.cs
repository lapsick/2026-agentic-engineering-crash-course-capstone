using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ToolShare.Lending.Maintenance;

/// <summary>
/// Closing an open maintenance request (US3). Requires <c>Lending.Maintenance.Close</c>
/// (contracts/lending-permissions.md); the roster view requires <c>Lending.Loans</c>
/// (maintenance visibility is part of "seeing what's going on with the fleet," not a
/// separate grant).
/// </summary>
public interface IMaintenanceRequestAppService : IApplicationService
{
    Task<MaintenanceRequestDto> CloseAsync(Guid id, CloseMaintenanceRequestDto input);

    Task<PagedResultDto<MaintenanceRequestDto>> GetOpenListAsync();
}

public class CloseMaintenanceRequestDto
{
    [Required]
    [Range(0, double.MaxValue)]
    public decimal Cost { get; set; }
}

public class MaintenanceRequestDto : EntityDto<Guid>
{
    public Guid ToolInstanceId { get; set; }

    public Guid TriggeringLoanId { get; set; }

    public MaintenanceRequestStatus Status { get; set; }

    public DateTime OpenedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public decimal? Cost { get; set; }
}
