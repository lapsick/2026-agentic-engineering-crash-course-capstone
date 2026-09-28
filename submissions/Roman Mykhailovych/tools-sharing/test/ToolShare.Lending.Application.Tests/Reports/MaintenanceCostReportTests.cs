using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Lending.Maintenance;
using Xunit;

namespace ToolShare.Lending.Reports;

/// <summary>
/// US3 / FR-007, FR-008, FR-013 and guarantees B1, B5, B7 from
/// contracts/lending-reports-app-service.md.
/// </summary>
public class MaintenanceCostReportTests : ReportTestBase
{
    [Fact]
    public async Task The_total_sums_only_requests_closed_within_the_range()
    {
        // FR-007.
        var (_, instances) = await SeedToolWithInstancesAsync("Circular Saw", 3);

        await InsertClosedMaintenanceRequestAsync(instances[0].Id, Guid.NewGuid(), Today.AddDays(-40), Today.AddDays(-30), 120.50m);
        await InsertClosedMaintenanceRequestAsync(instances[1].Id, Guid.NewGuid(), Today.AddDays(-40), Today.AddDays(-25), 79.50m);
        // Outside the range below.
        await InsertClosedMaintenanceRequestAsync(instances[2].Id, Guid.NewGuid(), Today.AddDays(-10), Today.AddDays(-5), 999m);

        var result = await GetMaintenanceCostAsync(Today.AddDays(-35), Today.AddDays(-20));

        result.TotalCost.ShouldBe(200m);
        result.ClosedRequestCount.ShouldBe(2);
        result.From.ShouldBe(Today.AddDays(-35));
        result.To.ShouldBe(Today.AddDays(-20));
    }

