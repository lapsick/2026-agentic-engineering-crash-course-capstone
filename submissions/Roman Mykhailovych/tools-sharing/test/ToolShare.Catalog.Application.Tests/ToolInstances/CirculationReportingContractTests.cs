using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog.Authorization;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.Tools;
using Volo.Abp;
using Volo.Abp.Authorization;
using Xunit;

namespace ToolShare.Catalog.ToolInstances;

/// <summary>
/// The new inbound Tier 1 contract this feature (004-lending) adds —
/// see specs/004-lending/contracts/catalog-extension.md. Covers every
/// operation/rejection in that document's table plus permission enforcement
/// (research R2/R10).
/// </summary>
public class CirculationReportingContractTests : CatalogAuthorizationTestBase
{
    private readonly IToolInstanceCirculationReportingAppService _circulationReportingAppService;

    public CirculationReportingContractTests()
    {
        _circulationReportingAppService = GetRequiredService<IToolInstanceCirculationReportingAppService>();
    }

    private async Task<Guid> CreateInstanceAsync()
    {
        var category = await CategoryAppService.CreateAsync(new CreateCategoryDto { Name = Guid.NewGuid().ToString() });
        var tool = await ToolAppService.CreateAsync(new CreateToolDto { Name = "Rotary Hammer", CategoryId = category.Id });
        var instance = await ToolInstanceAppService.CreateAsync(new CreateToolInstanceDto
        {
            ToolId = tool.Id,
            SerialNumber = Guid.NewGuid().ToString("N")[..8],
            Condition = ToolCondition.Good
        });
        return instance.Id;
    }

