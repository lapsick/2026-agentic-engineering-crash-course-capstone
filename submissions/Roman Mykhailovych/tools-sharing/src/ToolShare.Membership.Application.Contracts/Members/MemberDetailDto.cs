using System;

namespace ToolShare.Membership.Members;

public class MemberDetailDto : MemberListItemDto
{
    public Guid IdentityUserId { get; set; }

    public int EffectiveConcurrentLoanLimit { get; set; }

    public DateTime? StatusChangedAt { get; set; }

    public string? StatusChangeReason { get; set; }

    public string ConcurrencyStamp { get; set; } = default!;
}
