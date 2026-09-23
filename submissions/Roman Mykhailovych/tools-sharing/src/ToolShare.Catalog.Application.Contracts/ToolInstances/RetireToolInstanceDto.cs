using System.ComponentModel.DataAnnotations;

namespace ToolShare.Catalog.ToolInstances;

public class RetireToolInstanceDto
{
    [Required]
    [StringLength(CatalogDomainSharedConsts.RetirementReasonMaxLength, MinimumLength = 1)]
    public string Reason { get; set; } = default!;

    [Required]
    public string ConcurrencyStamp { get; set; } = default!;
}
