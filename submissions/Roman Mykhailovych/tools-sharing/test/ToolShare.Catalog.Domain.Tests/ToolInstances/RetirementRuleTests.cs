using System;
using Shouldly;
using ToolShare.Catalog.ToolInstances;
using Volo.Abp;
using Xunit;

namespace ToolShare.Catalog.ToolInstances;

public class RetirementRuleTests
{
    private static ToolInstance CreateInstance()
    {
        return new ToolInstance(Guid.NewGuid(), Guid.NewGuid(), "RH-001", ToolCondition.Good, DateTime.UtcNow, null);
    }

    [Fact]
    public void Retiring_from_in_circulation_succeeds()
    {
        var instance = CreateInstance();
        var retiredAt = DateTime.UtcNow;

        instance.Retire("Motor burnt out", retiredAt, null);

        instance.CirculationState.ShouldBe(ToolInstanceCirculationState.Retired);
        instance.RetirementReason.ShouldBe("Motor burnt out");
        instance.RetiredAt.ShouldBe(retiredAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Retiring_without_a_reason_is_rejected(string? reason)
    {
        var instance = CreateInstance();

        var exception = Should.Throw<BusinessException>(() =>
            instance.Retire(reason!, DateTime.UtcNow, null));

        exception.Code.ShouldBe("Catalog:RetirementReasonRequired");
        instance.CirculationState.ShouldBe(ToolInstanceCirculationState.InCirculation);
    }

    [Fact]
    public void Retirement_is_terminal_in_this_feature()
    {
        var instance = CreateInstance();
        instance.Retire("Motor burnt out", DateTime.UtcNow, null);

        var exception = Should.Throw<BusinessException>(() =>
            instance.Retire("Retiring again", DateTime.UtcNow, null));

        exception.Code.ShouldBe("Catalog:InstanceAlreadyRetired");
    }

    [Fact]
    public void Retiring_never_deletes_the_record()
    {
        var instance = CreateInstance();
        instance.AddPhoto("blob-1", "photo.jpg", "image/jpeg", 1024, new[] { "image/jpeg" }, 5 * 1024 * 1024, 5);

        instance.Retire("Motor burnt out", DateTime.UtcNow, null);

        // The record, its photos and its full history all remain — nothing is removed.
        instance.Photos.Count.ShouldBe(1);
        instance.StateHistory.Count.ShouldBe(2); // registration + retirement
        instance.SerialNumber.ShouldBe("RH-001");
    }
}
