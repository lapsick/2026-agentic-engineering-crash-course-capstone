using System;
using Volo.Abp.Application.Dtos;

namespace ToolShare.Membership.Members;

public class MemberListItemDto : EntityDto<Guid>
{
    public string DisplayName { get; set; } = default!;

    public string Email { get; set; } = default!;

    public MembershipStatus Status { get; set; }

    public CommunityRole Role { get; set; }

    public int CurrentRating { get; set; }

    public DateTime EnrolledAt { get; set; }
}
