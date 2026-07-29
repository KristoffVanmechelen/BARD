using System.Text.RegularExpressions;

namespace BARD.Application.Common.Services;

/// <summary>
/// Normalizes an enterprise/VAT number for reliable matching/deduplication
/// (authoritative decision #4). Strips whitespace, punctuation, and
/// country-prefix casing variance; uppercases the result. Deliberately
/// simple and deterministic — no external VAT-validation service call
/// (out of scope; not requested by the documented decisions).
///
/// Examples all normalize to "BE0123456789":
///   "BE 0123.456.789", "be0123456789", "BE-0123-456-789"
/// </summary>
public static class CompanyNumberNormalizer
{
    private static readonly Regex NonAlphanumeric = new(@"[^A-Za-z0-9]", RegexOptions.Compiled);

    public static string Normalize(string enterpriseNumber)
    {
        if (string.IsNullOrWhiteSpace(enterpriseNumber))
            throw new ArgumentException("Enterprise/VAT number is required.", nameof(enterpriseNumber));

        var stripped = NonAlphanumeric.Replace(enterpriseNumber, "");
        return stripped.ToUpperInvariant();
    }
}
