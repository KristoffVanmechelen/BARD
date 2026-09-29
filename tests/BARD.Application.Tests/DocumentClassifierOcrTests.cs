using BARD.Application.DocumentProcessing.Interfaces;
using BARD.Application.DocumentProcessing.Models;
using BARD.Domain.Enums;
using BARD.Infrastructure.DocumentProcessing;
using FluentAssertions;
using Xunit;

namespace BARD.Application.Tests;

public class DocumentClassifierOcrTests
{
    [Fact]
    public async Task ScannedEvad_IsClassifiedAfterOcr()
    {
        const string ocrText =
            "Elektronisch administratief document (e-VAD)\n" +
            "d.ARC 26BEMTK79C9Q004IA3NP5\n" +
            "17.Hoofdgedeelte e-VAD\n" +
            "a.Unieke referentie record 1\n" +
            "b.Code accijnsgoed W300\n" +
            "d.Hoeveelheid 9\n";

        var sut =
            new DocumentClassifierService(
                new EmptyPdfReader(),
                new AllOcrDetector(),
                new FixedOcrService(ocrText));

        await using var stream =
            new MemoryStream(new byte[] { 1 });

        var result =
            await sut.ClassifyAsync(stream, "scan.pdf");

        result.DocumentKind.Should().Be(DocumentKind.EadEVadDocument);
        result.Confidence.Should().BeGreaterThan(0.8m);
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
