using System;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ToolShare.Membership.Members;

/// <summary>SC-009/HR-05: the same <c>(OccurrenceId, OutcomeType)</c> reported twice applies once; one occurrence may legitimately carry both an overdue and a damage outcome.</summary>
public class ReliabilityIdempotencyTests : MembershipAuthorizationTestBase
{
    private readonly IReliabilityReportingAppService _reliabilityReportingAppService;

    public ReliabilityIdempotencyTests()
    {
        _reliabilityReportingAppService = GetRequiredService<IReliabilityReportingAppService>();
    }

    [Fact]
    public async Task Reporting_the_same_occurrence_and_outcome_type_twice_applies_once()
    {
        var member = await EnrolAsAdminAsync("Idempotency Same Outcome Target");
        var occurrenceId = Guid.NewGuid();

        var first = await _reliabilityReportingAppService.ReportAsync(new ReportReliabilityOutcomeDto
        {
            MemberId = member.Id,
            OutcomeType = ReliabilityOutcomeType.OverdueReturn,
            OccurrenceId = occurrenceId
        });

        first.Applied.ShouldBeTrue();
        first.AlreadyRecorded.ShouldBeFalse();

        var second = await _reliabilityReportingAppService.ReportAsync(new ReportReliabilityOutcomeDto
        {
            MemberId = member.Id,
            OutcomeType = ReliabilityOutcomeType.OverdueReturn,
            OccurrenceId = occurrenceId
        });

        second.Applied.ShouldBeFalse();
        second.AlreadyRecorded.ShouldBeTrue();
        second.ResultingRating.ShouldBe(first.ResultingRating);
    }

    [Fact]
    public async Task One_occurrence_reporting_both_an_overdue_and_a_damage_outcome_applies_both()
    {
        var member = await EnrolAsAdminAsync("Idempotency Composite Key Target");
        var occurrenceId = Guid.NewGuid();

        var overdue = await _reliabilityReportingAppService.ReportAsync(new ReportReliabilityOutcomeDto
        {
            MemberId = member.Id,
            OutcomeType = ReliabilityOutcomeType.OverdueReturn,
            OccurrenceId = occurrenceId
        });

        var damaged = await _reliabilityReportingAppService.ReportAsync(new ReportReliabilityOutcomeDto
        {
            MemberId = member.Id,
            OutcomeType = ReliabilityOutcomeType.DamagedReturn,
            OccurrenceId = occurrenceId
        });

        overdue.Applied.ShouldBeTrue();
        damaged.Applied.ShouldBeTrue();
        damaged.ResultingRating.ShouldBe(overdue.ResultingRating - CommunityRulesConsts.DefaultDamagePenaltyPoints);
    }
}
