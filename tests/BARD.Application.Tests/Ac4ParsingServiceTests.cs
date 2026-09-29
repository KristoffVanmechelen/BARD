using BARD.Application.DocumentProcessing.Interfaces;
using BARD.Application.DocumentProcessing.Models;
using BARD.Infrastructure.DocumentProcessing;
using FluentAssertions;
using Xunit;

namespace BARD.Application.Tests;

public class Ac4ParsingServiceTests
{
    [Fact]
    public async Task BelgianWeekAc4_ExtractsRealStructure()
    {
        const string text =
            "AC4 AANGIFTE DRN 26BEAC4C5G2YU6XJ7B2S9 LRN Weekaccijns2026-34\n" +
            "Valideringsdatum 26-08-2026\n" +
            "Aangifteperiode Van 17-08-2026 Tot 23-08-2026\n" +
            "Aangever identificatie 0808065824\n" +
            "Type betaling FRCT Rekenings-nummer 340AB8902\n" +
            "Totaalbedrag 6.233,60 €\n" +
            "3 S101 Niet-mousserende wijnen in wegwerpverpakking B1 17-08-2026 23-08-2026 51,63 hl\n" +
            "4 S109 Mousserende wijnen in wegwerpverpakking B2 17-08-2026 23-08-2026 6,25 hl";

        var sut =
            new Ac4ParsingService(
                new PdfReader(text),
                new OcrDetector(),
                new OcrService(),
                new DocumentReferenceResolverService());

        await using var stream =
            new MemoryStream(new byte[] { 1 });

        var result =
            await sut.ParseAsync(
                stream,
                "AC4.pdf");

        result.Drn.Should().Be("26BEAC4C5G2YU6XJ7B2S9");
        result.Lrn.Should().Be("Weekaccijns2026-34");
        result.Ac4Date.Should().Be(new DateOnly(2026, 8, 26));
        result.TotalAmount.Should().Be(6233.60m);
        result.Articles.Should().HaveCount(2);
        result.Articles![0].TaxBase.Should().Be(51.63m);
        result.Articles[1].TaxBase.Should().Be(6.25m);
    }

    [Fact]
    public async Task BelgianWeekAc4_PrefersStructuredRows_AndRebuildsWrappedDescriptions()
    {
        const string rawText =
            "AC4AANGIFTEDRN26BEAC4C5G2YU6XJ7B2S9LRNWeekaccijns2026-34";

        IReadOnlyList<IReadOnlyList<string>> rows =
            new[]
            {
                Row("AC4 AANGIFTE", "DRN", "26BEAC4C5G2YU6XJ7B2S9", "LRN", "Weekaccijns2026-34"),
                Row("Valideringsdatum", "26-08-2026"),
                Row("Aangifteperiode", "Van", "17-08-2026", "Tot", "23-08-2026"),
                Row("Aangever identificatie", "0808065824", "Type betaling", "FRCT", "Rekenings-nummer", "340AB8902"),
                Row("Totaalbedrag", "6.233,60 €", "Aantal artikelen", "6"),

                Row("1", "S217", "Niet-mousserende", "A1", "17-08-2026", "23-08-2026", "1,17", "hl", "100"),
                Row("gegiste dranken =< 8,5 %", "1,17", "hl", "200"),
                Row("in wegwerpverpakking", "1,17", "hl", "500"),

                Row("2", "S223", "Mousserende gegiste", "A2", "17-08-2026", "23-08-2026", "2,52", "hl", "100"),
                Row("dranken =< 8,5 % in", "2,52", "hl", "200"),
                Row("wegwerpverpakking", "2,52", "hl", "500"),

                Row("3", "S101", "Niet-mousserende", "B1", "17-08-2026", "23-08-2026", "51,63", "hl", "100"),
                Row("wijnen in", "51,63", "hl", "200"),
                Row("wegwerpverpakking", "51,63", "hl", "500"),

                Row("4", "S109", "Mousserende wijnen in", "B2", "17-08-2026", "23-08-2026", "6,25", "hl", "100"),
                Row("wegwerpverpakking", "6,25", "hl", "200"),
                Row("6,25", "hl", "500"),

                Row("5", "S305", "Tussenproducten (niet-mousserend) in", "C", "17-08-2026", "23-08-2026", "0,14", "hl", "100"),
                Row("wegwerpverpakking", "0,14", "hl", "200"),
                Row("0,14", "hl", "500"),

                Row("6", "S301", "Tussenproducten (niet-mousserend) in", "E1", "17-08-2026", "23-08-2026", "0,27", "hl", "100"),
                Row("wegwerpverpakking", "0,27", "hl", "200"),
                Row("0,27", "hl", "500"),
            };

        var sut =
            new Ac4ParsingService(
                new PdfReader(rawText, rows),
                new OcrDetector(),
                new OcrService(),
                new DocumentReferenceResolverService());

        await using var stream =
            new MemoryStream(new byte[] { 1 });

        var result =
            await sut.ParseAsync(
                stream,
                "AC4 week 2026-34.pdf");

        result.PaymentType.Should().Be("FRCT");
        result.AccountNumber.Should().Be("340AB8902");
        result.TotalAmount.Should().Be(6233.60m);
        result.Articles.Should().HaveCount(6);

        result.Articles![0].Description.Should()
            .Be("Niet-mousserende gegiste dranken =< 8,5 % in wegwerpverpakking");

        result.Articles[2].Description.Should()
            .Be("Niet-mousserende wijnen in wegwerpverpakking");

        result.Articles[3].Description.Should()
            .Be("Mousserende wijnen in wegwerpverpakking");

        result.Articles[3].TaxBase.Should().Be(6.25m);
        result.Articles[3].Unit.Should().Be("hl");
    }

    private static IReadOnlyList<string> Row(
        params string[] cells)
        => cells;

    private sealed class PdfReader : IPdfTextExtractionService
    {
        private readonly string _text;
        private readonly IReadOnlyList<IReadOnlyList<string>> _rows;

        public PdfReader(
            string text,
            IReadOnlyList<IReadOnlyList<string>>? rows = null)
        {
            _text = text;
            _rows = rows ?? Array.Empty<IReadOnlyList<string>>();
        }

        public Task<PdfExtractionResult> ExtractAsync(
            Stream stream,
            string name,
            CancellationToken ct = default)
            => Task.FromResult(
                new PdfExtractionResult(
                    name,
                    new[]
                    {
                        new PdfPageExtraction(
                            1,
                            _text,
                            _rows)
                    }));
    }

    private sealed class OcrDetector : IOcrDetectionService
    {
        public DocumentOcrAssessment AssessPages(
            IReadOnlyList<string> texts)
            => new(
                texts.Count,
                texts.Select(
                    (text, index) =>
                        new PageOcrAssessment(
                            index + 1,
                            text.Length,
                            false))
                    .ToArray());
    }

    private sealed class OcrService : IOcrService
    {
        public Task<IReadOnlyDictionary<int, string>>
            OcrPagesAsync(
                Stream stream,
                IReadOnlyList<int> pages,
                CancellationToken ct = default)
            => Task.FromResult<
                IReadOnlyDictionary<int, string>>(
                    new Dictionary<int, string>());
    }
}
