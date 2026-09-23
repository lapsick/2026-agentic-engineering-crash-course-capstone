using System;
using System.Linq;
using Shouldly;
using ToolShare.Catalog.ToolInstances;
using Xunit;

namespace ToolShare.Catalog.ToolInstances;

public class StateHistoryTests
{
    [Fact]
    public void Registration_appends_exactly_one_row_with_null_previous_values()
    {
        var registeredAt = DateTime.UtcNow;
        var instance = new ToolInstance(Guid.NewGuid(), Guid.NewGuid(), "RH-001", ToolCondition.Good, registeredAt, null);

        instance.StateHistory.Count.ShouldBe(1);

        var row = instance.StateHistory.Single();
        row.PreviousCondition.ShouldBeNull();
        row.PreviousCirculationState.ShouldBeNull();
        row.NewCondition.ShouldBe(ToolCondition.Good);
        row.NewCirculationState.ShouldBe(ToolInstanceCirculationState.InCirculation);
        row.ChangedAt.ShouldBe(registeredAt);
    }

    [Fact]
    public void Every_transition_appends_exactly_one_row_incl_registration()
    {
        var instance = new ToolInstance(Guid.NewGuid(), Guid.NewGuid(), "RH-001", ToolCondition.New, DateTime.UtcNow, null);
        instance.ChangeCondition(ToolCondition.Good, "inspected", DateTime.UtcNow, null);
        instance.ChangeCondition(ToolCondition.Worn, "wear noted", DateTime.UtcNow, null);
        instance.Retire("decommissioned", DateTime.UtcNow, null);

        // registration + 2 condition changes + retirement = 4 rows
        instance.StateHistory.Count.ShouldBe(4);
    }

    [Fact]
    public void History_rows_are_chronological_and_first_row_has_null_previous_values()
    {
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var instance = new ToolInstance(Guid.NewGuid(), Guid.NewGuid(), "RH-001", ToolCondition.New, t0, null);
        instance.ChangeCondition(ToolCondition.Good, "inspected", t0.AddDays(1), null);
        instance.Retire("decommissioned", t0.AddDays(2), null);

        var ordered = instance.StateHistory.OrderBy(h => h.ChangedAt).ToList();

        ordered[0].PreviousCondition.ShouldBeNull();
        ordered[0].PreviousCirculationState.ShouldBeNull();
        ordered[0].ChangedAt.ShouldBe(t0);

        ordered[1].PreviousCondition.ShouldBe(ToolCondition.New);
        ordered[1].NewCondition.ShouldBe(ToolCondition.Good);
        ordered[1].ChangedAt.ShouldBe(t0.AddDays(1));

        ordered[2].NewCirculationState.ShouldBe(ToolInstanceCirculationState.Retired);
        ordered[2].ChangedAt.ShouldBe(t0.AddDays(2));
    }

    [Fact]
    public void Retirement_row_repeats_the_unchanged_condition_dimension()
    {
        var instance = new ToolInstance(Guid.NewGuid(), Guid.NewGuid(), "RH-001", ToolCondition.Worn, DateTime.UtcNow, null);
        instance.Retire("decommissioned", DateTime.UtcNow, null);

        var retirementRow = instance.StateHistory.Last();

        // Only circulation state actually moved — the condition dimension repeats
        // its current value in both Previous* and New* (HR-04).
        retirementRow.PreviousCondition.ShouldBe(ToolCondition.Worn);
        retirementRow.NewCondition.ShouldBe(ToolCondition.Worn);
        retirementRow.PreviousCirculationState.ShouldBe(ToolInstanceCirculationState.InCirculation);
        retirementRow.NewCirculationState.ShouldBe(ToolInstanceCirculationState.Retired);
    }
}
