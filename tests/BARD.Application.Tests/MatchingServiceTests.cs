using BARD.Application.Common.Options;
using BARD.Application.DocumentProcessing.Models;
using BARD.Domain.Enums;
using BARD.Infrastructure.DocumentProcessing;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BARD.Application.Tests;

public class MatchingServiceTests
{
    private static MatchingService CreateService(BusinessRulesOptions? businessRules = null)
    {
        var weights = Options.Create(new ScoringWeightsOptions());
        var thresholds = Options.Create(new MatchThresholdsOptions());
        var rules = Options.Create(businessRules ?? new BusinessRulesOptions());
        return new MatchingService(weights, thresholds, rules, new FakeAliasResolver());
    }

    private static ParsedInvoice Invoice(string number, string country, params ParsedInvoiceLine[] lines) =>
        new(number, null, "Customer", "Address", country, lines, "invoice.pdf",
            ExtractionMethod.ClassicalTextExtraction, 1.0m, Array.Empty<string>(), "");

    private static ParsedExcelClaimRow Row(int idx, string? invoiceNumber, string? product, string? excise,
        decimal? qty, string? mrn, string? country) =>
        new(idx, invoiceNumber, product, excise, qty, mrn, country, new Dictionary<string, string?>());

    [Fact]
    public void ExactMatch_ScoresAtOrAboveAutoMatchThreshold()
    {
        var invoices = new[] { Invoice("INV-1001", "FR", new ParsedInvoiceLine("Jupiler 24x33", 120m, null, null, "", 0, null)) };
        var row = Row(0, "INV-1001", "JUPILER", "S101", 120m, "M1", "FR");

        var result = new MatchingService(Options.Create(new ScoringWeightsOptions()), Options.Create(new MatchThresholdsOptions()),
            Options.Create(new BusinessRulesOptions()), new FakeAliasResolver()).MatchAll(new[] { row }, invoices).Single();

        result.Status.Should().Be(MatchStatus.AutoMatch);
        result.ConfidenceScore.Should().BeGreaterThanOrEqualTo(95m);
    }

    [Fact]
    public void AliasResolution_TreatsSynonymsAsIdenticalProduct()
    {
        var invoices = new[] { Invoice("INV-1001", "FR", new ParsedInvoiceLine("JUP VP", 60m, null, null, "", 0, null)) };
        var row = Row(0, "INV-1001", "JUP", "S101", 60m, "M1", "FR");

        var result = CreateService().MatchAll(new[] { row }, invoices).Single();

        result.ScoreBreakdown!.AliasResolved.Should().BeTrue();
        result.ScoreBreakdown.DescriptionMatch.Should().BeTrue();
    }

    [Fact]
    public void MissingExciseCode_ForcesManualReview_RegardlessOfScore()
    {
        var invoices = new[] { Invoice("INV-1002", "DE", new ParsedInvoiceLine("Duvel 24x33", 48m, null, null, "", 0, null)) };
        var row = Row(0, "INV-1002", "Duvel 24x33", null, 48m, "M2", "DE"); // no excise code

        var result = CreateService().MatchAll(new[] { row }, invoices).Single();

        result.Status.Should().Be(MatchStatus.ManualReviewRequired);
        result.HardBlockReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void QuantityMismatch_EvenTiny_IsNotAMatch_NoToleranceAllowed()
    {
        var invoices = new[] { Invoice("INV-1001", "FR", new ParsedInvoiceLine("Jupiler 24x33", 120.001m, null, null, "", 0, null)) };
        var row = Row(0, "INV-1001", "JUPILER", "S101", 120m, "M1", "FR"); // 0.001 off

        var result = CreateService().MatchAll(new[] { row }, invoices).Single();

        result.ScoreBreakdown!.QuantityMatch.Should().BeFalse();
    }

    [Fact]
    public void NoMatchingInvoice_ScoresLow()
    {
        var invoices = new[] { Invoice("INV-1001", "FR", new ParsedInvoiceLine("Jupiler 24x33", 120m, null, null, "", 0, null)) };
        var row = Row(0, "INV-9999", "Nonexistent Beer", "S999", 10m, "M4", "NL");

        var result = CreateService().MatchAll(new[] { row }, invoices).Single();

        result.ConfidenceScore.Should().BeLessThan(80m);
    }

    [Fact]
    public void EmptyInvoicePool_YieldsNoMatch()
    {
        var row = Row(0, "INV-1001", "Jupiler", "S101", 10m, "M1", "FR");

        var result = CreateService().MatchAll(new[] { row }, Array.Empty<ParsedInvoice>()).Single();

        result.Status.Should().Be(MatchStatus.NoMatch);
        result.MatchedInvoice.Should().BeNull();
    }
}
