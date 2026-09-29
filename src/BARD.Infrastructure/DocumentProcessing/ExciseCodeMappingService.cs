using BARD.Application.DocumentProcessing.Interfaces;
using BARD.Application.DocumentProcessing.Models;

namespace BARD.Infrastructure.DocumentProcessing;

public sealed class ExciseCodeMappingService : IExciseCodeMappingService
{
    private static readonly IReadOnlyDictionary<string, string> DirectMappings =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["W200"] = "S101",
            ["W300"] = "S109",
        };

    public ExciseCodeMappingResult Map(
        string? emcsExciseCode,
        string? rawDescription,
        decimal? alcoholStrength,
        decimal? degreesPlato)
    {
        if (!string.IsNullOrWhiteSpace(emcsExciseCode)
            && DirectMappings.TryGetValue(emcsExciseCode.Trim(), out var direct))
        {
            return new ExciseCodeMappingResult(
                direct,
                1.00m,
                $"Direct configured mapping {emcsExciseCode.Trim().ToUpperInvariant()} -> {direct}.");
        }

        var description = Normalize(rawDescription);

        if (description.Contains("NIETMOUSSERENDEWIJN", StringComparison.Ordinal))
        {
            return new ExciseCodeMappingResult(
                "S101",
                0.95m,
                "Movement description explicitly identifies non-sparkling wine.");
        }

        if (!description.Contains("NIETMOUSSERENDE", StringComparison.Ordinal)
            && description.Contains("MOUSSERENDEWIJN", StringComparison.Ordinal))
        {
            return new ExciseCodeMappingResult(
                "S109",
                0.95m,
                "Movement description explicitly identifies sparkling wine.");
        }

        return new ExciseCodeMappingResult(
            null,
            0m,
            "No configured deterministic Belgian S-code mapping; raw EMCS code retained.");
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(
                value
                    .Where(char.IsLetterOrDigit)
                    .Select(char.ToUpperInvariant)
                    .ToArray());
}
