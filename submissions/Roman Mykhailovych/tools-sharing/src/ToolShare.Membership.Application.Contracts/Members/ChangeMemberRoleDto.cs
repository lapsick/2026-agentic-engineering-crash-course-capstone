using System.ComponentModel.DataAnnotations;

namespace ToolShare.Membership.Members;

public class ChangeMemberRoleDto
{
    public CommunityRole Role { get; set; }

    [Required]
    public string ConcurrencyStamp { get; set; } = default!;
}
