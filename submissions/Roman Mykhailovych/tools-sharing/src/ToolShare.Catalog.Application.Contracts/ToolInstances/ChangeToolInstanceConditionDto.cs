using System.ComponentModel.DataAnnotations;

namespace ToolShare.Catalog.ToolInstances;

public class ChangeToolInstanceConditionDto
{
    [Required]
    public ToolCondition Condition { get; set; }

    [StringLength(CatalogDomainSharedConsts.ConditionChangeReasonMaxLength)]
    public string? Reason { get; set; }

    [Required]
    public string ConcurrencyStamp { get; set; } = default!;
}
