using Volo.Abp.Application.Dtos;

namespace ToolShare.Membership.Members;

public class GetMemberListInput : PagedAndSortedResultRequestDto
{
    /// <summary>Name or email, case-insensitive contains.</summary>
    public string? Filter { get; set; }

    public MembershipStatus? Status { get; set; }

    public CommunityRole? Role { get; set; }
}
