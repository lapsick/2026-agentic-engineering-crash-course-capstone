using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ToolShare.Lending.Reports;

/// <summary>
/// US2 / FR-001–FR-003 and guarantees B1, B5, B6 from
/// contracts/lending-reports-app-service.md.
/// </summary>
public class PopularityReportTests : ReportTestBase
{
    [Fact]
    public async Task Tools_are_ranked_by_all_time_loan_count_most_borrowed_first()
    {
        // FR-001.
        var (toolA, instancesA) = await SeedToolWithInstancesAsync("Circular Saw", 1);
        var (toolB, instancesB) = await SeedToolWithInstancesAsync("Torque Wrench", 1);
        var memberId = await LibrarianMemberIdAsync();

        await InsertCompletedLoanAsync(memberId, instancesA[0].Id, Today.AddDays(-40));
        await InsertCompletedLoanAsync(memberId, instancesA[0].Id, Today.AddDays(-30));
        await InsertCompletedLoanAsync(memberId, instancesA[0].Id, Today.AddDays(-20));
        await InsertCompletedLoanAsync(memberId, instancesB[0].Id, Today.AddDays(-10));

        var result = await GetPopularityAsync(new ToolPopularityReportInput());

        result.Items.Count.ShouldBe(2);
        result.Items[0].ToolId.ShouldBe(toolA.Id);
        result.Items[0].ToolName.ShouldBe("Circular Saw");
        result.Items[0].LoanCount.ShouldBe(3);
        result.Items[1].ToolId.ShouldBe(toolB.Id);
        result.Items[1].LoanCount.ShouldBe(1);
    }

    [Fact]
    public async Task A_date_range_restricts_the_count_to_loans_checked_out_within_it()
    {
        // FR-002.
        var (toolA, instancesA) = await SeedToolWithInstancesAsync("Circular Saw", 1);
        var (toolB, instancesB) = await SeedToolWithInstancesAsync("Torque Wrench", 1);
        var memberId = await LibrarianMemberIdAsync();

        // A: two loans long ago, one recent. B: two recent.
        await InsertCompletedLoanAsync(memberId, instancesA[0].Id, Today.AddDays(-90));
        await InsertCompletedLoanAsync(memberId, instancesA[0].Id, Today.AddDays(-80));
        await InsertCompletedLoanAsync(memberId, instancesA[0].Id, Today.AddDays(-5));
        await InsertCompletedLoanAsync(memberId, instancesB[0].Id, Today.AddDays(-6));
        await InsertCompletedLoanAsync(memberId, instancesB[0].Id, Today.AddDays(-4));

        var allTime = await GetPopularityAsync(new ToolPopularityReportInput());
        allTime.Items[0].ToolId.ShouldBe(toolA.Id);
        allTime.Items[0].LoanCount.ShouldBe(3);

        // Narrowing to the last week flips the ranking.
        var narrowed = await GetPopularityAsync(new ToolPopularityReportInput
        {
            From = Today.AddDays(-7),
            To = Today
        });

        narrowed.Items.Count.ShouldBe(2);
        narrowed.Items[0].ToolId.ShouldBe(toolB.Id);
        narrowed.Items[0].LoanCount.ShouldBe(2);
        narrowed.Items[1].ToolId.ShouldBe(toolA.Id);
        narrowed.Items[1].LoanCount.ShouldBe(1);
    }

    [Fact]
    public async Task Loans_of_a_retired_instance_still_count()
    {
        // FR-003 / SC-004 / B6 — retirement does not erase history. This works
        // because Catalog's lookup returns retired instances by contract; if
        // the count drops, something is filtering them out.
        var (tool, instances) = await SeedToolWithInstancesAsync("Bench Grinder", 1);
        var memberId = await LibrarianMemberIdAsync();

        await InsertCompletedLoanAsync(memberId, instances[0].Id, Today.AddDays(-30));
        await InsertCompletedLoanAsync(memberId, instances[0].Id, Today.AddDays(-20));

        var beforeRetirement = await GetPopularityAsync(new ToolPopularityReportInput());
        beforeRetirement.Items.ShouldHaveSingleItem().LoanCount.ShouldBe(2);

        await RetireInstanceAsync(instances[0].Id);

        var afterRetirement = await GetPopularityAsync(new ToolPopularityReportInput());

        var item = afterRetirement.Items.ShouldHaveSingleItem();
        item.ToolId.ShouldBe(tool.Id);
        item.ToolName.ShouldBe("Bench Grinder");
        item.LoanCount.ShouldBe(2);
    }

