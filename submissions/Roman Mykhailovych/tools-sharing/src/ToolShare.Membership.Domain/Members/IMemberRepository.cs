using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ToolShare.Membership.Members;

public interface IMemberRepository : IRepository<Member, Guid>
{
    Task<Member?> FindByIdentityUserIdAsync(Guid identityUserId, CancellationToken cancellationToken = default);

    Task<List<Member>> GetPagedListAsync(
        string? filter,
        MembershipStatus? status,
        CommunityRole? role,
        string sorting,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(
        string? filter,
        MembershipStatus? status,
        CommunityRole? role,
        CancellationToken cancellationToken = default);

    /// <summary>Includes the full standing history (HR-03 chronological order).</summary>
    Task<Member?> GetWithHistoryAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Backs the MR-10 last-active-Administrator guard.</summary>
    Task<int> CountActiveAdministratorsAsync(Guid? excludedMemberId = null, CancellationToken cancellationToken = default);

    /// <summary>Backs the HR-05 idempotency pre-check for reported reliability outcomes.</summary>
    Task<bool> HasOutcomeAsync(Guid occurrenceId, ReliabilityOutcomeType outcomeType, CancellationToken cancellationToken = default);
}
