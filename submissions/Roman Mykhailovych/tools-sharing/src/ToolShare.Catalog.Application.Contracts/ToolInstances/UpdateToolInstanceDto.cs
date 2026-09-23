using System.ComponentModel.DataAnnotations;

namespace ToolShare.Catalog.ToolInstances;

/// <summary>
/// Condition is deliberately not editable here — use <c>ChangeConditionAsync</c>
/// so a history row is always written (IR-04).
/// </summary>
public class UpdateToolInstanceDto
{
    [Required]
    [StringLength(CatalogDomainSharedConsts.SerialNumberMaxLength, MinimumLength = CatalogDomainSharedConsts.SerialNumberMinLength)]
    public string SerialNumber { get; set; } = default!;

    [StringLength(CatalogDomainSharedConsts.NotesMaxLength)]
    public string? Notes { get; set; }

    [Required]
    public string ConcurrencyStamp { get; set; } = default!;
}
