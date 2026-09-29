using System.Text.RegularExpressions;
using BARD.Application.DocumentProcessing.Interfaces;
using BARD.Application.DocumentProcessing.Models;
using BARD.Domain.Enums;

namespace BARD.Infrastructure.DocumentProcessing;

public sealed class DocumentReferenceResolverService : IDocumentReferenceResolver
{
    private sealed record PatternFamily(
        string Name,
        DocumentReferenceType Type,
        Regex Regex,
        decimal Confidence);

    private static readonly PatternFamily[] PatternFamilies =
    {
        // Current Belgian AC4 family. The first 2 digits are the variable year.
        new(
            "BE_AC4_CURRENT",
            DocumentReferenceType.Drn,
            new Regex(
                @"(?<![A-Z0-9])\d{2}BEAC4[A-Z0-9]{14}(?![A-Z0-9])",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            0.78m),

        // Current Belgian EMCS family seen in e-AD/e-VAD references.
        // Pattern alone is evidence, never certainty.
        new(
            "BE_EMCS_CURRENT",
            DocumentReferenceType.Arc,
            new Regex(
                @"(?<![A-Z0-9])\d{2}BEM[A-Z0-9]{16}(?![A-Z0-9])",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            0.68m),

        // Generic 18-character customs MRN shape.
        new(
            "EU_CUSTOMS_MRN_SHAPE",
            DocumentReferenceType.Mrn,
            new Regex(
                @"(?<![A-Z0-9])\d{2}[A-Z]{2}[A-Z0-9]{14}(?![A-Z0-9])",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            0.48m),
    };

    private static readonly (DocumentReferenceType Type, Regex Regex)[] ExplicitLabels =
    {
        (
            DocumentReferenceType.Drn,
            new Regex(
                @"\bDRN\s*[:=\-]?\s*(\d{2}[A-Z]{2}AC4[A-Z0-9]{14})",
                RegexOptions.IgnoreCase | RegexOptions.Compiled)
        ),
        (
            DocumentReferenceType.Arc,
            new Regex(
                @"\bARC\s*[:=\-]?\s*([A-Z0-9]{18,24})",
                RegexOptions.IgnoreCase | RegexOptions.Compiled)
        ),
        (
            DocumentReferenceType.Mrn,
            new Regex(
                @"\bMRN\s*[:=\-]?\s*([A-Z0-9]{18,24})",
                RegexOptions.IgnoreCase | RegexOptions.Compiled)
        ),
        (
            DocumentReferenceType.Lrn,
            new Regex(
                @"\bLRN\s*[:=\-]?\s*([A-Z0-9][A-Z0-9._/\-]{2,63})",
                RegexOptions.IgnoreCase | RegexOptions.Compiled)
        ),
    };

    public IReadOnlyList<ResolvedDocumentReference> ResolveAll(
        string text,
        DocumentKind? documentKind = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<ResolvedDocumentReference>();

        var results = new List<ResolvedDocumentReference>();

        foreach (var (type, regex) in ExplicitLabels)
        {
            foreach (Match match in regex.Matches(text))
            {
                results.Add(
                    new ResolvedDocumentReference(
                        type,
                        match.Groups[1].Value.Trim(),
                        ReferenceResolutionMethod.ExplicitLabel,
                        1.00m,
                        null,
                        new[]
                        {
                            $"Reference explicitly labelled {type.ToString().ToUpperInvariant()}."
                        }));
            }
        }

        foreach (var family in PatternFamilies)
        {
            foreach (Match match in family.Regex.Matches(text))
            {
                var confidence = family.Confidence;
                var reasons = new List<string>
                {
                    $"Value matches pattern family {family.Name}."
                };

                if (documentKind == DocumentKind.Ac4Declaration
                    && family.Type == DocumentReferenceType.Drn)
                {
                    confidence = Math.Min(0.95m, confidence + 0.12m);
                    reasons.Add("AC4 document context supports DRN interpretation.");
                }

                if (documentKind == DocumentKind.EadEVadDocument
                    && family.Type == DocumentReferenceType.Arc)
                {
                    confidence = Math.Min(0.95m, confidence + 0.15m);
                    reasons.Add("e-AD/e-VAD context supports ARC interpretation.");
                }

                results.Add(
                    new ResolvedDocumentReference(
                        family.Type,
                        match.Value.Trim(),
                        ReferenceResolutionMethod.PatternInference,
                        confidence,
                        family.Name,
                        reasons));
            }
        }

        return results
            .GroupBy(r => (r.Type, Value: r.Value.ToUpperInvariant()))
            .Select(g => g
                .OrderByDescending(x => x.Confidence)
                .ThenBy(x => x.Method)
                .First())
            .OrderByDescending(r => r.Confidence)
            .ToArray();
    }
}
