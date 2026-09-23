using System;
using System.ComponentModel.DataAnnotations;

namespace ToolShare.Catalog.ToolInstances;

public class CreateToolInstanceDto
{
    [Required]
    public Guid ToolId { get; set; }

    [Required]
    [StringLength(CatalogDomainSharedConsts.SerialNumberMaxLength, MinimumLength = CatalogDomainSharedConsts.SerialNumberMinLength)]
    public string SerialNumber { get; set; } = default!;

    [Required]
    public ToolCondition Condition { get; set; }

    [StringLength(CatalogDomainSharedConsts.NotesMaxLength)]
    public string? Notes { get; set; }
}
