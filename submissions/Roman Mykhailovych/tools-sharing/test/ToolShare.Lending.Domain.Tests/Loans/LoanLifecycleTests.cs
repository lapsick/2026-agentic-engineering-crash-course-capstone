using System;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Lending.Reservations;
using Volo.Abp;
using Xunit;

namespace ToolShare.Lending.Loans;

/// <summary>LOAN-01, LOAN-03, LOAN-04 — pure entity guards, no database.</summary>
public class LoanLifecycleTests
{
    private static readonly DateTime Now = new(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc);

    private static Reservation CreateActiveReservation()
    {
        return new Reservation(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateOnly.FromDateTime(Now),
            DateOnly.FromDateTime(Now).AddDays(5),
            maxLoanTermDays: 14, createdAt: Now);
    }

    [Fact]
    public void Create_requires_an_Active_reservation_and_realizes_it()
    {
        var reservation = CreateActiveReservation();

        var loan = new Loan(Guid.NewGuid(), reservation, Now, ToolCondition.Good);

        loan.ReservationId.ShouldBe(reservation.Id);
        loan.MemberId.ShouldBe(reservation.MemberId);
        loan.ToolInstanceId.ShouldBe(reservation.ToolInstanceId);
        loan.PlannedReturnDate.ShouldBe(reservation.EndDate);
        loan.ConditionAtCheckout.ShouldBe(ToolCondition.Good);
        reservation.Status.ShouldBe(ReservationStatus.CheckedOut);
    }

    [Fact]
    public void Create_against_an_already_realized_reservation_throws()
    {
        var reservation = CreateActiveReservation();
        reservation.RealizeAsCheckedOut();

        Should.Throw<BusinessException>(() => new Loan(Guid.NewGuid(), reservation, Now, ToolCondition.Good));
    }

    [Fact]
    public void Return_is_permitted_only_once()
    {
        var reservation = CreateActiveReservation();
        var loan = new Loan(Guid.NewGuid(), reservation, Now, ToolCondition.Good);

        loan.Return(Now.AddDays(3), ToolCondition.Good);
        loan.ReturnedAt.ShouldBe(Now.AddDays(3));

        Should.Throw<BusinessException>(() => loan.Return(Now.AddDays(4), ToolCondition.Good));
    }

    [Fact]
    public void Return_after_the_planned_date_marks_overdue()
    {
        var reservation = CreateActiveReservation();
        var loan = new Loan(Guid.NewGuid(), reservation, Now, ToolCondition.Good);

        loan.Return(Now.AddDays(10), ToolCondition.Good);

        loan.IsOverdue.ShouldBeTrue();
    }

    [Fact]
    public void Return_on_or_before_the_planned_date_is_not_overdue()
    {
        var reservation = CreateActiveReservation();
        var loan = new Loan(Guid.NewGuid(), reservation, Now, ToolCondition.Good);

        loan.Return(Now.AddDays(1), ToolCondition.Good);

        loan.IsOverdue.ShouldBeFalse();
    }

    [Theory]
    [InlineData(ToolCondition.New, ToolCondition.New, false)]
    [InlineData(ToolCondition.New, ToolCondition.Good, true)]
    [InlineData(ToolCondition.Good, ToolCondition.New, false)]
    [InlineData(ToolCondition.Good, ToolCondition.Worn, true)]
    [InlineData(ToolCondition.Worn, ToolCondition.Damaged, true)]
    [InlineData(ToolCondition.Damaged, ToolCondition.Worn, false)]
    [InlineData(ToolCondition.New, ToolCondition.Damaged, true)]
    [InlineData(ToolCondition.Damaged, ToolCondition.New, false)]
    public void IsWorsened_compares_every_pair_on_the_four_level_scale(ToolCondition atCheckout, ToolCondition atReturn, bool expectedWorsened)
    {
        var reservation = CreateActiveReservation();
        var loan = new Loan(Guid.NewGuid(), reservation, Now, atCheckout);

        loan.Return(Now.AddDays(1), atReturn);

        loan.IsWorsened().ShouldBe(expectedWorsened);
    }

    [Fact]
    public void MarkOverdue_sets_the_flag_while_still_open()
    {
        var reservation = CreateActiveReservation();
        var loan = new Loan(Guid.NewGuid(), reservation, Now, ToolCondition.Good);

        loan.MarkOverdue(Now.AddDays(6));

        loan.IsOverdue.ShouldBeTrue();
    }
}
