using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog.Tools;
using Volo.Abp;
using Volo.Abp.Validation;
using Xunit;

namespace ToolShare.Catalog.Categories;

public class CategoryAppServiceTests : CatalogApplicationTestBase
{
    private readonly ICategoryAppService _categoryAppService;

    public CategoryAppServiceTests()
    {
        _categoryAppService = GetRequiredService<ICategoryAppService>();
    }

    [Fact]
    public async Task Can_create_edit_and_list_categories()
    {
        var created = await _categoryAppService.CreateAsync(new CreateCategoryDto { Name = "Power Tools", Description = "Electric tools" });
        created.Name.ShouldBe("Power Tools");
        created.ToolCount.ShouldBe(0);

        var updated = await _categoryAppService.UpdateAsync(created.Id, new UpdateCategoryDto
        {
            Name = "Power Tools Updated",
            Description = "Updated",
            ConcurrencyStamp = created.ConcurrencyStamp
        });
        updated.Name.ShouldBe("Power Tools Updated");

        var list = await _categoryAppService.GetListAsync(new GetCategoryListInput { Filter = "Updated" });
        list.TotalCount.ShouldBe(1);
        list.Items.ShouldContain(c => c.Id == created.Id);
    }

    [Fact]
    public async Task Creating_a_duplicate_category_name_is_rejected()
    {
        await _categoryAppService.CreateAsync(new CreateCategoryDto { Name = "Hand Tools" });

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _categoryAppService.CreateAsync(new CreateCategoryDto { Name = "hand tools" }));

        exception.Code.ShouldBe("Catalog:CategoryNameAlreadyExists");
    }

    [Fact]
    public async Task Deleting_a_category_with_tools_assigned_is_blocked()
    {
        var category = await _categoryAppService.CreateAsync(new CreateCategoryDto { Name = "Garden" });

        var toolAppService = GetRequiredService<IToolAppService>();
        await toolAppService.CreateAsync(new CreateToolDto { Name = "Hedge Trimmer", CategoryId = category.Id });

        var exception = await Should.ThrowAsync<BusinessException>(() => _categoryAppService.DeleteAsync(category.Id));
        exception.Code.ShouldBe("Catalog:CategoryHasTools");
    }

    [Fact]
    public async Task Deleting_an_empty_category_succeeds()
    {
        var category = await _categoryAppService.CreateAsync(new CreateCategoryDto { Name = "Measuring" });

        await _categoryAppService.DeleteAsync(category.Id);

        var list = await _categoryAppService.GetListAsync(new GetCategoryListInput { Filter = "Measuring" });
        list.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Creating_a_category_without_a_name_is_rejected()
    {
        await Should.ThrowAsync<AbpValidationException>(() =>
            _categoryAppService.CreateAsync(new CreateCategoryDto { Name = "" }));
    }
}
