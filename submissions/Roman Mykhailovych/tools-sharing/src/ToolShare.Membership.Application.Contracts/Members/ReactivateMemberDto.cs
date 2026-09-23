using System.ComponentModel.DataAnnotations;

namespace ToolShare.Membership.Members;

public class ReactivateMemberDto
{
    [Required]
    public string ConcurrencyStamp { get; set; } = default!;
}
