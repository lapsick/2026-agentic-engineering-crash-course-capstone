using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.ToolInstances;
using Volo.Abp;
using Xunit;

namespace ToolShare.Catalog.Tools;

public class ToolAppServiceTests : CatalogApplicationTestBase
{
    private readonly IToolAppService _toolAppService;
    private readonly ICategoryAppService _categoryAppService;

    public ToolAppServiceTests()
    {
        _toolAppService = GetRequiredService<IToolAppService>();
        _categoryAppService = GetRequiredService<ICategoryAppService>();
    }

    private async Task<CategoryDto> CreateCategoryAsync(string name = "Power Tools")
    {
        return await _categoryAppService.CreateAsync(new CreateCategoryDto { Name = name });
    }

    [Fact]
    public async Task Can_create_and_edit_a_tool()
    {
        var category = await CreateCategoryAsync();

        var created = await _toolAppService.CreateAsync(new CreateToolDto { Name = "Rotary Hammer", CategoryId = category.Id });
        created.Name.ShouldBe("Rotary Hammer");
        created.CategoryName.ShouldBe(category.Name);

        var updated = await _toolAppService.UpdateAsync(created.Id, new UpdateToolDto
        {
            Name = "Rotary Hammer XL",
            CategoryId = category.Id,
            ConcurrencyStamp = created.ConcurrencyStamp
        });
        updated.Name.ShouldBe("Rotary Hammer XL");
    }

    [Fact]
    public async Task Creating_a_tool_with_an_unknown_category_is_rejected()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _toolAppService.CreateAsync(new CreateToolDto { Name = "Drill", CategoryId = Guid.NewGuid() }));

        exception.Code.ShouldBe("Catalog:CategoryNotFound");
    }

    [Fact]
    public async Task Deleting_a_tool_with_instances_is_blocked()
    {
        var category = await CreateCategoryAsync();
        var tool = await _toolAppService.CreateAsync(new CreateToolDto { Name = "Circular Saw", CategoryId = category.Id });

        var instanceAppService = GetRequiredService<IToolInstanceAppService>();
        await instanceAppService.CreateAsync(new CreateToolInstanceDto
        {
            ToolId = tool.Id,
            SerialNumber = "CS-001",
            Condition = ToolCondition.Good
        });

        var exception = await Should.ThrowAsync<BusinessException>(() => _toolAppService.DeleteAsync(tool.Id));
        exception.Code.ShouldBe("Catalog:ToolHasInstances");
    }

    [Fact]
    public async Task Deleting_a_tool_without_instances_succeeds()
    {
        var category = await CreateCategoryAsync();
        var tool = await _toolAppService.CreateAsync(new CreateToolDto { Name = "Wrench Set", CategoryId = category.Id });

        await _toolAppService.DeleteAsync(tool.Id);
    }
}
