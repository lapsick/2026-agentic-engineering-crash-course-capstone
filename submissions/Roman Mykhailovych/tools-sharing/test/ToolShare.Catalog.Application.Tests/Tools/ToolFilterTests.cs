using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.ToolInstances;
using Xunit;

namespace ToolShare.Catalog.Tools;

public class ToolFilterTests : CatalogApplicationTestBase
{
    private readonly IToolAppService _toolAppService;
    private readonly ICategoryAppService _categoryAppService;
    private readonly IToolInstanceAppService _toolInstanceAppService;

    public ToolFilterTests()
    {
        _toolAppService = GetRequiredService<IToolAppService>();
        _categoryAppService = GetRequiredService<ICategoryAppService>();
        _toolInstanceAppService = GetRequiredService<IToolInstanceAppService>();
    }

    private async Task<CategoryDto> CreateCategoryAsync()
    {
        return await _categoryAppService.CreateAsync(new CreateCategoryDto { Name = Guid.NewGuid().ToString() });
    }

    [Fact]
    public async Task CategoryId_restricts_results_to_one_category()
    {
        var categoryA = await CreateCategoryAsync();
        var categoryB = await CreateCategoryAsync();
        await _toolAppService.CreateAsync(new CreateToolDto { Name = "Tool A", CategoryId = categoryA.Id });
        await _toolAppService.CreateAsync(new CreateToolDto { Name = "Tool B", CategoryId = categoryB.Id });

        var result = await _toolAppService.GetListAsync(new GetToolListInput { CategoryId = categoryA.Id });

        result.TotalCount.ShouldBe(1);
        result.Items.Single().Name.ShouldBe("Tool A");
    }

    [Fact]
    public async Task OnlyAvailable_excludes_tools_with_no_available_instance()
    {
        var category = await CreateCategoryAsync();
        var toolWithAvailableInstance = await _toolAppService.CreateAsync(new CreateToolDto { Name = "Has Available", CategoryId = category.Id });
        var toolWithoutAvailableInstance = await _toolAppService.CreateAsync(new CreateToolDto { Name = "Damaged Only", CategoryId = category.Id });
        var toolWithNoInstances = await _toolAppService.CreateAsync(new CreateToolDto { Name = "No Instances", CategoryId = category.Id });

        await _toolInstanceAppService.CreateAsync(new CreateToolInstanceDto
        {
            ToolId = toolWithAvailableInstance.Id,
            SerialNumber = "SN-A1",
            Condition = ToolCondition.Good
        });
        await _toolInstanceAppService.CreateAsync(new CreateToolInstanceDto
        {
            ToolId = toolWithoutAvailableInstance.Id,
            SerialNumber = "SN-B1",
            Condition = ToolCondition.Damaged
        });

        var result = await _toolAppService.GetListAsync(new GetToolListInput { OnlyAvailable = true });

        result.TotalCount.ShouldBe(1);
        result.Items.Single().Id.ShouldBe(toolWithAvailableInstance.Id);
    }

    [Fact]
    public async Task OnlyAvailable_treats_a_retired_instance_as_unavailable()
    {
        var category = await CreateCategoryAsync();
        var tool = await _toolAppService.CreateAsync(new CreateToolDto { Name = "Retired Only", CategoryId = category.Id });
        var instance = await _toolInstanceAppService.CreateAsync(new CreateToolInstanceDto
        {
            ToolId = tool.Id,
            SerialNumber = "SN-R1",
            Condition = ToolCondition.Good
        });

        await _toolInstanceAppService.RetireAsync(instance.Id, new RetireToolInstanceDto
        {
            Reason = "Worn out",
            ConcurrencyStamp = instance.ConcurrencyStamp
        });

        var result = await _toolAppService.GetListAsync(new GetToolListInput { OnlyAvailable = true });

        result.TotalCount.ShouldBe(0);
    }
}
