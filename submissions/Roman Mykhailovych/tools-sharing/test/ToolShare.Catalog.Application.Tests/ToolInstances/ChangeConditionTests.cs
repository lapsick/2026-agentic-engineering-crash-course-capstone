using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.Tools;
using Volo.Abp;
using Xunit;

namespace ToolShare.Catalog.ToolInstances;

public class ChangeConditionTests : CatalogApplicationTestBase
{
    private readonly IToolInstanceAppService _toolInstanceAppService;
    private readonly IToolAppService _toolAppService;
    private readonly ICategoryAppService _categoryAppService;

    public ChangeConditionTests()
    {
        _toolInstanceAppService = GetRequiredService<IToolInstanceAppService>();
        _toolAppService = GetRequiredService<IToolAppService>();
        _categoryAppService = GetRequiredService<ICategoryAppService>();
    }

    private async Task<ToolInstanceDto> CreateInstanceAsync(ToolCondition condition = ToolCondition.Good)
    {
        var category = await _categoryAppService.CreateAsync(new CreateCategoryDto { Name = Guid.NewGuid().ToString() });
        var tool = await _toolAppService.CreateAsync(new CreateToolDto { Name = "Rotary Hammer", CategoryId = category.Id });
        return await _toolInstanceAppService.CreateAsync(new CreateToolInstanceDto
        {
            ToolId = tool.Id,
            SerialNumber = Guid.NewGuid().ToString("N")[..8],
            Condition = condition
        });
    }

    [Fact]
    public async Task Changing_condition_writes_exactly_one_history_row()
    {
        var instance = await CreateInstanceAsync(ToolCondition.Good);

        var updated = await _toolInstanceAppService.ChangeConditionAsync(instance.Id, new ChangeToolInstanceConditionDto
        {
            Condition = ToolCondition.Worn,
            Reason = "Visible wear",
            ConcurrencyStamp = instance.ConcurrencyStamp
        });

        updated.Condition.ShouldBe(ToolCondition.Worn);

        var history = await _toolInstanceAppService.GetHistoryAsync(instance.Id);
        history.Items.Count.ShouldBe(2); // registration + this change
    }

    [Fact]
    public async Task Changing_to_the_same_condition_is_rejected()
    {
        var instance = await CreateInstanceAsync(ToolCondition.Good);

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _toolInstanceAppService.ChangeConditionAsync(instance.Id, new ChangeToolInstanceConditionDto
            {
                Condition = ToolCondition.Good,
                ConcurrencyStamp = instance.ConcurrencyStamp
            }));

        exception.Code.ShouldBe("Catalog:ConditionUnchanged");
    }

    [Fact]
    public async Task Changing_condition_of_a_retired_instance_is_rejected()
    {
        var instance = await CreateInstanceAsync(ToolCondition.Good);
        var retired = await _toolInstanceAppService.RetireAsync(instance.Id, new RetireToolInstanceDto
        {
            Reason = "No longer needed",
            ConcurrencyStamp = instance.ConcurrencyStamp
        });

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _toolInstanceAppService.ChangeConditionAsync(instance.Id, new ChangeToolInstanceConditionDto
            {
                Condition = ToolCondition.Worn,
                ConcurrencyStamp = retired.ConcurrencyStamp
            }));

        exception.Code.ShouldBe("Catalog:InstanceIsRetired");
    }
}
