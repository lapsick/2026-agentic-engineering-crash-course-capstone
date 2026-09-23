using System.Threading.Tasks;
using ToolShare.Catalog.Categories;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace ToolShare.Catalog;

/// <summary>
/// Seeds the five starter categories from data-model.md so a fresh install has a
/// usable category tree (FR-016). Idempotent: each category is inserted only if
/// no row with its <see cref="Category.NormalizedName"/> already exists, so
/// re-running <c>ToolShare.DbMigrator</c> never produces duplicates (SC-006).
/// No tools or instances are seeded — the catalog otherwise starts empty so US1
/// is demonstrable end-to-end.
/// </summary>
public class CatalogDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private static readonly (string Name, string Description)[] StarterCategories =
    {
        ("Power Tools", "Electric and battery-powered tools"),
        ("Hand Tools", "Non-powered hand tools"),
        ("Garden", "Garden and yard equipment"),
        ("Measuring", "Measuring and levelling instruments"),
        ("Ladders & Access", "Ladders, steps and access equipment")
    };

    private readonly ICategoryRepository _categoryRepository;
    private readonly CategoryManager _categoryManager;

    public CatalogDataSeedContributor(ICategoryRepository categoryRepository, CategoryManager categoryManager)
    {
        _categoryRepository = categoryRepository;
        _categoryManager = categoryManager;
    }

    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        foreach (var (name, description) in StarterCategories)
        {
            var normalizedName = CatalogTextNormalizer.Normalize(name);
            if (await _categoryRepository.FindByNormalizedNameAsync(normalizedName) is not null)
            {
                continue;
            }

            var category = await _categoryManager.CreateAsync(name, description);
            await _categoryRepository.InsertAsync(category, autoSave: true);
        }
    }
}
