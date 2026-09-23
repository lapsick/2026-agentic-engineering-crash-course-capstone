using System;
using System.Linq;
using System.Threading.Tasks;
using ToolShare.Lending.Loans;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ToolShare.Lending.Reservations;

/// <summary>
/// The rules that need repository access and therefore cannot live on
/// <see cref="Reservation"/> itself: RES-02 (availability), RES-03 (overlap
/// pre-check), RES-04 (overdue block), RES-05 (concurrent-loan limit), and the
/// RES-08/MAINT-03 cancellation cascade (research R7). Cross-module facts
/// (Catalog availability, Membership's effective concurrent-loan limit) are
/// supplied by the caller as plain values — this domain service never calls
/// another module directly.
/// </summary>
public class ReservationManager : DomainService
{
    private readonly IReservationRepository _reservationRepository;
    private readonly ILoanRepository _loanRepository;

    public ReservationManager(IReservationRepository reservationRepository, ILoanRepository loanRepository)
    {
        _reservationRepository = reservationRepository;
        _loanRepository = loanRepository;
    }

    /// <summary>Enforces RES-02, RES-03, RES-04, RES-05 before delegating to <see cref="Reservation"/>'s own RES-01 guard.</summary>
    public async Task<Reservation> CreateAsync(
        Guid memberId,
        Guid toolInstanceId,
        DateOnly startDate,
        DateOnly endDate,
        int maxLoanTermDays,
        int effectiveConcurrentLoanLimit,
        bool isInstanceAvailable,
        DateTime at)
    {
        if (!isInstanceAvailable)
        {
            throw new BusinessException(LendingDomainErrorCodes.InstanceUnavailable);
        }

        var openLoans = await _loanRepository.GetOpenForMemberAsync(memberId);
        if (openLoans.Any(l => l.IsOverdue))
        {
            throw new BusinessException(LendingDomainErrorCodes.MemberHasOverdueLoan);
        }

        var activeReservationCount = await _reservationRepository.GetActiveCountForMemberAsync(memberId);
        if (activeReservationCount + openLoans.Count >= effectiveConcurrentLoanLimit)
        {
            throw new BusinessException(LendingDomainErrorCodes.ConcurrentLoanLimitReached).WithData("limit", effectiveConcurrentLoanLimit);
        }

        if (await _reservationRepository.HasOverlapAsync(toolInstanceId, startDate, endDate))
        {
            throw new BusinessException(LendingDomainErrorCodes.InstanceAlreadyReservedForRange);
        }

        return new Reservation(GuidGenerator.Create(), memberId, toolInstanceId, startDate, endDate, maxLoanTermDays, at);
    }

    /// <summary>
    /// Enforces RES-08/MAINT-03 (research R7): cancels every not-yet-started
    /// Active reservation for the instance a maintenance request just opened
    /// against — the already-realized (CheckedOut) reservation that triggered
    /// the return is untouched, since it is no longer Active.
    /// </summary>
    public async Task CancelForMaintenanceAsync(Guid toolInstanceId, DateTime at, string reason)
    {
        var reservations = await _reservationRepository.GetActiveForInstanceAsync(toolInstanceId);
        var notYetStarted = reservations.Where(r => r.StartDate > DateOnly.FromDateTime(at));

        foreach (var reservation in notYetStarted)
        {
            reservation.CancelForMaintenance(at, reason);
            await _reservationRepository.UpdateAsync(reservation);
        }
    }
}
