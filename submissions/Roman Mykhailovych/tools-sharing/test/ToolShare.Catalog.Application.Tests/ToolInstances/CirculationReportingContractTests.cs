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
