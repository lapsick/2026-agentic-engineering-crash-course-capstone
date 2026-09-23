using System;
using Shouldly;
using ToolShare.Catalog.ToolInstances;
using Volo.Abp;
using Xunit;

namespace ToolShare.Catalog.ToolInstances;

public class SerialNumberRuleTests
{
    private static ToolInstance CreateInstance(string serialNumber, ToolCondition condition = ToolCondition.Good)
    {
        return new ToolInstance(Guid.NewGuid(), Guid.NewGuid(), serialNumber, condition, DateTime.UtcNow, null);
    }

    [Theory]
    [InlineData("RH-001")]
    [InlineData("RH001")]
    [InlineData("A")]
    [InlineData("a1")]
    [InlineData("SN.001/A_B")]
    public void Valid_serial_numbers_are_accepted(string serialNumber)
    {
        var instance = CreateInstance(serialNumber);
        instance.SerialNumber.ShouldBe(serialNumber);
    }

    [Fact]
    public void Serial_number_is_trimmed()
    {
        var instance = CreateInstance("  RH-001  ");
        instance.SerialNumber.ShouldBe("RH-001");
    }

    [Theory]
    [InlineData("rh-001", "RH-001")]
    [InlineData("RH-001", "RH-001")]
    [InlineData("Rh-001", "RH-001")]
    public void Normalized_serial_number_is_case_insensitive(string serialNumber, string expectedNormalized)
    {
        var instance = CreateInstance(serialNumber);
        instance.NormalizedSerialNumber.ShouldBe(expectedNormalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_or_whitespace_serial_number_is_rejected(string serialNumber)
    {
        Should.Throw<ArgumentException>(() => CreateInstance(serialNumber));
    }

    [Theory]
    [InlineData("-RH001")] // must start with alphanumeric
    [InlineData("_RH001")]
    [InlineData("RH 001")] // no spaces
    [InlineData("RH@001")] // no disallowed symbols
    public void Serial_number_with_disallowed_characters_is_rejected(string serialNumber)
    {
        var exception = Should.Throw<BusinessException>(() => CreateInstance(serialNumber));
        exception.Code.ShouldBe("Catalog:InvalidSerialNumber");
    }

    [Fact]
    public void Serial_number_exceeding_max_length_is_rejected()
    {
        var tooLong = new string('A', 65);
        Should.Throw<ArgumentException>(() => CreateInstance(tooLong));
    }

    [Fact]
    public void Serial_number_at_max_length_is_accepted()
    {
        var maxLength = new string('A', 64);
        var instance = CreateInstance(maxLength);
        instance.SerialNumber.ShouldBe(maxLength);
    }
}
