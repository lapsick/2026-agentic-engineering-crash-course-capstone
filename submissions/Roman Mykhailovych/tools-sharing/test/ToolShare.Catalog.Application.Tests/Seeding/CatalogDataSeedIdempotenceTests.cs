using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog.Categories;
using Volo.Abp.Data;
using Xunit;

namespace ToolShare.Catalog.Seeding;

/// <summary>FR-016, SC-006: running the Catalog data seeder twice must yield exactly the five starter categories, with no duplicates.</summary>
public class CatalogDataSeedIdempotenceTests : CatalogApplicationTestBase
{
    private readonly CatalogDataSeedContributor _seedContributor;
    private readonly ICategoryRepository _categoryRepository;

    public CatalogDataSeedIdempotenceTests()
    {
        _seedContributor = GetRequiredService<CatalogDataSeedContributor>();
        _categoryRepository = GetRequiredService<ICategoryRepository>();
    }

    [Fact]
    public async Task Running_the_seeder_twice_yields_exactly_five_categories_with_no_duplicates()
    {
        await _seedContributor.SeedAsync(new DataSeedContext());

        var afterFirstRun = await _categoryRepository.GetListAsync();
        afterFirstRun.Count.ShouldBe(5);

        await _seedContributor.SeedAsync(new DataSeedContext());

        var afterSecondRun = await _categoryRepository.GetListAsync();
        afterSecondRun.Count.ShouldBe(5);
        afterSecondRun.ShouldAllBe(c => afterSecondRun.FindAll(x => x.NormalizedName == c.NormalizedName).Count == 1);
    }
}