    [Fact]
    public async Task Several_instances_of_one_tool_fold_into_a_single_ranked_row()
    {
        // research R4: Loan stores ToolInstanceId only, so the instance→tool
        // fold happens outside SQL. Three instances of one tool must produce
        // one row carrying the summed count, not three rows.
        var (tool, instances) = await SeedToolWithInstancesAsync("Cordless Drill", 3);
        var memberId = await LibrarianMemberIdAsync();

        await InsertCompletedLoanAsync(memberId, instances[0].Id, Today.AddDays(-30));
        await InsertCompletedLoanAsync(memberId, instances[1].Id, Today.AddDays(-25));
        await InsertCompletedLoanAsync(memberId, instances[1].Id, Today.AddDays(-24));
        await InsertCompletedLoanAsync(memberId, instances[2].Id, Today.AddDays(-20));

        var result = await GetPopularityAsync(new ToolPopularityReportInput());

        var item = result.Items.ShouldHaveSingleItem();
        item.ToolId.ShouldBe(tool.Id);
        item.LoanCount.ShouldBe(4);
    }

    [Fact]
    public async Task A_never_borrowed_tool_is_absent_rather_than_breaking_the_report()
    {
        // research R5 — the spec accepts either zero-row or omission; the
        // loan-first query omits, and must not choke on the unborrowed tool.
        var (borrowed, borrowedInstances) = await SeedToolWithInstancesAsync("Circular Saw", 1);
        var (neverBorrowed, _) = await SeedToolWithInstancesAsync("Post Hole Digger", 2);
        var memberId = await LibrarianMemberIdAsync();

        await InsertCompletedLoanAsync(memberId, borrowedInstances[0].Id, Today.AddDays(-15));

        var result = await GetPopularityAsync(new ToolPopularityReportInput());

        result.Items.ShouldHaveSingleItem().ToolId.ShouldBe(borrowed.Id);
        result.Items.ShouldNotContain(i => i.ToolId == neverBorrowed.Id);
    }

    [Fact]
    public async Task A_range_containing_no_loans_is_a_successful_empty_list()
    {
        // FR-013 / B5 — an empty period is an answer, not an error.
        var (_, instances) = await SeedToolWithInstancesAsync("Circular Saw", 1);
        var memberId = await LibrarianMemberIdAsync();

        await InsertCompletedLoanAsync(memberId, instances[0].Id, Today.AddDays(-200));

        var result = await GetPopularityAsync(new ToolPopularityReportInput
        {
            From = Today.AddDays(-10),
            To = Today
        });

        result.ShouldNotBeNull();
        result.Items.ShouldNotBeNull();
        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Producing_the_report_mutates_nothing()
    {
        // B1 / FR-011.
        var (_, instances) = await SeedToolWithInstancesAsync("Circular Saw", 2);
        var memberId = await LibrarianMemberIdAsync();

        await InsertCompletedLoanAsync(memberId, instances[0].Id, Today.AddDays(-30));
        await InsertCompletedLoanAsync(memberId, instances[1].Id, Today.AddDays(-20));

        var countBefore = await LoanRepository.GetCountAsync();

        await GetPopularityAsync(new ToolPopularityReportInput());
        await GetPopularityAsync(new ToolPopularityReportInput { From = Today.AddDays(-40), To = Today });

        (await LoanRepository.GetCountAsync()).ShouldBe(countBefore);
    }

    private async Task<Volo.Abp.Application.Dtos.ListResultDto<ToolPopularityReportItemDto>> GetPopularityAsync(ToolPopularityReportInput input)
    {
        using (AsLibrarian())
        {
            return await ReportAppService.GetToolPopularityAsync(input);
        }
    }
}
