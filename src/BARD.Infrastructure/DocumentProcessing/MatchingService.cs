using System.Text.RegularExpressions;
using BARD.Application.Common.Options;
using BARD.Application.DocumentProcessing.Interfaces;
using BARD.Application.DocumentProcessing.Models;
using BARD.Domain.Enums;
using FuzzySharp;
using Microsoft.Extensions.Options;

namespace BARD.Infrastructure.DocumentProcessing;

/// <summary>
/// Port of core/matching/scoring.py + matcher.py. Every weight,
/// threshold, and the confirmed hard-block rule (excise code
/// missing/unverifiable -> always MANUAL_REVIEW, regardless of overall
/// score) are preserved exactly, sourced from ScoringWeightsOptions /
/// MatchThresholdsOptions / BusinessRulesOptions (bound from
/// appsettings.json, same values as the Python prototype's
/// core/config.py constants).
/// </summary>
public class MatchingService : IMatchingService
{
    private readonly ScoringWeightsOptions _weights;
    private readonly MatchThresholdsOptions _thresholds;
    private readonly BusinessRulesOptions _businessRules;
    private readonly IAliasResolverService _aliasResolver;

    public MatchingService(
        IOptions<ScoringWeightsOptions> weights,
        IOptions<MatchThresholdsOptions> thresholds,
        IOptions<BusinessRulesOptions> businessRules,
        IAliasResolverService aliasResolver)
    {
        _weights = weights.Value;
        _thresholds = thresholds.Value;
        _businessRules = businessRules.Value;
        _aliasResolver = aliasResolver;
    }

    private static string NormaliseInvoiceNumber(string? value) =>
        value is null ? "" : Regex.Replace(value.ToUpperInvariant(), @"[^A-Z0-9]", "");

    private static string NormaliseCountry(string? value) => (value ?? "").Trim().ToUpperInvariant();

    private (decimal Score, bool Match) ScoreInvoiceNumber(ParsedExcelClaimRow row, ParsedInvoice invoice)
    {
        var a = NormaliseInvoiceNumber(row.InvoiceNumber);
        var b = NormaliseInvoiceNumber(invoice.InvoiceNumber);
        if (a == "" || b == "") return (0m, false);
        var match = a == b;
        return (match ? 100m : 0m, match);
    }

    private (decimal Score, bool Match) ScoreQuantity(ParsedExcelClaimRow row, ParsedInvoiceLine line)
    {
        var a = row.Quantity;
        var b = line.Quantity;
        if (a is null || b is null) return (0m, false);

        if (_businessRules.QuantityTolerancePct <= 0m)
        {
            var exactMatch = a == b;
            return (exactMatch ? 100m : 0m, exactMatch);
        }

        if (a == 0 && b == 0) return (100m, true);
        if (a == 0 || b == 0) return (0m, false);

        var relativeDiff = Math.Abs(a.Value - b.Value) / Math.Max(Math.Abs(a.Value), Math.Abs(b.Value));
        var withinTolerance = relativeDiff <= _businessRules.QuantityTolerancePct;
        if (withinTolerance) return (100m, true);

        var score = Math.Max(0m, 100m * (1 - relativeDiff));
        return (score, false);
    }

    /// <summary>
    /// Excise code has no direct equivalent on a sales invoice — this
    /// criterion checks presence on the Excel row only (data-completeness
    /// check). Full cross-reference validation is a documented future
    /// extension, same limitation as the Python prototype.
    /// </summary>
    private static (decimal Score, bool Present) ScoreExciseCode(ParsedExcelClaimRow row)
    {
        var hasCode = !string.IsNullOrWhiteSpace(row.ExciseCode);
        return (hasCode ? 100m : 0m, hasCode);
    }

    private (decimal Score, bool Match, bool AliasResolved, string? Canonical) ScoreDescription(ParsedExcelClaimRow row, ParsedInvoiceLine line)
    {
        var descA = row.ProductDescription ?? "";
        var descB = line.ProductDescription ?? "";
        if (descA == "" || descB == "") return (0m, false, false, null);

        var canonicalA = _aliasResolver.Resolve(descA);
        var canonicalB = _aliasResolver.Resolve(descB);

        if (canonicalA is not null && canonicalA == canonicalB)
            return (100m, true, true, canonicalA);

        var fuzzyScore = (decimal)Fuzz.TokenSetRatio(descA, descB);
        if (fuzzyScore < _businessRules.DescriptionFuzzyFloor)
            return (0m, false, false, null);

        var isMatch = fuzzyScore >= 80m;
        return (fuzzyScore, isMatch, false, null);
    }

