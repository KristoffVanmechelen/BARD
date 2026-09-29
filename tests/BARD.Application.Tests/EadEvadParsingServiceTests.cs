using BARD.Application.DocumentProcessing.Interfaces;
using BARD.Application.DocumentProcessing.Models;
using BARD.Infrastructure.DocumentProcessing;
using FluentAssertions;
using Xunit;

namespace BARD.Application.Tests;

public class EadEvadParsingServiceTests
{
    [Fact]
    public async Task RealStyleEvad_ExtractsArcRecordsQuantitiesAndMappings()
    {
        const string ocrText =
            "Elektronisch administratief document (e-VAD)\n" +
            "d.ARC\n26BEMTK79C9Q004IA3NP5\n" +
            "a.Lokaal referentienummer (LRN)\nretour-Belgie-2026-08\n" +
            "17.Hoofdgedeelte e-VAD\n" +
            "a.Unieke referentie record\n1\n" +
            "b.Code accijnsgoed\nW300 - Mousserende wijn en mousserende gegiste dranken\n" +
            "c.GN-code\n22041015\n" +
            "d.Hoeveelheid\n9\n" +
            "g.Alcoholgehalte\n14.00\n" +
            "17.Hoofdgedeelte e-VAD\n" +
            "a.Unieke referentie record\n2\n" +
            "b.Code accijnsgoed\nW300 - Mousserende wijn en mousserende gegiste dranken\n" +
            "c.GN-code\n22041098\n" +
            "d.Hoeveelheid\n31.500\n" +
            "g.Alcoholgehalte\n14.00\n" +
            "17.Hoofdgedeelte e-VAD\n" +
            "a.Unieke referentie record\n3\n" +
            "b.Code accijnsgoed\nW200 - Niet-mousserende wijn\n" +
            "c.GN-code\n22042113\n" +
            "d.Hoeveelheid\n9\n" +
            "g.Alcoholgehalte\n14.00\n";

        var sut =
            new EadEvadParsingService(
                new EmptyPdfReader(),
                new AllOcrDetector(),
                new FixedOcrService(ocrText),
                new DocumentReferenceResolverService(),
                new ExciseCodeMappingService());

        await using var stream =
            new MemoryStream(new byte[] { 1 });

        var result =
            await sut.ParseAsync(stream, "movement.pdf");

        result.Arc.Should().Be("26BEMTK79C9Q004IA3NP5");
        result.Records.Should().HaveCount(3);
        result.Records[0].BelgianExciseCode.Should().Be("S109");
        result.Records[0].QuantityLitres.Should().Be(9m);
        result.Records[1].QuantityLitres.Should().Be(31.5m);
        result.Records[2].BelgianExciseCode.Should().Be("S101");
        result.Records[2].QuantityLitres.Should().Be(9m);
    }

    private sealed class EmptyPdfReader : IPdfTextExtractionService
    {
        public Task<PdfExtractionResult> ExtractAsync(
            Stream pdfStream,
            string sourceFileName,
            CancellationToken ct = default)
            => Task.FromResult(
                new PdfExtractionResult(
                    sourceFileName,
                    new[]
                    {
                        new PdfPageExtraction(
                            0,
                            string.Empty,
                            Array.Empty<IReadOnlyList<string>>())
                    }));
    }

    private sealed class AllOcrDetector : IOcrDetectionService
    {
        public DocumentOcrAssessment AssessPages(
            IReadOnlyList<string> pageTexts)
            => new(
                1,
                new[]
                {
                    new PageOcrAssessment(0, 0, true)
                });
    }

    private sealed class FixedOcrService : IOcrService
    {
        private readonly string _text;

        public FixedOcrService(string text)
        {
            _text = text;
        }

        public Task<IReadOnlyDictionary<int, string>> OcrPagesAsync(
            Stream pdfStream,
            IReadOnlyList<int> pageNumbers,
            CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<int, string>>(
                new Dictionary<int, string>
                {
                    [0] = _text,
                });
    }
}
