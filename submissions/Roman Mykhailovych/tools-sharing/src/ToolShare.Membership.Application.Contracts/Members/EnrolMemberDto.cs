using System.ComponentModel.DataAnnotations;

namespace ToolShare.Membership.Members;

/// <summary>
/// <see cref="Role"/> is optional so an Administrator can enrol a Librarian in
/// one action (SC-001). The member record is still <b>created</b> as
/// <see cref="CommunityRole.Member"/> per FR-003 and then immediately transitioned
/// if a different role is requested, so the standing history shows both the
/// <c>Enrolled</c> and the <c>RoleChanged</c> entry — history never skips a step.
/// </summary>
public class EnrolMemberDto
{
    [Required]
    [StringLength(128, MinimumLength = 2)]
    public string DisplayName { get; set; } = default!;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = default!;

    [Required]
    [StringLength(128, MinimumLength = 6)]
    public string InitialPassword { get; set; } = default!;

    public CommunityRole Role { get; set; } = CommunityRole.Member;
}
