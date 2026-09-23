using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ToolShare.Catalog.Categories;

public class CategoryManager : DomainService
{
    private readonly ICategoryRepository _categoryRepository;

    public CategoryManager(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    /// <summary>Enforces CR-01–CR-03.</summary>
    public async Task<Category> CreateAsync(string name, string? description = null)
    {
        var category = new Category(GuidGenerator.Create(), name, description);

        await ValidateNameUniquenessAsync(category.NormalizedName, excludedId: null);

        return category;
    }

    /// <summary>Re-checks CR-03 excluding the category itself.</summary>
    public async Task ChangeNameAsync(Category category, string newName)
    {
        var previousNormalizedName = category.NormalizedName;
        category.SetName(newName);

        if (category.NormalizedName == previousNormalizedName)
        {
            return;
        }

        await ValidateNameUniquenessAsync(category.NormalizedName, category.Id);
    }

    /// <summary>Enforces CR-04 — a category cannot be deleted while any tool references it.</summary>
    public async Task DeleteAsync(Category category)
    {
        if (await _categoryRepository.AnyToolAssignedAsync(category.Id))
        {
            throw new BusinessException("Catalog:CategoryHasTools");
        }

        await _categoryRepository.DeleteAsync(category);
    }

    private async Task ValidateNameUniquenessAsync(string normalizedName, Guid? excludedId)
    {
        var existing = await _categoryRepository.FindByNormalizedNameAsync(normalizedName, excludedId);
        if (existing is not null)
        {
            throw new BusinessException("Catalog:CategoryNameAlreadyExists").WithData("name", normalizedName);
        }
    }
}
