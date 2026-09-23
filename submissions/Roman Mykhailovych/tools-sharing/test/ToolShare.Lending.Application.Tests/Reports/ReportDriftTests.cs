using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using Xunit;

namespace ToolShare.Lending.Reports;

/// <summary>
/// The structural guard research R2 relies on instead of introducing an ABP
/// <c>Specification</c>. The overdue rule is necessarily expressed twice —
/// once as translatable SQL inside <c>ReportAppService</c>, once as the pure
/// <c>Loan.IsOverdueAsOf</c> — because EF Core cannot translate an instance
/// method. This asserts the two select exactly the same set. If someone later
/// changes one and not the other, this is the test that notices.
/// </summary>
public class ReportDriftTests : ReportTestBase
{
    [Fact]
    public async Task The_reports_SQL_filter_selects_exactly_what_Loan_IsOverdueAsOf_selects()
    {
        var (_, _, overdueLongAgo) = await SeedCatalogDataAsync();
        var (_, _, overdueYesterday) = await SeedCatalogDataAsync();
        var (_, _, dueToday) = await SeedCatalogDataAsync();
        var (_, _, dueTomorrow) = await SeedCatalogDataAsync();
        var (_, _, returnedLate) = await SeedCatalogDataAsync();
        var (_, _, returnedOnTime) = await SeedCatalogDataAsync();

        var librarianMemberId = await LibrarianMemberIdAsync();
        var noGrantsMemberId = await ResolveMemberIdAsync(LendingTestPrincipals.NoGrantsUserId);

        await InsertOverdueLoanAsync(librarianMemberId, overdueLongAgo.Id, daysOverdue: 45);
        await InsertOverdueLoanAsync(noGrantsMemberId, overdueYesterday.Id, daysOverdue: 1);

        // Boundary cases: due exactly today (not overdue) and due tomorrow.
        await InsertLoanAsync(librarianMemberId, dueToday.Id, Today.AddDays(-5), Today);
        await InsertLoanAsync(librarianMemberId, dueTomorrow.Id, Today.AddDays(-5), Today.AddDays(1));

        // A late return — the stored IsOverdue flag is true for this one, so an
        // implementation reading the flag would disagree with IsOverdueAsOf here.
        var lateLoan = await InsertOverdueLoanAsync(librarianMemberId, returnedLate.Id, daysOverdue: 12);
        lateLoan.Return(DateTime.UtcNow, ToolCondition.Good);
        await LoanRepository.UpdateAsync(lateLoan, autoSave: true);
        lateLoan.IsOverdue.ShouldBeTrue();

        await InsertCompletedLoanAsync(librarianMemberId, returnedOnTime.Id, Today.AddDays(-20));

        var today = Today;

        var allLoans = await LoanRepository.GetListAsync();
        var expected = allLoans
            .Where(l => l.IsOverdueAsOf(today))
            .Select(l => l.Id)
            .OrderBy(id => id)
            .ToList();

        // The fixture must actually exercise the predicate in both directions,
        // or set equality would be vacuously true.
        expected.Count.ShouldBe(2);
        allLoans.Count.ShouldBe(6);

        var reported = await GetReportedLoanIdsAsync();

        reported.ShouldBe(expected);
    }

    [Fact]
    public async Task The_two_expressions_still_agree_when_nothing_is_overdue()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var memberId = await LibrarianMemberIdAsync();

        await InsertCompletedLoanAsync(memberId, instance.Id, Today.AddDays(-30));

        var today = Today;
        var allLoans = await LoanRepository.GetListAsync();
        var expected = allLoans.Where(l => l.IsOverdueAsOf(today)).Select(l => l.Id).OrderBy(id => id).ToList();

        expected.ShouldBeEmpty();
        (await GetReportedLoanIdsAsync()).ShouldBe(expected);
    }

    private async Task<System.Collections.Generic.List<Guid>> GetReportedLoanIdsAsync()
    {
        using (AsLibrarian())
        {
            var result = await ReportAppService.GetOverdueLoansAsync();
            return result.Items.Select(i => i.LoanId).OrderBy(id => id).ToList();
        }
    }
}
