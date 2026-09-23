using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog.Categories;
using Xunit;

namespace ToolShare.Catalog.Tools;

public class ToolSearchTests : CatalogApplicationTestBase
{
    private readonly IToolAppService _toolAppService;
    private readonly ICategoryAppService _categoryAppService;

    public ToolSearchTests()
    {
        _toolAppService = GetRequiredService<IToolAppService>();
        _categoryAppService = GetRequiredService<ICategoryAppService>();
    }

    private async Task<CategoryDto> CreateCategoryAsync()
    {
        return await _categoryAppService.CreateAsync(new CreateCategoryDto { Name = System.Guid.NewGuid().ToString() });
    }

    [Fact]
    public async Task Filter_matches_case_insensitively_on_a_partial_name()
    {
        var category = await CreateCategoryAsync();
        await _toolAppService.CreateAsync(new CreateToolDto { Name = "Rotary Hammer", CategoryId = category.Id });
        await _toolAppService.CreateAsync(new CreateToolDto { Name = "Impact Driver", CategoryId = category.Id });

        var result = await _toolAppService.GetListAsync(new GetToolListInput { Filter = "hammer" });

        result.TotalCount.ShouldBe(1);
        result.Items.Single().Name.ShouldBe("Rotary Hammer");
    }

    [Fact]
    public async Task Filter_matches_partial_names_anywhere_in_the_string()
    {
        var category = await CreateCategoryAsync();
        await _toolAppService.CreateAsync(new CreateToolDto { Name = "Cordless Drill", CategoryId = category.Id });

        var result = await _toolAppService.GetListAsync(new GetToolListInput { Filter = "DRI" });

        result.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task Filter_is_case_insensitive_for_ukrainian_names()
    {
        var category = await CreateCategoryAsync();
        // Ukrainian for "drill".
        await _toolAppService.CreateAsync(new CreateToolDto { Name = "Дриль", CategoryId = category.Id });

        var mixedCaseResult = await _toolAppService.GetListAsync(new GetToolListInput { Filter = "дриль" });
        mixedCaseResult.TotalCount.ShouldBe(1);

        var upperCaseResult = await _toolAppService.GetListAsync(new GetToolListInput { Filter = "ДРИЛЬ" });
        upperCaseResult.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task Filter_is_accent_tolerant()
    {
        var category = await CreateCategoryAsync();

        // "Cafe Bar Clamp" spelled with a combining acute accent on the "e"
        // (built from code points so the source file has no literal accented glyph).
        var accentedName = "Cafe" + char.ConvertFromUtf32(0x0301) + " Bar Clamp";
        await _toolAppService.CreateAsync(new CreateToolDto { Name = accentedName, CategoryId = category.Id });

        var result = await _toolAppService.GetListAsync(new GetToolListInput { Filter = "cafe bar" });

        result.TotalCount.ShouldBe(1);
    }
}
