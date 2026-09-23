using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Lending.Loans;
using Xunit;

namespace ToolShare.Lending.Reports;

/// <summary>
/// US1 / FR-004–FR-006 and behavioral guarantees B2–B5, B9 from
/// contracts/lending-reports-app-service.md.
/// </summary>
public class OverdueReportTests : ReportTestBase
{
    [Fact]
    public async Task An_overdue_loan_appears_without_the_OverdueMarkingWorker_having_run()
    {
        // B2: the report computes overdue live. OverdueMarkingWorker is
        // deliberately NOT invoked here — the stored IsOverdue flag is still
        // false, and an implementation reading it would return nothing.
        var (_, tool, instance) = await SeedCatalogDataAsync();
        var memberId = await LibrarianMemberIdAsync();

        var loan = await InsertOverdueLoanAsync(memberId, instance.Id, daysOverdue: 4);
        loan.IsOverdue.ShouldBeFalse("the worker has not run, so the stored flag must still be false");

        var result = await GetReportAsync();

        var item = result.Items.ShouldHaveSingleItem();
        item.LoanId.ShouldBe(loan.Id);
        item.MemberId.ShouldBe(memberId);
        item.MemberDisplayName.ShouldBe("Librarian Test User");
        item.ToolInstanceId.ShouldBe(instance.Id);
        item.ToolName.ShouldBe(tool.Name);
        item.SerialNumber.ShouldBe(instance.SerialNumber);
        item.PlannedReturnDate.ShouldBe(Today.AddDays(-4));
        item.CheckedOutAt.ShouldBe(loan.CheckedOutAt);
        item.DaysOverdue.ShouldBe(4);
    }

    [Fact]
    public async Task A_returned_loan_does_not_appear_however_late_the_return_was()
    {
        // B3.
        var (_, _, instance) = await SeedCatalogDataAsync();
        var memberId = await LibrarianMemberIdAsync();

        var loan = await InsertOverdueLoanAsync(memberId, instance.Id, daysOverdue: 9);
        loan.Return(DateTime.UtcNow, ToolCondition.Good);
        await LoanRepository.UpdateAsync(loan, autoSave: true);

        // The late return set the stored flag; the report must still exclude it.
        loan.IsOverdue.ShouldBeTrue();

        var result = await GetReportAsync();

        result.Items.ShouldNotContain(i => i.LoanId == loan.Id);
    }

    [Fact]
    public async Task A_loan_still_within_its_term_does_not_appear()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var memberId = await LibrarianMemberIdAsync();

        // Due back tomorrow — not overdue.
        var loan = await InsertLoanAsync(memberId, instance.Id, Today.AddDays(-2), Today.AddDays(1));

        var result = await GetReportAsync();

        result.Items.ShouldNotContain(i => i.LoanId == loan.Id);
    }

    [Fact]
    public async Task A_loan_due_back_today_is_not_yet_overdue()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var memberId = await LibrarianMemberIdAsync();

        var loan = await InsertLoanAsync(memberId, instance.Id, Today.AddDays(-2), Today);

        var result = await GetReportAsync();

        result.Items.ShouldNotContain(i => i.LoanId == loan.Id);
    }

    [Fact]
    public async Task Results_are_ordered_most_overdue_first()
    {
        // B4 / FR-006.
        var (_, _, instanceA) = await SeedCatalogDataAsync();
        var (_, _, instanceB) = await SeedCatalogDataAsync();
        var (_, _, instanceC) = await SeedCatalogDataAsync();

        var librarianMemberId = await LibrarianMemberIdAsync();
        var noGrantsMemberId = await ResolveMemberIdAsync(LendingTestPrincipals.NoGrantsUserId);

        var threeDays = await InsertOverdueLoanAsync(librarianMemberId, instanceA.Id, daysOverdue: 3);
        var tenDays = await InsertOverdueLoanAsync(noGrantsMemberId, instanceB.Id, daysOverdue: 10);
        var sixDays = await InsertOverdueLoanAsync(librarianMemberId, instanceC.Id, daysOverdue: 6);

        var result = await GetReportAsync();

        result.Items.Select(i => i.LoanId).ShouldBe(new[] { tenDays.Id, sixDays.Id, threeDays.Id });
        result.Items.Select(i => i.DaysOverdue).ShouldBe(new[] { 10, 6, 3 });
    }

    [Fact]
    public async Task Nothing_overdue_is_a_successful_empty_list_not_an_error()
    {
        // B5 / FR-013. Each test method runs against its own cloned database,
        // so "nothing overdue" here means genuinely nothing.
        var result = await GetReportAsync();

        result.ShouldNotBeNull();
        result.Items.ShouldNotBeNull();
        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_unresolvable_member_leaves_the_display_name_null_without_dropping_the_row()
    {
        // B9, and the spec's "a deactivated member's overdue loan still appears".
        var (_, tool, instance) = await SeedCatalogDataAsync();
        var unknownMemberId = Guid.NewGuid();

        var loan = await InsertOverdueLoanAsync(unknownMemberId, instance.Id, daysOverdue: 2);

        var result = await GetReportAsync();

        var item = result.Items.ShouldHaveSingleItem();
        item.LoanId.ShouldBe(loan.Id);
        item.MemberId.ShouldBe(unknownMemberId);
        item.MemberDisplayName.ShouldBeNull();
        // The tool side still resolves — one unresolvable reference must not
        // blank out the other.
        item.ToolName.ShouldBe(tool.Name);
    }

    [Fact]
    public async Task An_unresolvable_tool_instance_leaves_the_tool_name_null_without_dropping_the_row()
    {
        // B9, the spec's deleted-catalog-entry edge case.
        var memberId = await LibrarianMemberIdAsync();
        var unknownInstanceId = Guid.NewGuid();

        var loan = await InsertOverdueLoanAsync(memberId, unknownInstanceId, daysOverdue: 5);

        var result = await GetReportAsync();

        var item = result.Items.ShouldHaveSingleItem();
        item.LoanId.ShouldBe(loan.Id);
        item.ToolInstanceId.ShouldBe(unknownInstanceId);
        item.ToolName.ShouldBeNull();
        item.SerialNumber.ShouldBeNull();
        item.MemberDisplayName.ShouldBe("Librarian Test User");
        item.DaysOverdue.ShouldBe(5);
    }

    [Fact]
    public async Task Producing_the_report_mutates_nothing()
    {
        // B1 / FR-011.
        var (_, _, instance) = await SeedCatalogDataAsync();
        var memberId = await LibrarianMemberIdAsync();

        var loan = await InsertOverdueLoanAsync(memberId, instance.Id, daysOverdue: 7);

        var countBefore = await LoanRepository.GetCountAsync();
        var before = await LoanRepository.GetAsync(loan.Id);
        var isOverdueBefore = before.IsOverdue;
        var returnedAtBefore = before.ReturnedAt;

        await GetReportAsync();
        await GetReportAsync();

        var countAfter = await LoanRepository.GetCountAsync();
        var after = await LoanRepository.GetAsync(loan.Id);

        countAfter.ShouldBe(countBefore);
        after.IsOverdue.ShouldBe(isOverdueBefore);
        after.IsOverdue.ShouldBeFalse("the report must not mark loans overdue as a side effect");
        after.ReturnedAt.ShouldBe(returnedAtBefore);
    }

    private async Task<Volo.Abp.Application.Dtos.ListResultDto<OverdueLoanReportItemDto>> GetReportAsync()
    {
        using (AsLibrarian())
        {
            return await ReportAppService.GetOverdueLoansAsync();
        }
    }
}
