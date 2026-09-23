using System.ComponentModel.DataAnnotations;

namespace ToolShare.Membership.Members;

public class DeactivateMemberDto
{
    [Required]
    [StringLength(512)]
    public string Reason { get; set; } = default!;

    [Required]
    public string ConcurrencyStamp { get; set; } = default!;
}
