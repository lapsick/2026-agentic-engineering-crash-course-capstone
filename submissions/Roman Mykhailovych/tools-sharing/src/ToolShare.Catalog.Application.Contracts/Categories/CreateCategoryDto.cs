using System.ComponentModel.DataAnnotations;

namespace ToolShare.Catalog.Categories;

public class CreateCategoryDto
{
    [Required]
    [StringLength(CatalogDomainSharedConsts.CategoryNameMaxLength, MinimumLength = CatalogDomainSharedConsts.CategoryNameMinLength)]
    public string Name { get; set; } = default!;

    [StringLength(CatalogDomainSharedConsts.CategoryDescriptionMaxLength)]
    public string? Description { get; set; }
}