    [Fact]
    public async Task MarkOnLoanAsync_moves_an_available_instance_to_OnLoan()
    {
        var instanceId = await CreateInstanceAsync();

        await _circulationReportingAppService.MarkOnLoanAsync(instanceId);

        var instance = await ToolInstanceAppService.GetAsync(instanceId);
        instance.CirculationState.ShouldBe(ToolInstanceCirculationState.OnLoan);
        instance.IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task MarkOnLoanAsync_is_rejected_when_the_instance_is_already_on_loan()
    {
        var instanceId = await CreateInstanceAsync();
        await _circulationReportingAppService.MarkOnLoanAsync(instanceId);

        var exception = await Should.ThrowAsync<BusinessException>(() => _circulationReportingAppService.MarkOnLoanAsync(instanceId));
        exception.Code.ShouldBe("Catalog:InstanceNotAvailableForLoan");
    }

    [Fact]
    public async Task MarkReturnedAsync_returns_the_instance_to_circulation_with_the_new_condition()
    {
        var instanceId = await CreateInstanceAsync();
        await _circulationReportingAppService.MarkOnLoanAsync(instanceId);

        await _circulationReportingAppService.MarkReturnedAsync(instanceId, ToolCondition.Worn);

        var instance = await ToolInstanceAppService.GetAsync(instanceId);
        instance.CirculationState.ShouldBe(ToolInstanceCirculationState.InCirculation);
        instance.Condition.ShouldBe(ToolCondition.Worn);
        instance.IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task MarkReturnedAsync_is_rejected_when_the_instance_is_not_on_loan()
    {
        var instanceId = await CreateInstanceAsync();

        var exception = await Should.ThrowAsync<BusinessException>(() => _circulationReportingAppService.MarkReturnedAsync(instanceId, ToolCondition.Good));
        exception.Code.ShouldBe("Catalog:InstanceNotOnLoan");
    }

    [Fact]
    public async Task MarkReturnedForMaintenanceAsync_moves_the_instance_to_UnderMaintenance()
    {
        var instanceId = await CreateInstanceAsync();
        await _circulationReportingAppService.MarkOnLoanAsync(instanceId);

        await _circulationReportingAppService.MarkReturnedForMaintenanceAsync(instanceId, ToolCondition.Damaged);

        var instance = await ToolInstanceAppService.GetAsync(instanceId);
        instance.CirculationState.ShouldBe(ToolInstanceCirculationState.UnderMaintenance);
        instance.Condition.ShouldBe(ToolCondition.Damaged);
        instance.IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task MarkMaintenanceClosedAsync_restores_availability()
    {
        var instanceId = await CreateInstanceAsync();
        await _circulationReportingAppService.MarkOnLoanAsync(instanceId);
        await _circulationReportingAppService.MarkReturnedForMaintenanceAsync(instanceId, ToolCondition.Worn);

        await _circulationReportingAppService.MarkMaintenanceClosedAsync(instanceId);

        var instance = await ToolInstanceAppService.GetAsync(instanceId);
        instance.CirculationState.ShouldBe(ToolInstanceCirculationState.InCirculation);
        instance.IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task MarkMaintenanceClosedAsync_is_rejected_when_no_request_is_open()
    {
        var instanceId = await CreateInstanceAsync();

        var exception = await Should.ThrowAsync<BusinessException>(() => _circulationReportingAppService.MarkMaintenanceClosedAsync(instanceId));
        exception.Code.ShouldBe("Catalog:InstanceNotUnderMaintenance");
    }

    [Fact]
    public async Task Reporting_lending_state_requires_the_ReportLendingState_permission()
    {
        var instanceId = await CreateInstanceAsync();

        using (AsAuthenticatedUserWithNoGrants())
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _circulationReportingAppService.MarkOnLoanAsync(instanceId));
        }
    }

    // ---- 008-out-of-band-maintenance: MarkSentToMaintenanceAsync (IR-09) ----

    [Fact]
    public async Task MarkSentToMaintenanceAsync_moves_an_in_circulation_instance_under_maintenance_with_the_observed_condition()
    {
        var instanceId = await CreateInstanceAsync();

        await _circulationReportingAppService.MarkSentToMaintenanceAsync(instanceId, ToolCondition.Worn, "Cracked blade guard");

        var instance = await ToolInstanceAppService.GetAsync(instanceId);
        instance.CirculationState.ShouldBe(ToolInstanceCirculationState.UnderMaintenance);
        instance.Condition.ShouldBe(ToolCondition.Worn);
        instance.IsAvailable.ShouldBeFalse();
        instance.History.ShouldContain(h =>
            h.NewCirculationState == ToolInstanceCirculationState.UnderMaintenance &&
            h.Reason == "Cracked blade guard");
    }

    [Fact]
    public async Task MarkSentToMaintenanceAsync_makes_the_instance_unavailable_to_lookup_consumers()
    {
        var instanceId = await CreateInstanceAsync();
        var lookup = GetRequiredService<IToolInstanceLookupAppService>();

        await _circulationReportingAppService.MarkSentToMaintenanceAsync(instanceId, ToolCondition.Good, "Frayed cord");

        (await lookup.IsAvailableAsync(instanceId)).ShouldBeFalse();
    }

    [Fact]
    public async Task An_instance_sent_to_maintenance_is_restored_by_MarkMaintenanceClosedAsync()
    {
        var instanceId = await CreateInstanceAsync();
        await _circulationReportingAppService.MarkSentToMaintenanceAsync(instanceId, ToolCondition.Worn, "Cracked");

        await _circulationReportingAppService.MarkMaintenanceClosedAsync(instanceId);

        var instance = await ToolInstanceAppService.GetAsync(instanceId);
        instance.CirculationState.ShouldBe(ToolInstanceCirculationState.InCirculation);
        instance.Condition.ShouldBe(ToolCondition.Worn);
    }

    [Fact]
    public async Task MarkSentToMaintenanceAsync_is_rejected_when_the_instance_is_on_loan()
    {
        var instanceId = await CreateInstanceAsync();
        await _circulationReportingAppService.MarkOnLoanAsync(instanceId);

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _circulationReportingAppService.MarkSentToMaintenanceAsync(instanceId, ToolCondition.Good, "Cracked"));
        exception.Code.ShouldBe("Catalog:InstanceNotAvailableForMaintenance");
    }

    [Fact]
    public async Task MarkSentToMaintenanceAsync_is_rejected_for_a_better_observed_condition()
    {
        var instanceId = await CreateInstanceAsync();

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _circulationReportingAppService.MarkSentToMaintenanceAsync(instanceId, ToolCondition.New, "Cracked"));
        exception.Code.ShouldBe("Catalog:ObservedConditionBetterThanCurrent");
    }

    [Fact]
    public async Task MarkSentToMaintenanceAsync_requires_the_ReportLendingState_permission()
    {
        var instanceId = await CreateInstanceAsync();

        using (AsAuthenticatedUserWithNoGrants())
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _circulationReportingAppService.MarkSentToMaintenanceAsync(instanceId, ToolCondition.Good, "Cracked"));
        }
    }

    [Fact]
    public async Task A_Librarian_may_report_lending_state()
    {
        var instanceId = await CreateInstanceAsync();

        using (AsLibrarian())
        {
            await Should.NotThrowAsync(() => _circulationReportingAppService.MarkOnLoanAsync(instanceId));
        }
    }
}
