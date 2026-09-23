using System;
using Shouldly;
using ToolShare.Catalog.ToolInstances;
using Volo.Abp;
using Xunit;

namespace ToolShare.Catalog.ToolInstances;

public class ConditionTransitionTests
{
    private static ToolInstance CreateInstance(ToolCondition initialCondition = ToolCondition.Good)
    {
        return new ToolInstance(Guid.NewGuid(), Guid.NewGuid(), "RH-001", initialCondition, DateTime.UtcNow, null);
    }

    [Theory]
    [InlineData(ToolCondition.New, ToolCondition.Good)]
    [InlineData(ToolCondition.Good, ToolCondition.Worn)]
    [InlineData(ToolCondition.Worn, ToolCondition.Damaged)]
    [InlineData(ToolCondition.Damaged, ToolCondition.Good)] // repair — condition can improve, not a one-way ratchet
    [InlineData(ToolCondition.Worn, ToolCondition.New)]
    public void Any_condition_to_any_other_condition_is_allowed(ToolCondition from, ToolCondition to)
    {
        var instance = CreateInstance(from);

        instance.ChangeCondition(to, "reason", DateTime.UtcNow, null);

        instance.Condition.ShouldBe(to);
    }

    [Fact]
    public void Changing_to_the_same_condition_is_rejected_as_a_no_op()
    {
        var instance = CreateInstance(ToolCondition.Good);

        var exception = Should.Throw<BusinessException>(() =>
            instance.ChangeCondition(ToolCondition.Good, "reason", DateTime.UtcNow, null));

        exception.Code.ShouldBe("Catalog:ConditionUnchanged");
        instance.Condition.ShouldBe(ToolCondition.Good);
    }

    [Fact]
    public void Condition_cannot_change_while_retired()
    {
        var instance = CreateInstance(ToolCondition.Good);
        instance.Retire("no longer needed", DateTime.UtcNow, null);

        var exception = Should.Throw<BusinessException>(() =>
            instance.ChangeCondition(ToolCondition.Worn, "reason", DateTime.UtcNow, null));

        exception.Code.ShouldBe("Catalog:InstanceIsRetired");
    }

    [Fact]
    public void Condition_change_reason_is_optional()
    {
        var instance = CreateInstance(ToolCondition.Good);

        instance.ChangeCondition(ToolCondition.Worn, reason: null, DateTime.UtcNow, null);

        instance.Condition.ShouldBe(ToolCondition.Worn);
    }
}
