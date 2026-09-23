using System;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Lending.Reservations;
using Xunit;

namespace ToolShare.Lending.Loans;

/// <summary>
/// 006-librarian-reports, data-model.md §2 — the two pure query methods the
/// overdue report computes from. No database: these are the feature's only
/// Domain-layer code and Principle V's "domain rules get unit tests with no
/// database" applies to them.
///
/// The boundary these pin down: a loan is <b>not</b> overdue on its planned
/// return date and becomes overdue the following day with a days-overdue value
/// of 1 — the same comparison shape <see cref="Loan.Return"/> and
/// <c>EfCoreLoanRepository.GetNewlyOverdueAsync</c> already use.
/// </summary>
public class LoanOverdueCalculationTests
{
    private static readonly DateTime Now = new(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc);

    private static readonly DateOnly PlannedReturn = DateOnly.FromDateTime(Now).AddDays(5);

    private static Loan CreateOpenLoan()
    {
        var reservation = new Reservation(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateOnly.FromDateTime(Now),
            PlannedReturn,
            maxLoanTermDays: 14, createdAt: Now);

        return new Loan(Guid.NewGuid(), reservation, Now, ToolCondition.Good);
    }

    [Fact]
    public void A_loan_is_not_overdue_on_its_planned_return_date()
    {
        var loan = CreateOpenLoan();

        loan.IsOverdueAsOf(PlannedReturn).ShouldBeFalse();
        loan.DaysOverdueAsOf(PlannedReturn).ShouldBe(0);
    }

    [Fact]
    public void A_loan_is_overdue_the_day_after_its_planned_return_date_by_one_day()
    {
        var loan = CreateOpenLoan();

        loan.IsOverdueAsOf(PlannedReturn.AddDays(1)).ShouldBeTrue();
        loan.DaysOverdueAsOf(PlannedReturn.AddDays(1)).ShouldBe(1);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(10)]
    [InlineData(365)]
    public void Days_overdue_counts_whole_days_past_the_planned_return_date(int daysPast)
    {
        var loan = CreateOpenLoan();

        loan.DaysOverdueAsOf(PlannedReturn.AddDays(daysPast)).ShouldBe(daysPast);
    }

    [Fact]
    public void A_loan_before_its_planned_return_date_is_not_overdue_and_counts_zero_days()
    {
        var loan = CreateOpenLoan();

        loan.IsOverdueAsOf(PlannedReturn.AddDays(-1)).ShouldBeFalse();
        loan.DaysOverdueAsOf(PlannedReturn.AddDays(-1)).ShouldBe(0);
    }

    [Fact]
    public void A_returned_loan_is_never_overdue_however_late_the_return_was()
    {
        var loan = CreateOpenLoan();

        // Returned 30 days late — Loan.Return sets the stored IsOverdue flag,
        // which is exactly what these methods must NOT consult: the report
        // answers "who is holding something they should have brought back",
        // not "which returns were late".
        loan.Return(Now.AddDays(35), ToolCondition.Good);
        loan.IsOverdue.ShouldBeTrue();

        loan.IsOverdueAsOf(PlannedReturn.AddDays(30)).ShouldBeFalse();
        loan.DaysOverdueAsOf(PlannedReturn.AddDays(30)).ShouldBe(0);
    }

    [Fact]
    public void An_on_time_returned_loan_is_not_overdue_either()
    {
        var loan = CreateOpenLoan();

        loan.Return(Now.AddDays(2), ToolCondition.Good);

        loan.IsOverdueAsOf(PlannedReturn.AddDays(10)).ShouldBeFalse();
        loan.DaysOverdueAsOf(PlannedReturn.AddDays(10)).ShouldBe(0);
    }

    [Fact]
    public void The_stored_IsOverdue_flag_does_not_make_an_open_loan_report_as_overdue_before_its_date()
    {
        var loan = CreateOpenLoan();

        // The worker can only ever set the flag on a loan that is already past
        // its date, but the methods must be defined by the dates alone.
        loan.MarkOverdue(Now);
        loan.IsOverdue.ShouldBeTrue();

        loan.IsOverdueAsOf(PlannedReturn).ShouldBeFalse();
    }
}
