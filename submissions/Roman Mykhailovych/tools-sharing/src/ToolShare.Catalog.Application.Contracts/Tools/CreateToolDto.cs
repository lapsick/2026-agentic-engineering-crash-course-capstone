using System;
using System.ComponentModel.DataAnnotations;

namespace ToolShare.Catalog.Tools;

public class CreateToolDto
{
    [Required]
    [StringLength(CatalogDomainSharedConsts.ToolNameMaxLength, MinimumLength = CatalogDomainSharedConsts.ToolNameMinLength)]
    public string Name { get; set; } = default!;

    [StringLength(CatalogDomainSharedConsts.ToolDescriptionMaxLength)]
    public string? Description { get; set; }

    [Required]
    public Guid CategoryId { get; set; }
}
