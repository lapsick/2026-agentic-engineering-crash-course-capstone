using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ToolShare.Membership.CommunityRules;
using ToolShare.Membership.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;

namespace ToolShare.Membership.Members;

/// <summary>
/// Roster administration (US1). Enrolment provisions the identity account and
/// creates the <see cref="Member"/> aggregate in one unit of work (research R2):
/// a forced failure partway through rolls both back, since
/// <c>ToolShareDbContext</c> (identity) and <c>MembershipDbContext</c> share the
/// same connection string and therefore the same ambient database transaction.
/// </summary>
[Authorize]
public class MemberAppService : ApplicationService, IMemberAppService
{
    private readonly IMemberRepository _memberRepository;
    private readonly MemberManager _memberManager;
    private readonly ICommunityRulesRepository _communityRulesRepository;
    private readonly IMemberIdentityProvisioner _memberIdentityProvisioner;

    public MemberAppService(
        IMemberRepository memberRepository,
        MemberManager memberManager,
        ICommunityRulesRepository communityRulesRepository,
        IMemberIdentityProvisioner memberIdentityProvisioner)
    {
        _memberRepository = memberRepository;
        _memberManager = memberManager;
        _communityRulesRepository = communityRulesRepository;
        _memberIdentityProvisioner = memberIdentityProvisioner;
    }

    [Authorize(MembershipPermissions.Members.Default)]
    public virtual async Task<PagedResultDto<MemberListItemDto>> GetListAsync(GetMemberListInput input)
    {
        var normalizedFilter = string.IsNullOrWhiteSpace(input.Filter) ? null : input.Filter.Trim();

        var totalCount = await _memberRepository.GetCountAsync(normalizedFilter, input.Status, input.Role);
        var members = await _memberRepository.GetPagedListAsync(
            normalizedFilter,
            input.Status,
            input.Role,
            input.Sorting ?? string.Empty,
            input.SkipCount,
            input.MaxResultCount);

        return new PagedResultDto<MemberListItemDto>(totalCount, members.Select(MapToListItemDto).ToList());
    }

    [Authorize(MembershipPermissions.Members.Default)]
    public virtual async Task<MemberDetailDto> GetAsync(Guid id)
    {
        var member = await _memberRepository.GetAsync(id);
        return await MapToDetailDtoAsync(member);
    }

    /// <summary>
    /// Ordered <c>ChangedAt</c> ascending, tie-broken by <c>Id</c> (HR-03).
    /// Actor display names are resolved from the roster in one batched lookup
    /// (never N+1) via <see cref="MemberStandingChangeDtoFactory"/>, the same
    /// helper <see cref="MyMembershipAppService"/> uses for a member's own
    /// history.
    /// </summary>
    [Authorize(MembershipPermissions.Members.Default)]
    public virtual async Task<List<MemberStandingChangeDto>> GetStandingHistoryAsync(Guid id)
    {
        var withHistory = await _memberRepository.GetWithHistoryAsync(id)
            ?? throw new EntityNotFoundException(typeof(Member), id);

        var actorDisplayNames = await MemberStandingChangeDtoFactory.ResolveActorDisplayNamesAsync(_memberRepository, withHistory.StandingHistory);

        return MemberStandingChangeDtoFactory.ToOrderedDtos(withHistory.StandingHistory, actorDisplayNames);
    }

    /// <summary>
    /// Provisions the identity account then creates the aggregate (research R2).
    /// The member record is always created as <see cref="CommunityRole.Member"/>
    /// (FR-003) and, if a different role was requested, immediately transitioned —
    /// so the standing history shows both the <c>Enrolled</c> and the
    /// <c>RoleChanged</c> entry (history never skips a step).
    /// </summary>
    [Authorize(MembershipPermissions.Members.Enrol)]
    public virtual async Task<MemberDetailDto> EnrolAsync(EnrolMemberDto input)
    {
        var identityUserId = await _memberIdentityProvisioner.CreateAsync(input.DisplayName, input.Email, input.InitialPassword);

        var member = await _memberManager.CreateAsync(identityUserId, input.DisplayName, input.Email, Clock.Now, CurrentUser.Id);

        if (input.Role != CommunityRole.Member)
        {
            await _memberManager.ChangeRoleAsync(member, input.Role, Clock.Now, CurrentUser.Id);
        }

        await _memberIdentityProvisioner.SetRoleAsync(identityUserId, input.Role.ToString());

        await _memberRepository.InsertAsync(member, autoSave: true);

        return await MapToDetailDtoAsync(member);
    }

