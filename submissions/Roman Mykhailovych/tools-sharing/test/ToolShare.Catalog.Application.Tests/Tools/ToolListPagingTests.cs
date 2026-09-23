using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog.Categories;
using Volo.Abp.Validation;
using Xunit;

namespace ToolShare.Catalog.Tools;

public class ToolListPagingTests : CatalogApplicationTestBase
{
    private readonly IToolAppService _toolAppService;
    private readonly ICategoryAppService _categoryAppService;

    public ToolListPagingTests()
    {
        _toolAppService = GetRequiredService<IToolAppService>();
        _categoryAppService = GetRequiredService<ICategoryAppService>();
    }

    private async Task<CategoryDto> CreateCategoryAsync()
    {
        return await _categoryAppService.CreateAsync(new CreateCategoryDto { Name = Guid.NewGuid().ToString() });
    }

    [Fact]
    public async Task Default_page_size_is_ten()
    {
        var category = await CreateCategoryAsync();
        for (var i = 0; i < 15; i++)
        {
            await _toolAppService.CreateAsync(new CreateToolDto { Name = $"Tool {i:00}", CategoryId = category.Id });
        }

        var result = await _toolAppService.GetListAsync(new GetToolListInput());

        result.TotalCount.ShouldBe(15);
        result.Items.Count.ShouldBe(10);
    }

    [Fact]
    public async Task Requesting_more_than_the_hard_cap_is_rejected()
    {
        await Should.ThrowAsync<AbpValidationException>(() =>
            _toolAppService.GetListAsync(new GetToolListInput { MaxResultCount = 101 }));
    }

    [Fact]
    public async Task The_hard_cap_itself_is_accepted()
    {
        var result = await _toolAppService.GetListAsync(new GetToolListInput { MaxResultCount = 100 });

        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task An_unrecognized_sort_field_falls_back_to_the_default()
    {
        var category = await CreateCategoryAsync();
        await _toolAppService.CreateAsync(new CreateToolDto { Name = "Zebra Saw", CategoryId = category.Id });
        await _toolAppService.CreateAsync(new CreateToolDto { Name = "Anvil", CategoryId = category.Id });

        var result = await _toolAppService.GetListAsync(new GetToolListInput { Sorting = "NotARealField" });

        result.TotalCount.ShouldBe(2);
        result.Items[0].Name.ShouldBe("Anvil");
        result.Items[1].Name.ShouldBe("Zebra Saw");
    }
}
