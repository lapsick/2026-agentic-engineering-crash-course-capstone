using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ToolShare.Lending.Reservations;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;

namespace ToolShare.Lending.Loans;

/// <summary>
/// Self-service (FR-030). Resolves the caller's own reservations/loans/
/// waitlist entries from <c>CurrentUser.Id</c> — never a parameter — mirroring
/// Membership's <c>IMyMembershipAppService</c>.
/// </summary>
[Authorize]
public class MyLendingAppService : ApplicationService, IMyLendingAppService
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IWaitlistEntryRepository _waitlistEntryRepository;
    private readonly ILoanRepository _loanRepository;
    private readonly Membership.Members.IMemberStandingAppService _memberStandingAppService;

    public MyLendingAppService(
        IReservationRepository reservationRepository,
        IWaitlistEntryRepository waitlistEntryRepository,
        ILoanRepository loanRepository,
        Membership.Members.IMemberStandingAppService memberStandingAppService)
    {
        _reservationRepository = reservationRepository;
        _waitlistEntryRepository = waitlistEntryRepository;
        _loanRepository = loanRepository;
        _memberStandingAppService = memberStandingAppService;
    }

    public virtual async Task<List<ReservationDto>> GetMyReservationsAsync()
    {
        var memberId = await GetOwnMemberIdOrThrowAsync();

        var queryable = await _reservationRepository.GetQueryableAsync();
        var reservations = await AsyncExecuter.ToListAsync(queryable.Where(r => r.MemberId == memberId));
        return reservations.Select(MapToDto).ToList();
    }

    public virtual async Task<List<WaitlistEntryDto>> GetMyWaitlistEntriesAsync()
    {
        var memberId = await GetOwnMemberIdOrThrowAsync();

        var queryable = await _waitlistEntryRepository.GetQueryableAsync();
        var entries = await AsyncExecuter.ToListAsync(queryable.Where(w => w.MemberId == memberId));
        return entries.Select(MapToDto).ToList();
    }

    public virtual async Task<List<LoanDto>> GetMyLoansAsync()
    {
        var memberId = await GetOwnMemberIdOrThrowAsync();

        var queryable = await _loanRepository.GetQueryableAsync();
        var loans = await AsyncExecuter.ToListAsync(queryable.Where(l => l.MemberId == memberId));
        return loans.Select(MapToDto).ToList();
    }

    private async Task<System.Guid> GetOwnMemberIdOrThrowAsync()
    {
        var identityUserId = CurrentUser.Id
            ?? throw new BusinessException(LendingDomainErrorCodes.InvalidStateTransition);

        var standing = await _memberStandingAppService.GetByIdentityUserIdAsync(identityUserId);
        return standing.IsEnrolled
            ? standing.MemberId!.Value
            : throw new AbpAuthorizationException();
    }

    private static ReservationDto MapToDto(Reservation reservation) => new()
    {
        Id = reservation.Id,
        MemberId = reservation.MemberId,
        ToolInstanceId = reservation.ToolInstanceId,
        StartDate = reservation.StartDate,
        EndDate = reservation.EndDate,
        Status = reservation.Status,
        CreatedAt = reservation.CreatedAt,
        CancelledAt = reservation.CancelledAt,
        CancellationReason = reservation.CancellationReason
    };

    private static WaitlistEntryDto MapToDto(WaitlistEntry entry) => new()
    {
        Id = entry.Id,
        MemberId = entry.MemberId,
        ToolInstanceId = entry.ToolInstanceId,
        JoinedAt = entry.JoinedAt,
        OfferState = entry.OfferState,
        OfferExpiresAt = entry.OfferExpiresAt
    };

    private static LoanDto MapToDto(Loan loan) => new()
    {
        Id = loan.Id,
        ReservationId = loan.ReservationId,
        MemberId = loan.MemberId,
        ToolInstanceId = loan.ToolInstanceId,
        CheckedOutAt = loan.CheckedOutAt,
        PlannedReturnDate = loan.PlannedReturnDate,
        ConditionAtCheckout = loan.ConditionAtCheckout,
        ReturnedAt = loan.ReturnedAt,
        ReturnedCondition = loan.ReturnedCondition,
        IsOverdue = loan.IsOverdue
    };
}