    private (decimal Score, bool Match) ScoreDestinationCountry(ParsedExcelClaimRow row, ParsedInvoice invoice)
    {
        var a = NormaliseCountry(row.DestinationCountry);
        var b = NormaliseCountry(invoice.DestinationCountry);
        if (a == "" || b == "") return (0m, false);
        var match = a == b;
        return (match ? 100m : 0m, match);
    }

    private (decimal Overall, ScoreBreakdown Breakdown, string? HardBlockReason) ComputeScore(
        ParsedExcelClaimRow row, ParsedInvoice invoice, ParsedInvoiceLine line)
    {
        var (invScore, invMatch) = ScoreInvoiceNumber(row, invoice);
        var (qtyScore, qtyMatch) = ScoreQuantity(row, line);
        var (exciseScore, excisePresent) = ScoreExciseCode(row);
        var (descScore, descMatch, aliasResolved, canonical) = ScoreDescription(row, line);
        var (countryScore, countryMatch) = ScoreDestinationCountry(row, invoice);

        var overall =
            invScore * _weights.InvoiceNumber +
            qtyScore * _weights.Quantity +
            exciseScore * _weights.ExciseCode +
            descScore * _weights.Description +
            countryScore * _weights.DestinationCountry;

        var notes = new List<string>();
        if (aliasResolved) notes.Add($"Description matched via alias dictionary (canonical: '{canonical}').");
        if (!excisePresent) notes.Add("Excel row has no excise code recorded — flag for manual review.");
        if (!countryMatch && (!string.IsNullOrEmpty(row.DestinationCountry) || !string.IsNullOrEmpty(invoice.DestinationCountry)))
            notes.Add("Destination country differs or could not be confirmed.");

        var breakdown = new ScoreBreakdown(invScore, qtyScore, exciseScore, descScore, countryScore,
            invMatch, qtyMatch, excisePresent, descMatch, countryMatch, aliasResolved, canonical, notes);

        string? hardBlockReason = null;
        if (_businessRules.ExciseCodeMismatchIsHardBlock && !excisePresent)
            hardBlockReason = "Excise code missing on this row — hard rule forces manual review.";

        return (overall, breakdown, hardBlockReason);
    }

    private static MatchStatus StatusForScore(decimal score, string? hardBlockReason, MatchThresholdsOptions thresholds)
    {
        if (hardBlockReason is not null) return MatchStatus.ManualReviewRequired;
        if (score >= thresholds.AutoMatchMin) return MatchStatus.AutoMatch;
        if (score >= thresholds.LikelyMatchMin) return MatchStatus.LikelyMatch;
        return MatchStatus.ManualReviewRequired;
    }

    private MatchResult MatchRow(ParsedExcelClaimRow row, IReadOnlyList<ParsedInvoice> invoices)
    {
        var targetNumber = NormaliseInvoiceNumber(row.InvoiceNumber);
        var exactNumberInvoices = targetNumber != ""
            ? invoices.Where(inv => NormaliseInvoiceNumber(inv.InvoiceNumber) == targetNumber).ToList()
            : new List<ParsedInvoice>();

        var candidateInvoices = exactNumberInvoices.Count > 0 ? exactNumberInvoices : invoices;

        var bestScore = -1m;
        ParsedInvoice? bestInvoice = null;
        ParsedInvoiceLine? bestLine = null;
        ScoreBreakdown? bestBreakdown = null;
        string? bestHardBlock = null;
        var perInvoiceBest = new Dictionary<string, decimal>();

        foreach (var invoice in candidateInvoices)
        {
            if (invoice.Lines.Count == 0) continue;
            var invoiceBestScore = -1m;

            foreach (var line in invoice.Lines)
            {
                var (score, breakdown, hardBlockReason) = ComputeScore(row, invoice, line);
                if (score > invoiceBestScore) invoiceBestScore = score;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestInvoice = invoice;
                    bestLine = line;
                    bestBreakdown = breakdown;
                    bestHardBlock = hardBlockReason;
                }
            }

            perInvoiceBest[invoice.SourceFile] = invoiceBestScore;
        }

        if (bestInvoice is null)
            return new MatchResult(row, null, null, 0m, null, MatchStatus.NoMatch, null, Array.Empty<(string, decimal)>());

        var alternatives = perInvoiceBest
            .Where(kv => kv.Key != bestInvoice.SourceFile)
            .OrderByDescending(kv => kv.Value)
            .Take(3)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();

        var status = StatusForScore(bestScore, bestHardBlock, _thresholds);

        return new MatchResult(row, bestInvoice, bestLine, Math.Round(bestScore, 1), bestBreakdown, status, bestHardBlock, alternatives);
    }

    public IReadOnlyList<MatchResult> MatchAll(IReadOnlyList<ParsedExcelClaimRow> excelRows, IReadOnlyList<ParsedInvoice> invoices) =>
        excelRows.Select(row => MatchRow(row, invoices)).ToList();
}
