using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ToolShare.Lending.Reports;

/// <summary>
/// FR-009 / guarantee B8 for <c>GetMaintenanceCostAsync</c>. Its own file — not
/// shared with US2's equivalent — so US3 stays shippable without US2.
///
/// The null-bound cases are the ones worth having: both properties are
/// <c>DateOnly?</c> precisely so an omitted bound is representable and can be
/// rejected, rather than binding to <c>default(DateOnly)</c> and returning a
/// near-all-time total dressed up as a valid period result
/// (contracts/lending-reports-app-service.md, "Inputs").
/// </summary>
public class MaintenanceCostRangeValidationTests : ReportTestBase
{
    [Fact]
    public async Task An_inverted_range_is_rejected()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() => RunAsync(new MaintenanceCostReportInput
        {
            From = Today,
            To = Today.AddDays(-1)
        }));

        exception.Code.ShouldBe(LendingDomainErrorCodes.InvalidReportDateRange);
    }

    [Fact]
    public async Task A_missing_lower_bound_is_rejected_rather_than_defaulting_to_the_beginning_of_time()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() => RunAsync(new MaintenanceCostReportInput
        {
            From = null,
            To = Today
        }));

        exception.Code.ShouldBe(LendingDomainErrorCodes.InvalidReportDateRange);
    }

    [Fact]
    public async Task A_missing_upper_bound_is_rejected()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() => RunAsync(new MaintenanceCostReportInput
        {
            From = Today.AddDays(-30),
            To = null
        }));

        exception.Code.ShouldBe(LendingDomainErrorCodes.InvalidReportDateRange);
    }

    [Fact]
    public async Task Both_bounds_missing_is_rejected()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() => RunAsync(new MaintenanceCostReportInput()));

        exception.Code.ShouldBe(LendingDomainErrorCodes.InvalidReportDateRange);
    }

    [Fact]
    public async Task Both_bounds_are_inclusive()
    {
        var (_, instances) = await SeedToolWithInstancesAsync("Circular Saw", 3);

        var from = Today.AddDays(-10);
        var to = Today.AddDays(-4);

        await InsertClosedMaintenanceRequestAsync(instances[0].Id, Guid.NewGuid(), Today.AddDays(-20), from, 10m);
        await InsertClosedMaintenanceRequestAsync(instances[1].Id, Guid.NewGuid(), Today.AddDays(-20), to, 20m);
        await InsertClosedMaintenanceRequestAsync(instances[2].Id, Guid.NewGuid(), Today.AddDays(-20), from.AddDays(-1), 400m);

        var result = await RunAsync(new MaintenanceCostReportInput { From = from, To = to });

        result.TotalCost.ShouldBe(30m);
        result.ClosedRequestCount.ShouldBe(2);
    }

    [Fact]
    public async Task A_single_day_range_where_From_equals_To_is_valid()
    {
        var (_, instances) = await SeedToolWithInstancesAsync("Circular Saw", 2);
        var day = Today.AddDays(-6);

        await InsertClosedMaintenanceRequestAsync(instances[0].Id, Guid.NewGuid(), Today.AddDays(-20), day, 15m);
        await InsertClosedMaintenanceRequestAsync(instances[1].Id, Guid.NewGuid(), Today.AddDays(-20), day.AddDays(1), 99m);

        var result = await RunAsync(new MaintenanceCostReportInput { From = day, To = day });

        result.TotalCost.ShouldBe(15m);
        result.ClosedRequestCount.ShouldBe(1);
    }

    private async Task<MaintenanceCostReportDto> RunAsync(MaintenanceCostReportInput input)
    {
        using (AsLibrarian())
        {
            return await ReportAppService.GetMaintenanceCostAsync(input);
        }
    }
}
