using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ToolShare.Membership.Members;

/// <summary>
/// A probe handler (<see cref="MemberStandingChangedEventCapture"/>, registered
/// conventionally by <c>OneEntryOneEventTests</c>' <c>MemberStandingChangedProbeHandler</c>)
/// receives every <see cref="MemberStandingChangedEto"/>, including ones raised
/// through the public reporting contract; <c>CrossedLowRatingThreshold</c> is
/// true exactly when a rating moves across the threshold, in either direction.
/// </summary>
public class MemberStandingChangedEventTests : MembershipAuthorizationTestBase
{
    private readonly IReliabilityReportingAppService _reliabilityReportingAppService;
    private readonly MemberStandingChangedEventCapture _capture;

    public MemberStandingChangedEventTests()
    {
        _reliabilityReportingAppService = GetRequiredService<IReliabilityReportingAppService>();
        _capture = GetRequiredService<MemberStandingChangedEventCapture>();
    }

    [Fact]
    public async Task A_probe_handler_receives_the_event_for_a_reported_outcome()
    {
        var member = await EnrolAsAdminAsync("Event Probe Target");

        await _reliabilityReportingAppService.ReportAsync(new ReportReliabilityOutcomeDto
        {
            MemberId = member.Id,
            OutcomeType = ReliabilityOutcomeType.OverdueReturn,
            OccurrenceId = Guid.NewGuid()
        });

        var events = _capture.Events.Where(e => e.MemberId == member.Id).ToList();
        events.Count.ShouldBe(2); // Enrolled (from EnrolAsAdminAsync) + this RatingOutcome
        events[^1].Kind.ShouldBe(MemberStandingChangeKind.RatingOutcome);
    }

    [Fact]
    public async Task CrossedLowRatingThreshold_is_true_exactly_when_the_rating_crosses_the_threshold_in_either_direction()
    {
        // Rating starts at 100; default threshold is 50, default overdue penalty is 10.
        // 100 -> 90 -> 80 -> 70 -> 60 -> 50: five penalties, none crosses (50 is still "at or above" the threshold).
        var member = await EnrolAsAdminAsync("Threshold Crossing Target");

        for (var i = 0; i < 5; i++)
        {
            await _reliabilityReportingAppService.ReportAsync(new ReportReliabilityOutcomeDto
            {
                MemberId = member.Id,
                OutcomeType = ReliabilityOutcomeType.OverdueReturn,
                OccurrenceId = Guid.NewGuid()
            });
        }

        var beforeCrossing = _capture.Events
            .Where(e => e.MemberId == member.Id && e.Kind == MemberStandingChangeKind.RatingOutcome)
            .ToList();
        beforeCrossing.Count.ShouldBe(5);
        beforeCrossing.ShouldAllBe(e => !e.CrossedLowRatingThreshold);

        // 50 -> 40: crosses downward.
        var crossingDown = await _reliabilityReportingAppService.ReportAsync(new ReportReliabilityOutcomeDto
        {
            MemberId = member.Id,
            OutcomeType = ReliabilityOutcomeType.OverdueReturn,
            OccurrenceId = Guid.NewGuid()
        });
        crossingDown.ResultingRating.ShouldBe(40);

        var afterCrossingDown = _capture.Events
            .Where(e => e.MemberId == member.Id && e.Kind == MemberStandingChangeKind.RatingOutcome)
            .ToList();
        afterCrossingDown.Count.ShouldBe(6);
        afterCrossingDown[^1].CrossedLowRatingThreshold.ShouldBeTrue();

        // 40 -> 55: crosses upward. Manual adjustment (internal roster surface)
        // raises the same MemberStandingChangedEto through the same aggregate helper.
        var adminPrincipal = await BuildAdminPrincipalAsync();
        using (Impersonate(adminPrincipal))
        {
            var memberDetail = await MemberAppService.GetAsync(member.Id);
            await MemberAppService.AdjustRatingAsync(member.Id, new AdjustMemberRatingDto
            {
                Points = 15,
                Reason = "Crossing back upward",
                ConcurrencyStamp = memberDetail.ConcurrencyStamp
            });
        }

        var afterCrossingUp = _capture.Events
            .Where(e => e.MemberId == member.Id && e.Kind == MemberStandingChangeKind.RatingOutcome)
            .ToList();
        afterCrossingUp.Count.ShouldBe(7);
        afterCrossingUp[^1].NewRating.ShouldBe(55);
        afterCrossingUp[^1].CrossedLowRatingThreshold.ShouldBeTrue();
    }
}
