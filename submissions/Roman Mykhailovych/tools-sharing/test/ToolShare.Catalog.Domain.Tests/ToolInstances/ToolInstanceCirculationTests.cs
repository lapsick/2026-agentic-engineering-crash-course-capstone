using System;
using Shouldly;
using ToolShare.Catalog.ToolInstances;
using Volo.Abp;
using Xunit;

namespace ToolShare.Catalog.ToolInstances;

/// <summary>
/// The four circulation-reporting methods this feature (004-lending) adds to
/// <see cref="ToolInstance"/>, following <see cref="ToolInstance.ChangeCondition"/>/
/// <see cref="ToolInstance.Retire"/>'s exact existing shape — see
/// specs/004-lending/contracts/catalog-extension.md.
/// </summary>
public class ToolInstanceCirculationTests
{
    private static ToolInstance CreateInstance(ToolCondition initialCondition = ToolCondition.Good)
    {
        return new ToolInstance(Guid.NewGuid(), Guid.NewGuid(), "RH-001", initialCondition, DateTime.UtcNow, null);
    }

    [Fact]
    public void MarkOnLoan_succeeds_from_InCirculation()
    {
        var instance = CreateInstance();

        instance.MarkOnLoan(DateTime.UtcNow, null);

        instance.CirculationState.ShouldBe(ToolInstanceCirculationState.OnLoan);
    }

    [Fact]
    public void MarkOnLoan_is_rejected_when_not_InCirculation()
    {
        var instance = CreateInstance();
        instance.MarkOnLoan(DateTime.UtcNow, null);

        Should.Throw<BusinessException>(() => instance.MarkOnLoan(DateTime.UtcNow, null))
            .Code.ShouldBe("Catalog:InstanceNotAvailableForLoan");
    }

    [Fact]
    public void Return_succeeds_from_OnLoan_and_updates_condition()
    {
        var instance = CreateInstance(ToolCondition.Good);
        instance.MarkOnLoan(DateTime.UtcNow, null);

        instance.Return(ToolCondition.Worn, DateTime.UtcNow, null);

        instance.CirculationState.ShouldBe(ToolInstanceCirculationState.InCirculation);
        instance.Condition.ShouldBe(ToolCondition.Worn);
    }

    [Fact]
    public void Return_is_rejected_when_not_OnLoan()
    {
        var instance = CreateInstance();

        Should.Throw<BusinessException>(() => instance.Return(ToolCondition.Worn, DateTime.UtcNow, null))
            .Code.ShouldBe("Catalog:InstanceNotOnLoan");
    }

    [Fact]
    public void ReturnForMaintenance_succeeds_from_OnLoan_and_moves_to_UnderMaintenance()
    {
        var instance = CreateInstance(ToolCondition.Good);
        instance.MarkOnLoan(DateTime.UtcNow, null);

        instance.ReturnForMaintenance(ToolCondition.Damaged, DateTime.UtcNow, null);

        instance.CirculationState.ShouldBe(ToolInstanceCirculationState.UnderMaintenance);
        instance.Condition.ShouldBe(ToolCondition.Damaged);
    }

    [Fact]
    public void ReturnForMaintenance_is_rejected_when_not_OnLoan()
    {
        var instance = CreateInstance();

        Should.Throw<BusinessException>(() => instance.ReturnForMaintenance(ToolCondition.Damaged, DateTime.UtcNow, null))
            .Code.ShouldBe("Catalog:InstanceNotOnLoan");
    }

    [Fact]
    public void CloseMaintenance_succeeds_from_UnderMaintenance_and_leaves_condition_untouched()
    {
        var instance = CreateInstance(ToolCondition.Good);
        instance.MarkOnLoan(DateTime.UtcNow, null);
        instance.ReturnForMaintenance(ToolCondition.Damaged, DateTime.UtcNow, null);

        instance.CloseMaintenance(DateTime.UtcNow, null);

        instance.CirculationState.ShouldBe(ToolInstanceCirculationState.InCirculation);
        instance.Condition.ShouldBe(ToolCondition.Damaged);
    }

    [Fact]
    public void CloseMaintenance_is_rejected_when_not_UnderMaintenance()
    {
        var instance = CreateInstance();

        Should.Throw<BusinessException>(() => instance.CloseMaintenance(DateTime.UtcNow, null))
            .Code.ShouldBe("Catalog:InstanceNotUnderMaintenance");
    }

    [Fact]
    public void IsAvailable_is_false_while_OnLoan_or_UnderMaintenance()
    {
        var instance = CreateInstance();
        instance.MarkOnLoan(DateTime.UtcNow, null);
        instance.IsAvailable.ShouldBeFalse();

        // Damaged condition keeps IsAvailable false even after the request
        // closes (IsAvailable = InCirculation && Condition != Damaged) —
        // this feature does not model a condition change at closing time.
        instance.ReturnForMaintenance(ToolCondition.Damaged, DateTime.UtcNow, null);
        instance.IsAvailable.ShouldBeFalse();

        instance.CloseMaintenance(DateTime.UtcNow, null);
        instance.IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public void IsAvailable_is_true_after_maintenance_closes_with_a_non_damaged_condition()
    {
        var instance = CreateInstance();
        instance.MarkOnLoan(DateTime.UtcNow, null);
        instance.ReturnForMaintenance(ToolCondition.Worn, DateTime.UtcNow, null);

        instance.CloseMaintenance(DateTime.UtcNow, null);

        instance.IsAvailable.ShouldBeTrue();
    }
}
