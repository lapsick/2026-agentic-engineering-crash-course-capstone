using System.Globalization;
using Shouldly;
using Xunit;

namespace ToolShare.Catalog;

public class CatalogTextNormalizerTests
{
    [Theory]
    [InlineData("Rotary Hammer", "ROTARY HAMMER")]
    [InlineData("rotary hammer", "ROTARY HAMMER")]
    [InlineData("ROTARY HAMMER", "ROTARY HAMMER")]
    [InlineData("  Rotary   Hammer  ", "ROTARY HAMMER")]
    [InlineData("rh-001", "RH-001")]
    [InlineData("RH-001", "RH-001")]
    public void Normalize_folds_case_and_collapses_whitespace(string input, string expected)
    {
        CatalogTextNormalizer.Normalize(input).ShouldBe(expected);
    }

    [Fact]
    public void Normalize_is_case_insensitive_for_ukrainian_names()
    {
        // Ukrainian for "drill" in three casings.
        var mixedCase = "Дриль";
        var lowerCase = "дриль";
        var upperCase = "ДРИЛЬ";

        CatalogTextNormalizer.Normalize(mixedCase).ShouldBe(upperCase);
        CatalogTextNormalizer.Normalize(lowerCase).ShouldBe(upperCase);
        CatalogTextNormalizer.Normalize(upperCase).ShouldBe(upperCase);
    }

    [Fact]
    public void Normalize_strips_combining_diacritics()
    {
        // Build "e" + a combining acute accent (U+0301) purely from code points,
        // so the source file never contains a literal accented glyph at all.
        var decomposed = "cafe" + char.ConvertFromUtf32(0x0301);
        var precomposed = "caf" + char.ConvertFromUtf32(0x00E9);

        decomposed.ShouldNotBe(precomposed); // sanity: genuinely different code points

        CatalogTextNormalizer.Normalize(decomposed).ShouldBe("CAFE");
        CatalogTextNormalizer.Normalize(precomposed).ShouldBe("CAFE");
    }

    [Fact]
    public void Normalize_of_null_or_whitespace_returns_empty()
    {
        CatalogTextNormalizer.Normalize("").ShouldBe(string.Empty);
        CatalogTextNormalizer.Normalize("   ").ShouldBe(string.Empty);
    }
}
