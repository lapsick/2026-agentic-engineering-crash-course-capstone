using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.ToolInstances;
using ToolShare.Catalog.Tools;
using Volo.Abp.Data;
using Xunit;

namespace ToolShare.Catalog.Concurrency;

public class ConcurrentEditTests : CatalogApplicationTestBase
{
    private readonly ICategoryAppService _categoryAppService;
    private readonly IToolAppService _toolAppService;
    private readonly IToolInstanceAppService _toolInstanceAppService;

    public ConcurrentEditTests()
    {
        _categoryAppService = GetRequiredService<ICategoryAppService>();
        _toolAppService = GetRequiredService<IToolAppService>();
        _toolInstanceAppService = GetRequiredService<IToolInstanceAppService>();
    }

    [Fact]
    public async Task Updating_a_category_with_a_stale_concurrency_stamp_is_rejected()
    {
        var category = await _categoryAppService.CreateAsync(new CreateCategoryDto { Name = "Original" });

        // First save succeeds and changes the concurrency stamp server-side.
        await _categoryAppService.UpdateAsync(category.Id, new UpdateCategoryDto
        {
            Name = "First Edit",
            ConcurrencyStamp = category.ConcurrencyStamp
        });

        // Second save reuses the now-stale stamp from the original read.
        await Should.ThrowAsync<AbpDbConcurrencyException>(() =>
            _categoryAppService.UpdateAsync(category.Id, new UpdateCategoryDto
            {
                Name = "Second Edit",
                ConcurrencyStamp = category.ConcurrencyStamp
            }));
    }

    [Fact]
    public async Task Updating_a_tool_instance_with_a_stale_concurrency_stamp_is_rejected()
    {
        var category = await _categoryAppService.CreateAsync(new CreateCategoryDto { Name = Guid.NewGuid().ToString() });
        var tool = await _toolAppService.CreateAsync(new CreateToolDto { Name = "Rotary Hammer", CategoryId = category.Id });
        var instance = await _toolInstanceAppService.CreateAsync(new CreateToolInstanceDto
        {
            ToolId = tool.Id,
            SerialNumber = Guid.NewGuid().ToString("N")[..8],
            Condition = ToolCondition.Good
        });

        // First save succeeds and changes the concurrency stamp server-side.
        await _toolInstanceAppService.ChangeConditionAsync(instance.Id, new ChangeToolInstanceConditionDto
        {
            Condition = ToolCondition.Worn,
            ConcurrencyStamp = instance.ConcurrencyStamp
        });

        // Second save reuses the now-stale stamp from the original read.
        await Should.ThrowAsync<AbpDbConcurrencyException>(() =>
            _toolInstanceAppService.ChangeConditionAsync(instance.Id, new ChangeToolInstanceConditionDto
            {
                Condition = ToolCondition.Damaged,
                ConcurrencyStamp = instance.ConcurrencyStamp
            }));
    }
}
