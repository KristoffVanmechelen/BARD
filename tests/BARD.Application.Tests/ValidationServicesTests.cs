using BARD.Application.Common.Options;
using BARD.Application.DocumentProcessing.Models;
using BARD.Domain.Enums;
using BARD.Infrastructure.DocumentProcessing;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BARD.Application.Tests;

public class ExportValidationServiceTests
{
    private static ParsedExcelClaimRow Row(string? country) =>
        new(0, "INV-1", "Product", "S101", 10m, "MRN1", country, new Dictionary<string, string?>());

    private static ParsedInvoice Invoice(string? country) =>
        new("INV-1", null, null, null, country, Array.Empty<ParsedInvoiceLine>(), "f.pdf",
            ExtractionMethod.ClassicalTextExtraction, 1m, Array.Empty<string>(), "");

    [Fact]
    public void ForeignDestination_IsConfirmed()
    {
        var match = new MatchResult(Row("FR"), Invoice("FR"), null, 100m, null, MatchStatus.AutoMatch, null, Array.Empty<(string, decimal)>());
        var (status, _) = new ExportValidationService().CheckExport(match);
        status.Should().Be(ExportConfirmationStatus.Confirmed);
    }

    [Fact]
    public void DomesticBelgiumDelivery_IsNotConfirmed()
    {
        var match = new MatchResult(Row("BE"), Invoice("BE"), null, 100m, null, MatchStatus.AutoMatch, null, Array.Empty<(string, decimal)>());
        var (status, _) = new ExportValidationService().CheckExport(match);
        status.Should().Be(ExportConfirmationStatus.NotConfirmed);
    }

    [Fact]
    public void UnknownDestination_IsUncertain()
    {
        var match = new MatchResult(Row(null), Invoice(null), null, 0m, null, MatchStatus.NoMatch, null, Array.Empty<(string, decimal)>());
        var (status, _) = new ExportValidationService().CheckExport(match);
        status.Should().Be(ExportConfirmationStatus.Uncertain);
    }
}

public class MrnValidationServiceTests
{
    [Fact]
    public void CumulativeQuantityExceedsAc4_MarksExceeded_NoTolerance()
    {
        var line1 = Guid.NewGuid();
        var line2 = Guid.NewGuid();
        var rows = new List<(Guid, ParsedExcelClaimRow)>
        {
            (line1, new ParsedExcelClaimRow(0, "INV-A", "Jupiler", "S101", 100m, "MRN1", "DE", new Dictionary<string, string?>())),
            (line2, new ParsedExcelClaimRow(1, "INV-B", "Jupiler", "S101", 80m, "MRN1", "DE", new Dictionary<string, string?>())),
        };
        var ac4 = new ParsedAc4Declaration("MRN1", DateOnly.FromDateTime(DateTime.Today), "Consignee", null, 150m, "S101",
            "ac4.pdf", ExtractionMethod.ClassicalTextExtraction, 1m, Array.Empty<string>(), "");

        var results = new MrnValidationService().Validate(rows, new[] { ac4 });

        results[line1].Status.Should().Be(MrnCumulativeStatus.Exceeded);
        results[line2].Status.Should().Be(MrnCumulativeStatus.Exceeded);
    }

    [Fact]
    public void CumulativeQuantityWithinAc4_MarksWithinLimit()
    {
        var line1 = Guid.NewGuid();
        var rows = new List<(Guid, ParsedExcelClaimRow)>
        {
            (line1, new ParsedExcelClaimRow(0, "INV-A", "Jupiler", "S101", 100m, "MRN1", "DE", new Dictionary<string, string?>())),
        };
        var ac4 = new ParsedAc4Declaration("MRN1", DateOnly.FromDateTime(DateTime.Today), "Consignee", null, 200m, "S101",
            "ac4.pdf", ExtractionMethod.ClassicalTextExtraction, 1m, Array.Empty<string>(), "");

        var results = new MrnValidationService().Validate(rows, new[] { ac4 });

        results[line1].Status.Should().Be(MrnCumulativeStatus.WithinLimit);
    }

    [Fact]
    public void MissingAc4ForMrn_IsUncertain()
    {
        var line1 = Guid.NewGuid();
        var rows = new List<(Guid, ParsedExcelClaimRow)>
        {
            (line1, new ParsedExcelClaimRow(0, "INV-A", "Jupiler", "S101", 100m, "MRN1", "DE", new Dictionary<string, string?>())),
        };

        var results = new MrnValidationService().Validate(rows, Array.Empty<ParsedAc4Declaration>());

        results[line1].Status.Should().Be(MrnCumulativeStatus.Uncertain);
        results[line1].Ac4Status.Should().Be(Ac4Status.NotConfirmed);
    }
}

public class RefundDeadlineValidationServiceTests
{
    private static RefundDeadlineValidationService CreateService(int deadlineMonths = 12) =>
        new(Options.Create(new BusinessRulesOptions { RefundDeadlineMonths = deadlineMonths }));

    [Fact]
    public void WithinDeadline_IsConfirmed()
    {
        var ac4 = new ParsedAc4Declaration("MRN1", new DateOnly(2026, 1, 1), null, null, 100m, "S101", "f.pdf",
            ExtractionMethod.ClassicalTextExtraction, 1m, Array.Empty<string>(), "");

        var (status, _) = CreateService().CheckDeadline(ac4, new DateOnly(2026, 6, 1));

        status.Should().Be(Ac4Status.Confirmed);
    }

    [Fact]
    public void ExceedsDeadline_IsNotConfirmed()
    {
        var ac4 = new ParsedAc4Declaration("MRN1", new DateOnly(2024, 1, 1), null, null, 100m, "S101", "f.pdf",
            ExtractionMethod.ClassicalTextExtraction, 1m, Array.Empty<string>(), "");

        var (status, _) = CreateService().CheckDeadline(ac4, new DateOnly(2026, 6, 1));

        status.Should().Be(Ac4Status.NotConfirmed);
    }

    [Fact]
    public void DeadlineMeasuredAgainstApplicationDate_NotWallClockToday()
    {
        // AC4 from 2020, application also from 2020 -> must be Confirmed
        // even though "today" in this test run is years later.
        var ac4 = new ParsedAc4Declaration("MRN1", new DateOnly(2020, 1, 1), null, null, 100m, "S101", "f.pdf",
            ExtractionMethod.ClassicalTextExtraction, 1m, Array.Empty<string>(), "");

        var (status, _) = CreateService().CheckDeadline(ac4, new DateOnly(2020, 6, 1));

        status.Should().Be(Ac4Status.Confirmed);
    }

    [Fact]
    public void MissingAc4Date_IsUncertain()
    {
        var ac4 = new ParsedAc4Declaration("MRN1", null, null, null, 100m, "S101", "f.pdf",
            ExtractionMethod.ClassicalTextExtraction, 1m, Array.Empty<string>(), "");

        var (status, _) = CreateService().CheckDeadline(ac4, new DateOnly(2026, 6, 1));

        status.Should().Be(Ac4Status.Uncertain);
    }
}
