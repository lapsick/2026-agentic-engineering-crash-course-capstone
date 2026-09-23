using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ToolShare.Lending.Reservations;

/// <summary>
/// Reservations and the waitlist (US1). Creating/cancelling one's own
/// reservation and joining a waitlist carry no permission requirement beyond
/// the enrolment gate (contracts/lending-permissions.md).
/// </summary>
public interface IReservationAppService : IApplicationService
{
    Task<ReservationDto> CreateAsync(CreateReservationDto input);

    Task CancelAsync(Guid id);

    Task<WaitlistEntryDto> JoinWaitlistAsync(JoinWaitlistDto input);

    Task<PagedResultDto<ReservationDto>> GetListForInstanceAsync(Guid toolInstanceId);
}

public class CreateReservationDto
{
    [Required]
    public Guid ToolInstanceId { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly EndDate { get; set; }
}

public class JoinWaitlistDto
{
    [Required]
    public Guid ToolInstanceId { get; set; }
}

public class ReservationDto : EntityDto<Guid>
{
    public Guid MemberId { get; set; }

    public Guid ToolInstanceId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public ReservationStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }
}

public class WaitlistEntryDto : EntityDto<Guid>
{
    public Guid MemberId { get; set; }

    public Guid ToolInstanceId { get; set; }

    public DateTime JoinedAt { get; set; }

    public WaitlistOfferState OfferState { get; set; }

    public DateTime? OfferExpiresAt { get; set; }
}
