using System.Collections.Generic;
using System.Threading.Tasks;
using ToolShare.Lending.Reservations;
using Volo.Abp.Application.Services;

namespace ToolShare.Lending.Loans;

/// <summary>
/// Self-service (FR-030). Deliberately takes **no member id** on any method:
/// the caller's own reservations/loans/waitlist entries are resolved from
/// <c>CurrentUser.Id</c>, mirroring Membership's <c>IMyMembershipAppService</c>
/// self-only-by-construction design.
/// </summary>
public interface IMyLendingAppService : IApplicationService
{
    Task<List<ReservationDto>> GetMyReservationsAsync();

    Task<List<LoanDto>> GetMyLoansAsync();

    Task<List<WaitlistEntryDto>> GetMyWaitlistEntriesAsync();
}
