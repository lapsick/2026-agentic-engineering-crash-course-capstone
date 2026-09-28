using System;
using System.Linq;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ToolShare.Catalog.ToolInstances;

/// <summary>
/// 008 IR-09 (FR-003, FR-007, FR-011): <see cref="ToolInstance.SendToMaintenance"/>
/// moves an in-circulation instance under maintenance, recording a worse
/// observed condition, in exactly one history row carrying the reason — and
/// refuses retired, on-loan, already-under-maintenance, or improved-condition cases.
/// See specs/008-out-of-band-maintenance/contracts/catalog-extension.md.
/// </summary>
public class SendToMaintenanceTests
{
    private static readonly DateTime Now = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid LibrarianId = Guid.NewGuid();

    private static ToolInstance CreateInstance(ToolCondition condition = ToolCondition.Good)
    {
        return new ToolInstance(Guid.NewGuid(), Guid.NewGuid(), "RH-001", condition, Now.AddDays(-30), null);
    }

    [Fact]
    public void A_worse_observed_condition_is_recorded_and_the_instance_goes_under_maintenance_in_one_history_row()
    {
        var instance = CreateInstance(ToolCondition.Good);
        var historyBefore = instance.StateHistory.Count;

        instance.SendToMaintenance(ToolCondition.Worn, "  Cracked blade guard  ", Now, LibrarianId);

        instance.Condition.ShouldBe(ToolCondition.Worn);
        instance.CirculationState.ShouldBe(ToolInstanceCirculationState.UnderMaintenance);
        instance.IsAvailable.ShouldBeFalse();

        instance.StateHistory.Count.ShouldBe(historyBefore + 1);
        var row = instance.StateHistory.OrderBy(h => h.ChangedAt).Last();
        row.PreviousCondition.ShouldBe(ToolCondition.Good);
        row.NewCondition.ShouldBe(ToolCondition.Worn);
        row.PreviousCirculationState.ShouldBe(ToolInstanceCirculationState.InCirculation);
        row.NewCirculationState.ShouldBe(ToolInstanceCirculationState.UnderMaintenance);
        row.Reason.ShouldBe("Cracked blade guard");
        row.ChangedAt.ShouldBe(Now);
        row.ChangedByUserId.ShouldBe(LibrarianId);
    }

    [Fact]
    public void An_equal_observed_condition_leaves_the_condition_unchanged_but_still_records_the_circulation_change()
    {
        var instance = CreateInstance(ToolCondition.Good);
        var historyBefore = instance.StateHistory.Count;

        instance.SendToMaintenance(ToolCondition.Good, "Frayed cord", Now, LibrarianId);

        instance.Condition.ShouldBe(ToolCondition.Good);
        instance.CirculationState.ShouldBe(ToolInstanceCirculationState.UnderMaintenance);
        instance.StateHistory.Count.ShouldBe(historyBefore + 1);

        var row = instance.StateHistory.OrderBy(h => h.ChangedAt).Last();
        row.PreviousCondition.ShouldBe(ToolCondition.Good);
        row.NewCondition.ShouldBe(ToolCondition.Good);
        row.NewCirculationState.ShouldBe(ToolInstanceCirculationState.UnderMaintenance);
    }

    [Fact]
    public void A_Damaged_instance_may_be_reported_as_Damaged()
    {
        var instance = CreateInstance(ToolCondition.Damaged);

        instance.SendToMaintenance(ToolCondition.Damaged, "Still broken", Now, LibrarianId);

        instance.CirculationState.ShouldBe(ToolInstanceCirculationState.UnderMaintenance);
    }

    [Fact]
    public void Exactly_one_state_changed_event_is_raised()
    {
        var instance = CreateInstance(ToolCondition.Good);
        var eventsBefore = instance.GetLocalEvents().Count();

        instance.SendToMaintenance(ToolCondition.Worn, "Cracked", Now, LibrarianId);

        var events = instance.GetLocalEvents().Skip(eventsBefore).Select(e => e.EventData).ToList();
        events.Count.ShouldBe(1);
        var eto = events.Single().ShouldBeOfType<ToolInstanceStateChangedEto>();
        eto.NewCirculationState.ShouldBe(ToolInstanceCirculationState.UnderMaintenance);
        eto.NewCondition.ShouldBe(ToolCondition.Worn);
        eto.Reason.ShouldBe("Cracked");
    }

    [Fact]
    public void A_retired_instance_is_refused()
    {
        var instance = CreateInstance();
        instance.Retire("Worn out", Now.AddDays(-1), null);

        Should.Throw<BusinessException>(() => instance.SendToMaintenance(ToolCondition.Good, "Cracked", Now, LibrarianId))
            .Code.ShouldBe("Catalog:InstanceIsRetired");
    }

    [Fact]
    public void An_instance_on_loan_is_refused()
    {
        var instance = CreateInstance();
        instance.MarkOnLoan(Now.AddDays(-1), null);

        Should.Throw<BusinessException>(() => instance.SendToMaintenance(ToolCondition.Good, "Cracked", Now, LibrarianId))
            .Code.ShouldBe("Catalog:InstanceNotAvailableForMaintenance");
    }

    [Fact]
    public void An_instance_already_under_maintenance_is_refused()
    {
        var instance = CreateInstance();
        instance.SendToMaintenance(ToolCondition.Good, "Cracked", Now.AddDays(-1), LibrarianId);

        Should.Throw<BusinessException>(() => instance.SendToMaintenance(ToolCondition.Good, "Again", Now, LibrarianId))
            .Code.ShouldBe("Catalog:InstanceNotAvailableForMaintenance");
    }

    [Fact]
    public void A_better_observed_condition_is_refused()
    {
        var instance = CreateInstance(ToolCondition.Good);

        Should.Throw<BusinessException>(() => instance.SendToMaintenance(ToolCondition.New, "Cracked", Now, LibrarianId))
            .Code.ShouldBe("Catalog:ObservedConditionBetterThanCurrent");

        instance.CirculationState.ShouldBe(ToolInstanceCirculationState.InCirculation);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_reason_is_refused(string reason)
    {
        var instance = CreateInstance();

        Should.Throw<ArgumentException>(() => instance.SendToMaintenance(ToolCondition.Good, reason, Now, LibrarianId));
    }

    [Fact]
    public void A_reason_longer_than_the_history_limit_is_refused()
    {
        var instance = CreateInstance();
        var reason = new string('x', CatalogDomainSharedConsts.ConditionChangeReasonMaxLength + 1);

        Should.Throw<ArgumentException>(() => instance.SendToMaintenance(ToolCondition.Good, reason, Now, LibrarianId));
    }

    [Fact]
    public void The_existing_CloseMaintenance_returns_it_to_circulation_with_the_condition_untouched()
    {
        var instance = CreateInstance(ToolCondition.Good);
        instance.SendToMaintenance(ToolCondition.Worn, "Cracked", Now, LibrarianId);

        instance.CloseMaintenance(Now.AddDays(1), LibrarianId);

        instance.CirculationState.ShouldBe(ToolInstanceCirculationState.InCirculation);
        instance.Condition.ShouldBe(ToolCondition.Worn);
    }
}
