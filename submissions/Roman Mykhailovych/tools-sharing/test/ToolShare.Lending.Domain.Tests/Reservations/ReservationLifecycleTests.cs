using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ToolShare.Lending.Reservations;

/// <summary>RES-01, RES-06, RES-07, RES-08 — pure entity guards, no database.</summary>
public class ReservationLifecycleTests
{
    private static readonly DateTime Now = new(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Creating_within_the_maximum_loan_term_succeeds()
    {
        var reservation = new Reservation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 10),
            new DateOnly(2026, 8, 17),
            maxLoanTermDays: 14,
            createdAt: Now);

        reservation.Status.ShouldBe(ReservationStatus.Active);
        reservation.StartDate.ShouldBe(new DateOnly(2026, 8, 10));
        reservation.EndDate.ShouldBe(new DateOnly(2026, 8, 17));
    }

    [Fact]
    public void Creating_beyond_the_maximum_loan_term_throws()
    {
        Should.Throw<BusinessException>(() => new Reservation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 10),
            new DateOnly(2026, 9, 10),
            maxLoanTermDays: 14,
            createdAt: Now)).Code.ShouldBe(LendingDomainErrorCodes.LoanTermExceeded);
    }

    [Fact]
    public void Creating_with_end_before_start_throws()
    {
        Should.Throw<BusinessException>(() => new Reservation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 10),
            new DateOnly(2026, 8, 9),
            maxLoanTermDays: 14,
            createdAt: Now));
    }

    [Fact]
    public void Cancel_is_permitted_only_while_Active()
    {
        var reservation = CreateActiveReservation();

        reservation.Cancel(Now);
        reservation.Status.ShouldBe(ReservationStatus.Cancelled);
        reservation.CancelledAt.ShouldBe(Now);

        Should.Throw<BusinessException>(() => reservation.Cancel(Now))
            .Code.ShouldBe(LendingDomainErrorCodes.ReservationNotCancellable);
    }

    [Fact]
    public void RealizeAsCheckedOut_is_permitted_only_once_from_Active()
    {
        var reservation = CreateActiveReservation();

        reservation.RealizeAsCheckedOut();
        reservation.Status.ShouldBe(ReservationStatus.CheckedOut);

        Should.Throw<BusinessException>(() => reservation.RealizeAsCheckedOut());
    }

    [Fact]
    public void Cancel_after_checkout_is_rejected()
    {
        var reservation = CreateActiveReservation();
        reservation.RealizeAsCheckedOut();

        Should.Throw<BusinessException>(() => reservation.Cancel(Now))
            .Code.ShouldBe(LendingDomainErrorCodes.ReservationNotCancellable);
    }

    [Fact]
    public void CancelForMaintenance_is_permitted_only_while_Active_and_not_yet_started()
    {
        var reservation = new Reservation(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateOnly.FromDateTime(Now).AddDays(5),
            DateOnly.FromDateTime(Now).AddDays(10),
            maxLoanTermDays: 14, createdAt: Now);

        reservation.CancelForMaintenance(Now, "Instance requires repair");

        reservation.Status.ShouldBe(ReservationStatus.Cancelled);
        reservation.CancellationReason.ShouldBe("Instance requires repair");
    }

    [Fact]
    public void CancelForMaintenance_on_an_already_started_reservation_throws()
    {
        var reservation = new Reservation(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateOnly.FromDateTime(Now),
            DateOnly.FromDateTime(Now).AddDays(5),
            maxLoanTermDays: 14, createdAt: Now);

        Should.Throw<BusinessException>(() => reservation.CancelForMaintenance(Now, "reason"));
    }

    // ---- 008 RES-09: out-of-band cascade (FR-008) ----

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void CancelUncollectedForMaintenance_cancels_an_active_reservation_whether_or_not_it_has_started(int startsInDays)
    {
        var reservation = new Reservation(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateOnly.FromDateTime(Now).AddDays(startsInDays),
            DateOnly.FromDateTime(Now).AddDays(startsInDays + 3),
            maxLoanTermDays: 14, createdAt: Now);

        reservation.CancelUncollectedForMaintenance(Now, "  Instance taken out of circulation for maintenance.  ");

        reservation.Status.ShouldBe(ReservationStatus.Cancelled);
        reservation.CancelledAt.ShouldBe(Now);
        reservation.CancellationReason.ShouldBe("Instance taken out of circulation for maintenance.");
    }

    [Fact]
    public void CancelUncollectedForMaintenance_refuses_a_checked_out_reservation()
    {
        var reservation = CreateActiveReservation();
        reservation.RealizeAsCheckedOut();

        Should.Throw<BusinessException>(() => reservation.CancelUncollectedForMaintenance(Now, "reason"))
            .Code.ShouldBe(LendingDomainErrorCodes.InvalidStateTransition);
    }

    [Fact]
    public void CancelUncollectedForMaintenance_refuses_an_already_cancelled_reservation()
    {
        var reservation = CreateActiveReservation();
        reservation.Cancel(Now);

        Should.Throw<BusinessException>(() => reservation.CancelUncollectedForMaintenance(Now, "reason"))
            .Code.ShouldBe(LendingDomainErrorCodes.InvalidStateTransition);
    }

    [Fact]
    public void CancelUncollectedForMaintenance_refuses_a_reason_over_the_cancellation_limit()
    {
        var reservation = CreateActiveReservation();

        Should.Throw<ArgumentException>(() => reservation.CancelUncollectedForMaintenance(
            Now, new string('x', LendingDomainSharedConsts.CancellationReasonMaxLength + 1)));
    }

    private static Reservation CreateActiveReservation()
    {
        return new Reservation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateOnly.FromDateTime(Now).AddDays(1),
            DateOnly.FromDateTime(Now).AddDays(5),
            maxLoanTermDays: 14,
            createdAt: Now);
    }
}
