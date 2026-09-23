using System;
using System.Linq;
using Shouldly;
using Xunit;

namespace ToolShare.Membership.Members;

public class RatingClampingTests
{
    private static Member CreateMember()
    {
        return new Member(Guid.NewGuid(), Guid.NewGuid(), "Alice Example", "alice@example.com", DateTime.UtcNow, null);
    }

    [Fact]
    public void Positive_outcome_within_bounds_applies_raw_points_unchanged()
    {
        var member = CreateMember(); // starts at 100
        member.ApplyOutcome(ReliabilityOutcomeType.ManualAdjustment, -30, occurrenceId: null, reason: "correction", DateTime.UtcNow, null);

        member.CurrentRating.ShouldBe(70);

        var entry = member.StandingHistory.Last();
        entry.RawPoints.ShouldBe(-30);
        entry.EffectivePoints.ShouldBe(-30);
    }

    [Fact]
    public void Outcome_that_would_exceed_100_is_clamped_and_effective_points_differ_from_raw()
    {
        var member = CreateMember(); // starts at 100
        member.ApplyOutcome(ReliabilityOutcomeType.ManualAdjustment, 20, occurrenceId: null, reason: "bonus", DateTime.UtcNow, null);

        member.CurrentRating.ShouldBe(100);

        var entry = member.StandingHistory.Last();
        entry.RawPoints.ShouldBe(20);
        entry.EffectivePoints.ShouldBe(0);
        entry.ResultingRating.ShouldBe(100);
    }

    [Fact]
    public void Outcome_that_would_go_below_0_is_clamped_and_effective_points_differ_from_raw()
    {
        var member = CreateMember();
        member.ApplyOutcome(ReliabilityOutcomeType.ManualAdjustment, -150, occurrenceId: null, reason: "big penalty", DateTime.UtcNow, null);

        member.CurrentRating.ShouldBe(0);

        var entry = member.StandingHistory.Last();
        entry.RawPoints.ShouldBe(-150);
        entry.EffectivePoints.ShouldBe(-100);
        entry.ResultingRating.ShouldBe(0);
    }

    [Fact]
    public void Clamped_outcome_still_records_an_audit_entry_with_zero_effective_points()
    {
        var member = CreateMember();
        member.ApplyOutcome(ReliabilityOutcomeType.CleanReturn, 5, occurrenceId: Guid.NewGuid(), reason: null, DateTime.UtcNow, null);

        var entry = member.StandingHistory.Last();
        entry.EffectivePoints.ShouldBe(0);
        member.StandingHistory.Count.ShouldBe(2); // enrolment + this outcome
    }
}
