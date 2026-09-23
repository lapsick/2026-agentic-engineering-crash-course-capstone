using System.Globalization;
using System.Text;

namespace ToolShare.Catalog;

/// <summary>
/// Normalizes display text for case-insensitive, accent-tolerant matching and
/// uniqueness (SC-008, FR-003). Pure and database-free so it is unit-testable
/// and reused identically by both the write path (persisted NormalizedName /
/// NormalizedSerialNumber columns) and the search query builder.
/// </summary>
public static class CatalogTextNormalizer
{
    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = CollapseWhitespace(value.Trim());

        var decomposed = trimmed.Normalize(NormalizationForm.FormD);

        var withoutMarks = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                withoutMarks.Append(c);
            }
        }

        var upper = withoutMarks.ToString().ToUpper(CultureInfo.InvariantCulture);

        return upper.Normalize(NormalizationForm.FormC);
    }

    private static string CollapseWhitespace(string value)
    {
        var sb = new StringBuilder(value.Length);
        var lastWasWhitespace = false;

        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasWhitespace)
                {
                    sb.Append(' ');
                }

                lastWasWhitespace = true;
            }
            else
            {
                sb.Append(c);
                lastWasWhitespace = false;
            }
        }

        return sb.ToString();
    }
}
