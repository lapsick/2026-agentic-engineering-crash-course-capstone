using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace ToolShare.Lending.Reservations;

/// <summary>
/// A member's claim on a specific instance for a date range — its own
/// aggregate root because it has its own lifecycle and is the thing a
/// <see cref="Loans.Loan"/> realizes (data-model.md `RES-07`).
/// </summary>
public class Reservation : FullAuditedAggregateRoot<Guid>
{
    public virtual Guid MemberId { get; private set; }

    public virtual Guid ToolInstanceId { get; private set; }

    public virtual DateOnly StartDate { get; private set; }

    public virtual DateOnly EndDate { get; private set; }

    public virtual ReservationStatus Status { get; private set; }

    public virtual DateTime CreatedAt { get; private set; }

    public virtual DateTime? CancelledAt { get; private set; }

    public virtual string? CancellationReason { get; private set; }

    protected Reservation()
    {
    }

    /// <summary>
    /// Enforces RES-01: the range must not exceed <paramref name="maxLoanTermDays"/>
    /// and must not end before it starts. Availability (RES-02), overlap
    /// (RES-03), overdue-block (RES-04) and the concurrent-loan limit (RES-05)
    /// all need repository/other-module access and are enforced by
    /// <see cref="ReservationManager"/>, not here.
    /// </summary>
    public Reservation(Guid id, Guid memberId, Guid toolInstanceId, DateOnly startDate, DateOnly endDate, int maxLoanTermDays, DateTime createdAt)
        : base(id)
    {
        MemberId = memberId;
        ToolInstanceId = toolInstanceId;
        SetDateRange(startDate, endDate, maxLoanTermDays);
        Status = ReservationStatus.Active;
        CreatedAt = createdAt;
    }

    private void SetDateRange(DateOnly startDate, DateOnly endDate, int maxLoanTermDays)
    {
        if (endDate < startDate)
        {
            throw new BusinessException(LendingDomainErrorCodes.LoanTermExceeded);
        }

        var days = endDate.DayNumber - startDate.DayNumber;
        if (days > maxLoanTermDays)
        {
            throw new BusinessException(LendingDomainErrorCodes.LoanTermExceeded).WithData("maxLoanTermDays", maxLoanTermDays);
        }

        StartDate = startDate;
        EndDate = endDate;
    }

    /// <summary>Enforces RES-06: cancellable only while <see cref="ReservationStatus.Active"/>. Member-initiated.</summary>
    public void Cancel(DateTime at)
    {
        if (Status != ReservationStatus.Active)
        {
            throw new BusinessException(LendingDomainErrorCodes.ReservationNotCancellable);
        }

        Status = ReservationStatus.Cancelled;
        CancelledAt = at;
    }

    /// <summary>
    /// Enforces RES-07: called only by <see cref="Loans.Loan"/>'s own creation
    /// path (never directly by an application service — by convention, not
    /// compiler-enforced access, matching this codebase's existing style of
    /// not using <c>InternalsVisibleTo</c> for domain-test access), one-way —
    /// a <see cref="ReservationStatus.CheckedOut"/> reservation never reverts.
    /// </summary>
    public void RealizeAsCheckedOut()
    {
        if (Status != ReservationStatus.Active)
        {
            throw new BusinessException(LendingDomainErrorCodes.InvalidStateTransition);
        }

        Status = ReservationStatus.CheckedOut;
    }

    /// <summary>
    /// Enforces RES-08/MAINT-03 (research R7): system-initiated cancellation
    /// when a maintenance request opens for this instance while this
    /// reservation's range has not yet started. Never applied to a
    /// reservation whose range has already begun (that one, if any, is already
    /// <see cref="ReservationStatus.CheckedOut"/>, not <see cref="ReservationStatus.Active"/>).
    /// </summary>
    public void CancelForMaintenance(DateTime at, string reason)
    {
        if (Status != ReservationStatus.Active)
        {
            throw new BusinessException(LendingDomainErrorCodes.InvalidStateTransition);
        }

        if (StartDate <= DateOnly.FromDateTime(at))
        {
            throw new BusinessException(LendingDomainErrorCodes.InvalidStateTransition);
        }

        reason = Check.NotNullOrWhiteSpace(reason, nameof(reason)).Trim();
        Check.Length(reason, nameof(reason), LendingDomainSharedConsts.CancellationReasonMaxLength);

        Status = ReservationStatus.Cancelled;
        CancelledAt = at;
        CancellationReason = reason;
    }
}
