using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ToolShare.Membership.Members;

/// <summary>
/// Roster administration (US1) plus manual standing corrections and history
/// (US4). Tier 2 — module-internal, consumed only by
/// <c>ToolShare.Membership.Blazor</c>. Permission requirements are documented in
/// contracts/membership-permissions.md, not repeated here.
/// </summary>
public interface IMemberAppService : IApplicationService
{
    Task<PagedResultDto<MemberListItemDto>> GetListAsync(GetMemberListInput input);

    Task<MemberDetailDto> GetAsync(Guid id);

    /// <summary>Ordered <c>ChangedAt</c> ascending, tie-broken by <c>Id</c> (HR-03).</summary>
    Task<List<MemberStandingChangeDto>> GetStandingHistoryAsync(Guid id);

    Task<MemberDetailDto> EnrolAsync(EnrolMemberDto input);

    Task<MemberDetailDto> ChangeRoleAsync(Guid id, ChangeMemberRoleDto input);

    Task<MemberDetailDto> DeactivateAsync(Guid id, DeactivateMemberDto input);

    Task<MemberDetailDto> ReactivateAsync(Guid id, ReactivateMemberDto input);

    /// <summary>
    /// The only path that creates a <see cref="ReliabilityOutcomeType.ManualAdjustment"/>
    /// entry (FR-021); <see cref="AdjustMemberRatingDto.Reason"/> is mandatory.
    /// Clamped exactly like automatic outcomes (MR-06).
    /// </summary>
    Task<MemberDetailDto> AdjustRatingAsync(Guid id, AdjustMemberRatingDto input);
}