    /// <summary>Updates the aggregate and the ABP role set together, guarded by MR-10 (last Administrator).</summary>
    [Authorize(MembershipPermissions.Members.ChangeRole)]
    public virtual async Task<MemberDetailDto> ChangeRoleAsync(Guid id, ChangeMemberRoleDto input)
    {
        var member = await _memberRepository.GetAsync(id);
        member.ConcurrencyStamp = input.ConcurrencyStamp;

        await _memberManager.ChangeRoleAsync(member, input.Role, Clock.Now, CurrentUser.Id);
        await _memberIdentityProvisioner.SetRoleAsync(member.IdentityUserId, input.Role.ToString());

        await _memberRepository.UpdateAsync(member, autoSave: true);

        return await MapToDetailDtoAsync(member);
    }

    /// <summary>Never touches the identity account (Q2 decision). Guarded by MR-10.</summary>
    [Authorize(MembershipPermissions.Members.Deactivate)]
    public virtual async Task<MemberDetailDto> DeactivateAsync(Guid id, DeactivateMemberDto input)
    {
        var member = await _memberRepository.GetAsync(id);
        member.ConcurrencyStamp = input.ConcurrencyStamp;

        await _memberManager.DeactivateAsync(member, input.Reason, Clock.Now, CurrentUser.Id);

        await _memberRepository.UpdateAsync(member, autoSave: true);

        return await MapToDetailDtoAsync(member);
    }

    /// <summary>Leaves <see cref="Member.CurrentRating"/> untouched (FR-005). No MR-10 guard: reactivation only ever adds an active Administrator back.</summary>
    [Authorize(MembershipPermissions.Members.Deactivate)]
    public virtual async Task<MemberDetailDto> ReactivateAsync(Guid id, ReactivateMemberDto input)
    {
        var member = await _memberRepository.GetAsync(id);
        member.ConcurrencyStamp = input.ConcurrencyStamp;

        member.Reactivate(Clock.Now, CurrentUser.Id);

        await _memberRepository.UpdateAsync(member, autoSave: true);

        return await MapToDetailDtoAsync(member);
    }

    /// <summary>
    /// FR-021: the only path that creates a
    /// <see cref="ReliabilityOutcomeType.ManualAdjustment"/> entry. Goes through
    /// the same <see cref="Member.ApplyOutcome"/> every automatic outcome uses
    /// (MR-06/MR-07) — a manual adjustment is not a separate code path, only a
    /// different <see cref="ReliabilityOutcomeType"/> with a caller-supplied
    /// signed point value instead of one derived from the current community rules.
    /// No MR-10 guard: adjusting a rating never changes status or role.
    /// </summary>
    [Authorize(MembershipPermissions.Members.AdjustRating)]
    public virtual async Task<MemberDetailDto> AdjustRatingAsync(Guid id, AdjustMemberRatingDto input)
    {
        var member = await _memberRepository.GetAsync(id);
        member.ConcurrencyStamp = input.ConcurrencyStamp;

        var rules = await _communityRulesRepository.GetCurrentAsync();

        member.ApplyOutcome(
            ReliabilityOutcomeType.ManualAdjustment,
            input.Points,
            occurrenceId: null,
            input.Reason,
            Clock.Now,
            CurrentUser.Id,
            rules);

        await _memberRepository.UpdateAsync(member, autoSave: true);

        return await MapToDetailDtoAsync(member);
    }

    private static MemberListItemDto MapToListItemDto(Member member) => new()
    {
        Id = member.Id,
        DisplayName = member.DisplayName,
        Email = member.Email,
        Status = member.Status,
        Role = member.Role,
        CurrentRating = member.CurrentRating,
        EnrolledAt = member.EnrolledAt
    };

    private async Task<MemberDetailDto> MapToDetailDtoAsync(Member member)
    {
        var rules = await _communityRulesRepository.GetCurrentAsync();

        return new MemberDetailDto
        {
            Id = member.Id,
            DisplayName = member.DisplayName,
            Email = member.Email,
            Status = member.Status,
            Role = member.Role,
            CurrentRating = member.CurrentRating,
            EnrolledAt = member.EnrolledAt,
            IdentityUserId = member.IdentityUserId,
            EffectiveConcurrentLoanLimit = rules is null ? 0 : member.EffectiveConcurrentLoanLimit(rules),
            StatusChangedAt = member.StatusChangedAt,
            StatusChangeReason = member.StatusChangeReason,
            ConcurrencyStamp = member.ConcurrencyStamp
        };
    }
}
