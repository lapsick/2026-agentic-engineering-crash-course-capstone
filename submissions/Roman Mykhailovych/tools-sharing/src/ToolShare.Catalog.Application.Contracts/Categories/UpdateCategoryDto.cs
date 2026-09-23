using System.ComponentModel.DataAnnotations;

namespace ToolShare.Catalog.Categories;

public class UpdateCategoryDto
{
    [Required]
    [StringLength(CatalogDomainSharedConsts.CategoryNameMaxLength, MinimumLength = CatalogDomainSharedConsts.CategoryNameMinLength)]
    public string Name { get; set; } = default!;

    [StringLength(CatalogDomainSharedConsts.CategoryDescriptionMaxLength)]
    public string? Description { get; set; }

    [Required]
    public string ConcurrencyStamp { get; set; } = default!;
}
