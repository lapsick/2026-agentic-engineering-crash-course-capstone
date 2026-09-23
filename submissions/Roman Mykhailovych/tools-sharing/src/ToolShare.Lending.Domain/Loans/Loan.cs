using System;
using ToolShare.Catalog;
using ToolShare.Lending.Reservations;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace ToolShare.Lending.Loans;

/// <summary>
/// An instance checked out to a member (data-model.md `LOAN-01`–`LOAN-06`).
/// <see cref="Id"/> doubles as the occurrence identifier this feature reports
/// to Membership (research R5) — no separate identifier concept is invented.
/// </summary>
public class Loan : FullAuditedAggregateRoot<Guid>
{
    public virtual Guid ReservationId { get; private set; }

    public virtual Guid MemberId { get; private set; }

    public virtual Guid ToolInstanceId { get; private set; }

    public virtual DateTime CheckedOutAt { get; private set; }

    public virtual DateOnly PlannedReturnDate { get; private set; }

    /// <summary>Catalog's own condition scale — read once at checkout, stored as a plain value, no FK (Constitution III).</summary>
    public virtual ToolCondition ConditionAtCheckout { get; private set; }

    public virtual DateTime? ReturnedAt { get; private set; }

    public virtual ToolCondition? ReturnedCondition { get; private set; }

    public virtual bool IsOverdue { get; private set; }

    public virtual DateTime? ReminderSentAt { get; private set; }

    public virtual DateTime? OverdueNoticeSentAt { get; private set; }

    public virtual DateTime? ReliabilityReportedAt { get; private set; }

    protected Loan()
    {
    }

    /// <summary>
    /// Enforces LOAN-01/LOAN-02: requires an <see cref="ReservationStatus.Active"/>
    /// reservation and realizes it (<see cref="Reservation.RealizeAsCheckedOut"/>)
    /// in the same operation, so a loan can never exist without exactly one
    /// originating, now-<see cref="ReservationStatus.CheckedOut"/> reservation.
    /// Availability re-check (LOAN-06) needs Catalog's own lookup and is
    /// enforced by <see cref="LoanManager"/>/the application layer, not here.
    /// </summary>
    public Loan(Guid id, Reservation reservation, DateTime checkedOutAt, ToolCondition conditionAtCheckout)
        : base(id)
    {
        if (reservation.Status != ReservationStatus.Active)
        {
            throw new BusinessException(LendingDomainErrorCodes.NoMatchingActiveReservation);
        }

        ReservationId = reservation.Id;
        MemberId = reservation.MemberId;
        ToolInstanceId = reservation.ToolInstanceId;
        CheckedOutAt = checkedOutAt;
        PlannedReturnDate = reservation.EndDate;
        ConditionAtCheckout = conditionAtCheckout;

        reservation.RealizeAsCheckedOut();
    }

    /// <summary>
    /// Enforces LOAN-03: permitted only while open. Sets <see cref="IsOverdue"/>
    /// if not already set and the return is late. Whether this return is
    /// worsened (LOAN-04) is read afterward via <see cref="IsWorsened"/> — the
    /// caller (research: <see cref="LoanManager"/>) decides what that implies
    /// (opening a maintenance request, which Catalog operation to call).
    /// </summary>
    public void Return(DateTime returnedAt, ToolCondition returnedCondition)
    {
        if (ReturnedAt.HasValue)
        {
            throw new BusinessException(LendingDomainErrorCodes.InvalidStateTransition);
        }

        ReturnedAt = returnedAt;
        ReturnedCondition = returnedCondition;

        if (!IsOverdue && DateOnly.FromDateTime(returnedAt) > PlannedReturnDate)
        {
            IsOverdue = true;
        }
    }

    /// <summary>LOAN-04: a return is worsened iff the returned condition is a lower-quality value than at checkout on Catalog's fixed 4-level scale.</summary>
    public bool IsWorsened()
    {
        return ReturnedCondition.HasValue && ReturnedCondition.Value > ConditionAtCheckout;
    }

    /// <summary>
    /// 006-librarian-reports: is this loan overdue at <paramref name="asOf"/>?
    /// Pure — it reads the dates, never the stored <see cref="IsOverdue"/> flag,
    /// which lags reality by up to <c>OverdueMarkingWorker</c>'s sweep period
    /// (research R2). A loan is <b>not</b> overdue on its planned return date and
    /// becomes overdue the following day, matching <see cref="Return"/>'s
    /// comparison. A returned loan is never overdue however late the return was:
    /// this answers "who is holding something they should have brought back",
    /// not "which returns were late".
    /// </summary>
    public bool IsOverdueAsOf(DateOnly asOf)
    {
        return ReturnedAt is null && PlannedReturnDate < asOf;
    }

    /// <summary>Whole days past <see cref="PlannedReturnDate"/> at <paramref name="asOf"/>, or <c>0</c> when <see cref="IsOverdueAsOf"/> does not hold.</summary>
    public int DaysOverdueAsOf(DateOnly asOf)
    {
        return IsOverdueAsOf(asOf) ? asOf.DayNumber - PlannedReturnDate.DayNumber : 0;
    }

    /// <summary>Set by <c>OverdueMarkingWorker</c> (research R5) while the loan is still open — a no-op once already returned.</summary>
    public void MarkOverdue(DateTime at)
    {
        if (ReturnedAt.HasValue)
        {
            return;
        }

        IsOverdue = true;
    }

    /// <summary>Makes <c>ReturnReminderWorker</c> idempotent (LOAN-07) and raises the reminder's <see cref="LendingNotificationDueEto"/> (FR-022).</summary>
    public void MarkReminderSent(DateTime at)
    {
        ReminderSentAt = at;

        AddLocalEvent(new LendingNotificationDueEto
        {
            LoanId = Id,
            MemberId = MemberId,
            ToolInstanceId = ToolInstanceId,
            Kind = LendingNotificationKind.ReturnReminder,
            PlannedReturnDate = PlannedReturnDate,
            RaisedAt = at
        });
    }

    /// <summary>Makes <c>OverdueMarkingWorker</c> idempotent (LOAN-07) and raises the overdue notice's <see cref="LendingNotificationDueEto"/> (FR-023).</summary>
    public void MarkOverdueNoticeSent(DateTime at)
    {
        OverdueNoticeSentAt = at;

        AddLocalEvent(new LendingNotificationDueEto
        {
            LoanId = Id,
            MemberId = MemberId,
            ToolInstanceId = ToolInstanceId,
            Kind = LendingNotificationKind.Overdue,
            PlannedReturnDate = PlannedReturnDate,
            RaisedAt = at
        });
    }

    /// <summary>Set once every applicable reliability outcome has been reported to Membership (LOAN-05), so a retried close does not double-report.</summary>
    public void MarkReliabilityReported(DateTime at)
    {
        ReliabilityReportedAt = at;
    }
}
