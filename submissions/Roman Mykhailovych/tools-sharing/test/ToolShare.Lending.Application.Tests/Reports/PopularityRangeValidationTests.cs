using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ToolShare.Lending.Reports;

/// <summary>
/// FR-009 / guarantee B8 for <c>GetToolPopularityAsync</c>, plus the inclusive
/// and open-ended range semantics data-model.md §4 fixes. US2 owns this file so
/// the story stays shippable independently of US3.
/// </summary>
public class PopularityRangeValidationTests : ReportTestBase
{
    [Fact]
    public async Task An_inverted_range_is_rejected_rather_than_answered_with_an_empty_list()
    {
        // An empty list would read as "nothing happened in that period" — a
        // wrong answer to a question that was never valid.
        var exception = await Should.ThrowAsync<BusinessException>(() => GetPopularityAsync(new ToolPopularityReportInput
        {
            From = Today,
            To = Today.AddDays(-1)
        }));

        exception.Code.ShouldBe(LendingDomainErrorCodes.InvalidReportDateRange);
    }

    [Fact]
    public async Task Both_bounds_are_inclusive()
    {
        var (_, instances) = await SeedToolWithInstancesAsync("Circular Saw", 3);
        var memberId = await LibrarianMemberIdAsync();

        var from = Today.AddDays(-10);
        var to = Today.AddDays(-4);

        await InsertCompletedLoanAsync(memberId, instances[0].Id, from);           // exactly on From
        await InsertCompletedLoanAsync(memberId, instances[1].Id, to);             // exactly on To
        await InsertCompletedLoanAsync(memberId, instances[2].Id, from.AddDays(-1)); // just outside

        var result = await GetPopularityAsync(new ToolPopularityReportInput { From = from, To = to });

        result.Items.ShouldHaveSingleItem().LoanCount.ShouldBe(2);
    }

    [Fact]
    public async Task Supplying_only_a_lower_bound_is_open_ended_on_the_other_side()
    {
        var (_, instances) = await SeedToolWithInstancesAsync("Circular Saw", 2);
        var memberId = await LibrarianMemberIdAsync();

        await InsertCompletedLoanAsync(memberId, instances[0].Id, Today.AddDays(-100));
        await InsertCompletedLoanAsync(memberId, instances[1].Id, Today.AddDays(-3));

        var result = await GetPopularityAsync(new ToolPopularityReportInput { From = Today.AddDays(-20) });

        result.Items.ShouldHaveSingleItem().LoanCount.ShouldBe(1);
    }

    [Fact]
    public async Task Supplying_only_an_upper_bound_is_open_ended_on_the_other_side()
    {
        var (_, instances) = await SeedToolWithInstancesAsync("Circular Saw", 2);
        var memberId = await LibrarianMemberIdAsync();

        await InsertCompletedLoanAsync(memberId, instances[0].Id, Today.AddDays(-100));
        await InsertCompletedLoanAsync(memberId, instances[1].Id, Today.AddDays(-3));

        var result = await GetPopularityAsync(new ToolPopularityReportInput { To = Today.AddDays(-20) });

        result.Items.ShouldHaveSingleItem().LoanCount.ShouldBe(1);
    }

    [Fact]
    public async Task Omitting_both_bounds_yields_the_all_time_result()
    {
        // FR-002: for popularity — unlike the maintenance cost report — an
        // omitted bound is meaningful rather than invalid.
        var (_, instances) = await SeedToolWithInstancesAsync("Circular Saw", 2);
        var memberId = await LibrarianMemberIdAsync();

        await InsertCompletedLoanAsync(memberId, instances[0].Id, Today.AddDays(-500));
        await InsertCompletedLoanAsync(memberId, instances[1].Id, Today.AddDays(-1));

        var result = await GetPopularityAsync(new ToolPopularityReportInput());

        result.Items.ShouldHaveSingleItem().LoanCount.ShouldBe(2);
    }

    [Fact]
    public async Task A_single_day_range_where_From_equals_To_is_valid()
    {
        var (_, instances) = await SeedToolWithInstancesAsync("Circular Saw", 2);
        var memberId = await LibrarianMemberIdAsync();

        var day = Today.AddDays(-8);
        await InsertCompletedLoanAsync(memberId, instances[0].Id, day);
        await InsertCompletedLoanAsync(memberId, instances[1].Id, day.AddDays(1));

        var result = await GetPopularityAsync(new ToolPopularityReportInput { From = day, To = day });

        result.Items.ShouldHaveSingleItem().LoanCount.ShouldBe(1);
    }

    private async Task<Volo.Abp.Application.Dtos.ListResultDto<ToolPopularityReportItemDto>> GetPopularityAsync(ToolPopularityReportInput input)
    {
        using (AsLibrarian())
        {
            return await ReportAppService.GetToolPopularityAsync(input);
        }
    }
}
