using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ToolShare.Membership.Members;

/// <summary>FR-027: the inbound reliability-outcome reporting contract.</summary>
public class ReliabilityReportingTests : MembershipAuthorizationTestBase
{
    private readonly IReliabilityReportingAppService _reliabilityReportingAppService;

    public ReliabilityReportingTests()
    {
        _reliabilityReportingAppService = GetRequiredService<IReliabilityReportingAppService>();
    }

    [Fact]
    public async Task An_outcome_is_applied_for_an_active_member()
    {
        var member = await EnrolAsAdminAsync("Reliability Active Target");

        var result = await _reliabilityReportingAppService.ReportAsync(new ReportReliabilityOutcomeDto
        {
            MemberId = member.Id,
            OutcomeType = ReliabilityOutcomeType.OverdueReturn,
            OccurrenceId = Guid.NewGuid()
        });

        result.Applied.ShouldBeTrue();
        result.AlreadyRecorded.ShouldBeFalse();
        result.RawPoints.ShouldBe(-CommunityRulesConsts.DefaultOverduePenaltyPoints);
        result.EffectivePoints.ShouldBe(-CommunityRulesConsts.DefaultOverduePenaltyPoints);
        result.ResultingRating.ShouldBe(100 - CommunityRulesConsts.DefaultOverduePenaltyPoints);
    }

    [Fact]
    public async Task An_outcome_is_applied_for_a_deactivated_member()
    {
        var member = await EnrolAsAdminAsync("Reliability Deactivated Target");
        var adminPrincipal = await BuildAdminPrincipalAsync();

        using (Impersonate(adminPrincipal))
        {
            await MemberAppService.DeactivateAsync(member.Id, new DeactivateMemberDto
            {
                Reason = "Left while still holding a tool",
                ConcurrencyStamp = member.ConcurrencyStamp
            });
        }

        var result = await _reliabilityReportingAppService.ReportAsync(new ReportReliabilityOutcomeDto
        {
            MemberId = member.Id,
            OutcomeType = ReliabilityOutcomeType.DamagedReturn,
            OccurrenceId = Guid.NewGuid()
        });

        result.Applied.ShouldBeTrue();
        result.ResultingRating.ShouldBe(100 - CommunityRulesConsts.DefaultDamagePenaltyPoints);
    }

    [Fact]
    public async Task Reporting_for_an_unknown_member_throws_NotAnEnrolledMember()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() => _reliabilityReportingAppService.ReportAsync(new ReportReliabilityOutcomeDto
        {
            MemberId = Guid.NewGuid(),
            OutcomeType = ReliabilityOutcomeType.OverdueReturn,
            OccurrenceId = Guid.NewGuid()
        }));

        exception.Code.ShouldBe(MembershipDomainErrorCodes.NotAnEnrolledMember);
    }

    [Fact]
    public async Task Reporting_a_manual_adjustment_throws_ManualAdjustmentNotReportable()
    {
        var member = await EnrolAsAdminAsync("Reliability Manual Adjustment Target");

        var exception = await Should.ThrowAsync<BusinessException>(() => _reliabilityReportingAppService.ReportAsync(new ReportReliabilityOutcomeDto
        {
            MemberId = member.Id,
            OutcomeType = ReliabilityOutcomeType.ManualAdjustment,
            OccurrenceId = Guid.NewGuid()
        }));

        exception.Code.ShouldBe(MembershipDomainErrorCodes.ManualAdjustmentNotReportable);
    }
}
