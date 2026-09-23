using System;
using Shouldly;
using ToolShare.Catalog.ToolInstances;
using Xunit;

namespace ToolShare.Catalog.ToolInstances;

public class AvailabilityTests
{
    [Theory]
    [InlineData(ToolCondition.New, true)]
    [InlineData(ToolCondition.Good, true)]
    [InlineData(ToolCondition.Worn, true)]
    [InlineData(ToolCondition.Damaged, false)]
    public void InCirculation_instance_is_available_unless_damaged(ToolCondition condition, bool expectedAvailable)
    {
        var instance = new ToolInstance(Guid.NewGuid(), Guid.NewGuid(), "RH-001", condition, DateTime.UtcNow, null);

        instance.IsAvailable.ShouldBe(expectedAvailable);
    }

    [Theory]
    [InlineData(ToolCondition.New)]
    [InlineData(ToolCondition.Good)]
    [InlineData(ToolCondition.Worn)]
    [InlineData(ToolCondition.Damaged)]
    public void Retired_instance_is_never_available_regardless_of_condition(ToolCondition condition)
    {
        var instance = new ToolInstance(Guid.NewGuid(), Guid.NewGuid(), "RH-001", condition, DateTime.UtcNow, null);
        instance.Retire("no longer needed", DateTime.UtcNow, null);

        instance.IsAvailable.ShouldBeFalse();
    }
}
