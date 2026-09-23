using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ToolShare.Membership.CommunityRules;
using ToolShare.Membership.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Uow;

namespace ToolShare.Membership.Members;

/// <summary>
/// The inbound reliability-outcome reporting contract (US5, Tier 1, FR-027).
/// Idempotent pre-check on <c>(OccurrenceId, OutcomeType)</c> against the
/// filtered unique index (HR-05) — the index is the authority under
/// concurrency, this is only the friendly answer. Concurrent reports for the
/// same member are serialized by the database's optimistic concurrency check
/// on <see cref="Member.ConcurrencyStamp"/> and retried internally up to three
/// times, each attempt in its **own** unit of work (research R7): retrying
/// inside a failed unit of work would reuse a poisoned <c>DbContext</c>, so
/// every attempt re-reads the member from a fresh one.
/// </summary>
[Authorize]
public class ReliabilityReportingAppService : ApplicationService, IReliabilityReportingAppService
{
    private const int MaxAttempts = 3;

    private readonly IMemberRepository _memberRepository;
    private readonly ICommunityRulesRepository _communityRulesRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public ReliabilityReportingAppService(
        IMemberRepository memberRepository,
        ICommunityRulesRepository communityRulesRepository,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _memberRepository = memberRepository;
        _communityRulesRepository = communityRulesRepository;
        _unitOfWorkManager = unitOfWorkManager;
    }

    [Authorize(MembershipPermissions.Reliability.Report)]
    public virtual async Task<ReliabilityReportResultDto> ReportAsync(ReportReliabilityOutcomeDto input)
    {
        if (input.OutcomeType == ReliabilityOutcomeType.ManualAdjustment)
        {
            throw new BusinessException(MembershipDomainErrorCodes.ManualAdjustmentNotReportable);
        }

        for (var attempt = 1; attempt < MaxAttempts; attempt++)
        {
            try
            {
                return await ReportInOwnUnitOfWorkAsync(input);
            }
            catch (AbpDbConcurrencyException)
            {
                // Another concurrent report for the same member won the race;
                // re-read and reapply against a fresh unit of work rather than
                // surfacing contention to the caller (FR-032, SC-009a).
            }
        }

        return await ReportInOwnUnitOfWorkAsync(input);
    }

    private async Task<ReliabilityReportResultDto> ReportInOwnUnitOfWorkAsync(ReportReliabilityOutcomeDto input)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);

        var member = await _memberRepository.FindAsync(input.MemberId)
            ?? throw new BusinessException(MembershipDomainErrorCodes.NotAnEnrolledMember);

        if (await _memberRepository.HasOutcomeAsync(input.OccurrenceId, input.OutcomeType))
        {
            await uow.CompleteAsync();
            return new ReliabilityReportResultDto
            {
                Applied = false,
                AlreadyRecorded = true,
                ResultingRating = member.CurrentRating
            };
        }

        var rules = await _communityRulesRepository.GetCurrentAsync()
            ?? throw new AbpException("The community rules row is missing; the data seeder has not run.");

        var rawPoints = ReliabilityPolicy.GetRawPoints(input.OutcomeType, rules);
        var previousRating = member.CurrentRating;

        member.ApplyOutcome(input.OutcomeType, rawPoints, input.OccurrenceId, input.Note, Clock.Now, CurrentUser.Id, rules);

        await _memberRepository.UpdateAsync(member, autoSave: true);

        await uow.CompleteAsync();

        return new ReliabilityReportResultDto
        {
            Applied = true,
            AlreadyRecorded = false,
            RawPoints = rawPoints,
            EffectivePoints = member.CurrentRating - previousRating,
            ResultingRating = member.CurrentRating
        };
    }
}
