using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ToolShare.Membership.Members;

/// <summary>
/// Shared shaping of <see cref="MemberStandingChange"/> rows into
/// <see cref="MemberStandingChangeDto"/>, used by both <see cref="MemberAppService"/>
/// (US4, an administrator reading someone else's history) and
/// <see cref="MyMembershipAppService"/> (US3, a member reading their own) so the
/// batched actor-display-name resolution (one roster lookup per history page,
/// never N+1) exists exactly once.
/// </summary>
internal static class MemberStandingChangeDtoFactory
{
    /// <summary>
    /// Resolved from the Membership roster in one batched lookup — never from
    /// the identity store — the actor is always a member.
    /// </summary>
    public static async Task<Dictionary<Guid, string>> ResolveActorDisplayNamesAsync(
        IMemberRepository memberRepository,
        IEnumerable<MemberStandingChange> history)
    {
        var actorIdentityUserIds = history
            .Where(change => change.ChangedByUserId.HasValue)
            .Select(change => change.ChangedByUserId!.Value)
            .Distinct()
            .ToList();

        if (actorIdentityUserIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var actors = await memberRepository.GetListAsync(m => actorIdentityUserIds.Contains(m.IdentityUserId));
        return actors.ToDictionary(m => m.IdentityUserId, m => m.DisplayName);
    }

    public static List<MemberStandingChangeDto> ToOrderedDtos(
        IEnumerable<MemberStandingChange> history,
        IReadOnlyDictionary<Guid, string> actorDisplayNames)
    {
        return history
            .OrderBy(change => change.ChangedAt)
            .ThenBy(change => change.Id)
            .Select(change => ToDto(change, actorDisplayNames))
            .ToList();
    }

    private static MemberStandingChangeDto ToDto(MemberStandingChange change, IReadOnlyDictionary<Guid, string> actorDisplayNames)
    {
        return new MemberStandingChangeDto
        {
            Id = change.Id,
            Kind = change.Kind,
            ChangedAt = change.ChangedAt,
            ChangedByUserId = change.ChangedByUserId,
            ChangedByDisplayName = change.ChangedByUserId.HasValue && actorDisplayNames.TryGetValue(change.ChangedByUserId.Value, out var name)
                ? name
                : null,
            Reason = change.Reason,
            PreviousStatus = change.PreviousStatus,
            NewStatus = change.NewStatus,
            PreviousRole = change.PreviousRole,
            NewRole = change.NewRole,
            OutcomeType = change.OutcomeType,
            RawPoints = change.RawPoints,
            EffectivePoints = change.EffectivePoints,
            ResultingRating = change.ResultingRating,
            OccurrenceId = change.OccurrenceId
        };
    }
}