    [Fact]
    public async Task An_open_request_contributes_nothing()
    {
        // FR-008 / B7 — no final cost has been recorded for it yet.
        var (_, instances) = await SeedToolWithInstancesAsync("Circular Saw", 2);

        await InsertClosedMaintenanceRequestAsync(instances[0].Id, Guid.NewGuid(), Today.AddDays(-20), Today.AddDays(-10), 45m);
        await InsertOpenMaintenanceRequestAsync(instances[1].Id, Guid.NewGuid(), Today.AddDays(-15));

        var result = await GetMaintenanceCostAsync(Today.AddDays(-30), Today);

        result.TotalCost.ShouldBe(45m);
        result.ClosedRequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task A_range_with_no_closures_is_a_successful_zero_not_an_error()
    {
        // FR-013 / B5 — zero is a result, distinct from an error or a blank.
        var (_, instances) = await SeedToolWithInstancesAsync("Circular Saw", 1);

        await InsertClosedMaintenanceRequestAsync(instances[0].Id, Guid.NewGuid(), Today.AddDays(-100), Today.AddDays(-90), 300m);

        var result = await GetMaintenanceCostAsync(Today.AddDays(-10), Today);

        result.ShouldNotBeNull();
        result.TotalCost.ShouldBe(0m);
        result.ClosedRequestCount.ShouldBe(0);
        result.From.ShouldBe(Today.AddDays(-10));
        result.To.ShouldBe(Today);
    }

    [Fact]
    public async Task Two_requests_for_the_same_instance_closed_the_same_day_both_contribute()
    {
        // spec Edge Cases: costs are never merged or deduplicated by instance.
        var (_, instances) = await SeedToolWithInstancesAsync("Circular Saw", 1);
        var closedOn = Today.AddDays(-7);

        await InsertClosedMaintenanceRequestAsync(instances[0].Id, Guid.NewGuid(), Today.AddDays(-20), closedOn, 30m);
        await InsertClosedMaintenanceRequestAsync(instances[0].Id, Guid.NewGuid(), Today.AddDays(-15), closedOn, 70m);

        var result = await GetMaintenanceCostAsync(Today.AddDays(-14), Today);

        result.TotalCost.ShouldBe(100m);
        result.ClosedRequestCount.ShouldBe(2);
    }

    [Fact]
    public async Task Cost_is_attributed_by_the_closure_date_not_the_opening_date()
    {
        // research R6: a request opened in one period and closed in a later one
        // charges the period the money was actually spent in.
        var (_, instances) = await SeedToolWithInstancesAsync("Circular Saw", 1);

        await InsertClosedMaintenanceRequestAsync(instances[0].Id, Guid.NewGuid(), openedOn: Today.AddDays(-60), closedOn: Today.AddDays(-10), cost: 250m);

        var periodOfOpening = await GetMaintenanceCostAsync(Today.AddDays(-70), Today.AddDays(-50));
        periodOfOpening.TotalCost.ShouldBe(0m);
        periodOfOpening.ClosedRequestCount.ShouldBe(0);

        var periodOfClosing = await GetMaintenanceCostAsync(Today.AddDays(-20), Today);
        periodOfClosing.TotalCost.ShouldBe(250m);
        periodOfClosing.ClosedRequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task A_zero_cost_closure_is_counted_as_a_closure()
    {
        // Zero is a valid, explicit cost (MaintenanceRequest.Close permits it),
        // which is exactly why ClosedRequestCount exists alongside TotalCost.
        var (_, instances) = await SeedToolWithInstancesAsync("Circular Saw", 1);

        await InsertClosedMaintenanceRequestAsync(instances[0].Id, Guid.NewGuid(), Today.AddDays(-20), Today.AddDays(-10), 0m);

        var result = await GetMaintenanceCostAsync(Today.AddDays(-30), Today);

        result.TotalCost.ShouldBe(0m);
        result.ClosedRequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task Producing_the_report_mutates_nothing()
    {
        // B1 / FR-011.
        var (_, instances) = await SeedToolWithInstancesAsync("Circular Saw", 2);

        var closed = await InsertClosedMaintenanceRequestAsync(instances[0].Id, Guid.NewGuid(), Today.AddDays(-20), Today.AddDays(-10), 60m);
        await InsertOpenMaintenanceRequestAsync(instances[1].Id, Guid.NewGuid(), Today.AddDays(-5));

        var countBefore = await MaintenanceRequestRepository.GetCountAsync();
        var before = await MaintenanceRequestRepository.GetAsync(closed.Id);
        var statusBefore = before.Status;
        var closedAtBefore = before.ClosedAt;
        var costBefore = before.Cost;

        await GetMaintenanceCostAsync(Today.AddDays(-30), Today);
        await GetMaintenanceCostAsync(Today.AddDays(-3), Today);

        (await MaintenanceRequestRepository.GetCountAsync()).ShouldBe(countBefore);

        var after = await MaintenanceRequestRepository.GetAsync(closed.Id);
        after.Status.ShouldBe(statusBefore);
        after.Status.ShouldBe(MaintenanceRequestStatus.Closed);
        after.ClosedAt.ShouldBe(closedAtBefore);
        after.Cost.ShouldBe(costBefore);
    }

    // ---- 008-out-of-band-maintenance US4 (FR-018, SC-005) ----

    [Fact]
    public async Task Both_origins_count_toward_the_total_and_their_subtotals_add_up_to_it()
    {
        var (_, instances) = await SeedToolWithInstancesAsync("Circular Saw", 5);
        var reporter = await LibrarianMemberIdAsync();

        await InsertClosedMaintenanceRequestAsync(instances[0].Id, Guid.NewGuid(), Today.AddDays(-20), Today.AddDays(-10), 30m);
        await InsertClosedOutOfBandRequestAsync(instances[1].Id, reporter, Today.AddDays(-15), Today.AddDays(-5), 20m);
        // Outside the range below — one of each origin.
        await InsertClosedMaintenanceRequestAsync(instances[2].Id, Guid.NewGuid(), Today.AddDays(-90), Today.AddDays(-80), 500m);
        await InsertClosedOutOfBandRequestAsync(instances[3].Id, reporter, Today.AddDays(-90), Today.AddDays(-80), 700m);
        // Open — contributes nothing.
        await InsertOpenOutOfBandRequestAsync(instances[4].Id, reporter, Today.AddDays(-3));

        var result = await GetMaintenanceCostAsync(Today.AddDays(-30), Today);

        result.TotalCost.ShouldBe(50m);
        result.ClosedRequestCount.ShouldBe(2);
        result.ReturnTriggeredSubtotal.ShouldBe(30m);
        result.OutOfBandSubtotal.ShouldBe(20m);
        (result.ReturnTriggeredSubtotal + result.OutOfBandSubtotal).ShouldBe(result.TotalCost);
    }

    [Fact]
    public async Task Each_closed_request_is_listed_with_its_origin_in_closure_order()
    {
        var (tool, instances) = await SeedToolWithInstancesAsync("Circular Saw", 2);
        var reporter = await LibrarianMemberIdAsync();
        var loanId = Guid.NewGuid();

        await InsertClosedOutOfBandRequestAsync(instances[1].Id, reporter, Today.AddDays(-15), Today.AddDays(-5), 20m);
        await InsertClosedMaintenanceRequestAsync(instances[0].Id, loanId, Today.AddDays(-20), Today.AddDays(-10), 30m);

        var result = await GetMaintenanceCostAsync(Today.AddDays(-30), Today);

        result.Items.Count.ShouldBe(2);

        var returnTriggered = result.Items[0];
        returnTriggered.Origin.ShouldBe(MaintenanceRequestOrigin.ReturnTriggered);
        returnTriggered.ToolInstanceId.ShouldBe(instances[0].Id);
        returnTriggered.ToolName.ShouldBe(tool.Name);
        returnTriggered.SerialNumber.ShouldBe(instances[0].SerialNumber);
        returnTriggered.TriggeringLoanId.ShouldBe(loanId);
        returnTriggered.Cost.ShouldBe(30m);
        returnTriggered.ReportedByMemberId.ShouldBeNull();
        returnTriggered.ReportReason.ShouldBeNull();
        returnTriggered.ObservedCondition.ShouldBeNull();

        var outOfBand = result.Items[1];
        outOfBand.Origin.ShouldBe(MaintenanceRequestOrigin.OutOfBand);
        outOfBand.TriggeringLoanId.ShouldBeNull();
        outOfBand.ReportedByMemberId.ShouldBe(reporter);
        outOfBand.ReportedByDisplayName.ShouldBe("Librarian Test User");
        outOfBand.ReportReason.ShouldBe("Found cracked on the shelf");
        outOfBand.ObservedCondition.ShouldBe(Catalog.ToolCondition.Worn);
        outOfBand.Cost.ShouldBe(20m);

        result.Items[0].ClosedAt.ShouldBeLessThan(result.Items[1].ClosedAt);
    }

    [Fact]
    public async Task A_range_with_no_closures_has_zero_subtotals_and_no_items()
    {
        var result = await GetMaintenanceCostAsync(Today.AddDays(-3), Today.AddDays(-2));

        result.ReturnTriggeredSubtotal.ShouldBe(0m);
        result.OutOfBandSubtotal.ShouldBe(0m);
        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_request_whose_instance_cannot_be_resolved_is_still_listed()
    {
        // 006 B9: the row stays, with the name left null.
        var unknownInstanceId = Guid.NewGuid();
        await InsertClosedMaintenanceRequestAsync(unknownInstanceId, Guid.NewGuid(), Today.AddDays(-4), Today.AddDays(-2), 15m);

        var result = await GetMaintenanceCostAsync(Today.AddDays(-3), Today);

        var item = result.Items.ShouldHaveSingleItem();
        item.ToolInstanceId.ShouldBe(unknownInstanceId);
        item.ToolName.ShouldBeNull();
        item.SerialNumber.ShouldBeNull();
    }

    private async Task InsertClosedOutOfBandRequestAsync(Guid toolInstanceId, Guid reporterMemberId, DateOnly openedOn, DateOnly closedOn, decimal cost)
    {
        var request = MaintenanceRequest.ReportOutOfBand(
            Guid.NewGuid(),
            toolInstanceId,
            reporterMemberId,
            "Found cracked on the shelf",
            Catalog.ToolCondition.Worn,
            openedOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        request.Close(closedOn.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc), cost);

        await MaintenanceRequestRepository.InsertAsync(request, autoSave: true);
    }

    private async Task InsertOpenOutOfBandRequestAsync(Guid toolInstanceId, Guid reporterMemberId, DateOnly openedOn)
    {
        var request = MaintenanceRequest.ReportOutOfBand(
            Guid.NewGuid(),
            toolInstanceId,
            reporterMemberId,
            "Found cracked on the shelf",
            Catalog.ToolCondition.Worn,
            openedOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        await MaintenanceRequestRepository.InsertAsync(request, autoSave: true);
    }

    private async Task<MaintenanceCostReportDto> GetMaintenanceCostAsync(DateOnly from, DateOnly to)
    {
        using (AsLibrarian())
        {
            return await ReportAppService.GetMaintenanceCostAsync(new MaintenanceCostReportInput { From = from, To = to });
        }
    }
}
