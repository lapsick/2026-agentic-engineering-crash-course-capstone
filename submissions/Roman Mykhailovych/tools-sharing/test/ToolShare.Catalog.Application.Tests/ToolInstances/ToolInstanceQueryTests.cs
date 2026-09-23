using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.Tools;
using Xunit;

namespace ToolShare.Catalog.ToolInstances;

public class ToolInstanceQueryTests : CatalogApplicationTestBase
{
    private readonly IToolInstanceAppService _toolInstanceAppService;
    private readonly IToolAppService _toolAppService;
    private readonly ICategoryAppService _categoryAppService;

    public ToolInstanceQueryTests()
    {
        _toolInstanceAppService = GetRequiredService<IToolInstanceAppService>();
        _toolAppService = GetRequiredService<IToolAppService>();
        _categoryAppService = GetRequiredService<ICategoryAppService>();
    }

    [Fact]
    public async Task GetAsync_returns_detail_with_tool_category_photos_and_history()
    {
        var category = await _categoryAppService.CreateAsync(new CreateCategoryDto { Name = Guid.NewGuid().ToString() });
        var tool = await _toolAppService.CreateAsync(new CreateToolDto { Name = "Rotary Hammer", CategoryId = category.Id });
        var instance = await _toolInstanceAppService.CreateAsync(new CreateToolInstanceDto
        {
            ToolId = tool.Id,
            SerialNumber = Guid.NewGuid().ToString("N")[..8],
            Condition = ToolCondition.Good
        });

        await _toolInstanceAppService.ChangeConditionAsync(instance.Id, new ChangeToolInstanceConditionDto
        {
            Condition = ToolCondition.Worn,
            ConcurrencyStamp = instance.ConcurrencyStamp
        });

        var detail = await _toolInstanceAppService.GetAsync(instance.Id);

        detail.ToolName.ShouldBe(tool.Name);
        detail.CategoryId.ShouldBe(category.Id);
        detail.CategoryName.ShouldBe(category.Name);
        detail.History.Count.ShouldBe(2);
        detail.Photos.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetListByToolAsync_respects_includeRetired()
    {
        var category = await _categoryAppService.CreateAsync(new CreateCategoryDto { Name = Guid.NewGuid().ToString() });
        var tool = await _toolAppService.CreateAsync(new CreateToolDto { Name = "Impact Driver", CategoryId = category.Id });

        var active = await _toolInstanceAppService.CreateAsync(new CreateToolInstanceDto
        {
            ToolId = tool.Id,
            SerialNumber = Guid.NewGuid().ToString("N")[..8],
            Condition = ToolCondition.Good
        });
        var retired = await _toolInstanceAppService.CreateAsync(new CreateToolInstanceDto
        {
            ToolId = tool.Id,
            SerialNumber = Guid.NewGuid().ToString("N")[..8],
            Condition = ToolCondition.Good
        });
        await _toolInstanceAppService.RetireAsync(retired.Id, new RetireToolInstanceDto
        {
            Reason = "No longer needed",
            ConcurrencyStamp = retired.ConcurrencyStamp
        });

        var defaultList = await _toolInstanceAppService.GetListByToolAsync(tool.Id);
        defaultList.Items.Count.ShouldBe(1);
        defaultList.Items.ShouldContain(i => i.Id == active.Id);

        var fullList = await _toolInstanceAppService.GetListByToolAsync(tool.Id, includeRetired: true);
        fullList.Items.Count.ShouldBe(2);
    }
}
