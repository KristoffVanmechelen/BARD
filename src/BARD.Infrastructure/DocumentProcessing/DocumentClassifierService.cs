using BARD.Application.DocumentProcessing.Interfaces;
using BARD.Application.DocumentProcessing.Models;
using BARD.Domain.Enums;

namespace BARD.Infrastructure.DocumentProcessing;

public class DocumentClassifierService : IDocumentClassifierService
{
    private static readonly string[] InvoiceMarkers =
    {
        "invoice",
        "facture",
        "factuur",
        "bill to",
        "ship to",
        "customer",
        "invoice number",
        "invoice date",
    };

    private static readonly string[] Ac4Markers =
    {
        "ac4 aangifte",
        "valideringsdatum",
        "aangifteperiode",
        "type betaling",
        "drn",
    };

    private static readonly string[] MovementMarkers =
    {
        "elektronisch administratief document",
        "e-vad",
        "e-ad",
        "hoofdgedeelte e-vad",
        "code accijnsgoed",
        "unieke referentie record",
        "d.arc",
    };

    private readonly IPdfTextExtractionService _pdfTextExtraction;
    private readonly IOcrDetectionService _ocrDetection;
    private readonly IOcrService _ocrService;

    public DocumentClassifierService(
        IPdfTextExtractionService pdfTextExtraction,
        IOcrDetectionService ocrDetection,
        IOcrService ocrService)
    {
        _pdfTextExtraction = pdfTextExtraction;
        _ocrDetection = ocrDetection;
        _ocrService = ocrService;
    }

    public static bool IsLikelyAc4(string text)
    {
        var lowered = text.ToLowerInvariant();
        return Ac4Markers.Count(lowered.Contains) >= 2;
    }

    public static bool IsLikelyMovementDocument(string text)
    {
        var lowered = text.ToLowerInvariant();
        return MovementMarkers.Count(lowered.Contains) >= 2;
    }

    public async Task<DocumentClassificationResult> ClassifyAsync(
        Stream pdfStream,
        string fileName,
        CancellationToken ct = default)
    {
        PdfExtractionResult extraction;

        try
        {
            extraction =
                await _pdfTextExtraction.ExtractAsync(pdfStream, fileName, ct);
        }
        catch (Exception ex)
        {
            return Unknown(fileName, $"Could not open PDF: {ex.Message}");
        }

        var pageTexts =
            extraction.PageTexts
                .Select(t => t ?? string.Empty)
                .ToArray();

        var classificationTexts = pageTexts.ToArray();

        var assessment =
            _ocrDetection.AssessPages(pageTexts);

        var ocrUsed = false;
        string? ocrFailure = null;

        if (assessment.AnyPageNeedsOcr)
        {
            try
            {
                var pagesToOcr =
                    assessment.PagesNeedingOcr
                        .Take(2)
                        .ToArray();

                if (pagesToOcr.Length > 0)
                {
                    pdfStream.Position = 0;

                    var ocr =
                        await _ocrService.OcrPagesAsync(
                            pdfStream,
                            pagesToOcr,
                            ct);

                    foreach (var pair in ocr)
                    {
                        if (pair.Key >= 0 && pair.Key < classificationTexts.Length)
                            classificationTexts[pair.Key] = pair.Value ?? string.Empty;
                    }

                    ocrUsed = true;
                }
            }
            catch (Exception ex)
            {
                ocrFailure = ex.Message;
                // Continue with any classical text that was available.
            }
        }

        var text =
            string.Join("\n", classificationTexts)
                .ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(text))
        {
            return Unknown(
                fileName,
                ocrFailure is null
                    ? "No usable text found after classical extraction/OCR classification."
                    : $"OCR failed during classification: {ocrFailure}");
        }

        var movementHits =
            MovementMarkers.Where(text.Contains).Distinct().ToList();

        var ac4Hits =
            Ac4Markers.Where(text.Contains).Distinct().ToList();

        var invoiceHits =
            InvoiceMarkers.Where(text.Contains).Distinct().ToList();

        if (movementHits.Count >= 2
            && movementHits.Count >= ac4Hits.Count)
        {
            return new DocumentClassificationResult(
                fileName,
                DocumentKind.EadEVadDocument,
                ocrUsed ? 0.92m : 0.95m,
                new[]
                {
                    $"Document contains e-AD/e-VAD markers: {string.Join(", ", movementHits.Take(4))}."
                    + (ocrUsed ? " Classification used OCR." : string.Empty),
                });
        }

        if (ac4Hits.Count >= 2
            && ac4Hits.Count > movementHits.Count)
        {
            return new DocumentClassificationResult(
                fileName,
                DocumentKind.Ac4Declaration,
                ocrUsed ? 0.87m : 0.90m,
                new[]
                {
                    $"Document contains AC4 markers: {string.Join(", ", ac4Hits.Take(4))}."
                    + (ocrUsed ? " Classification used OCR." : string.Empty),
                });
        }

        if (invoiceHits.Count > 0
            && movementHits.Count == 0
            && ac4Hits.Count == 0)
        {
            var confidence =
                Math.Min(
                    0.5m + 0.1m * invoiceHits.Count,
                    0.9m);

            if (ocrUsed)
                confidence = Math.Min(confidence, 0.85m);

            return new DocumentClassificationResult(
                fileName,
                DocumentKind.Invoice,
                confidence,
                new[]
                {
                    "Document contains invoice markers: "
                    + string.Join(", ", invoiceHits.Take(3))
                    + (ocrUsed ? ". Classification used OCR." : "."),
                });
        }

        return Unknown(
            fileName,
            ocrFailure is null
                ? "Content did not establish a reliable document kind after available text/OCR analysis."
                : $"Content remained unclassified because OCR failed: {ocrFailure}");
    }

    private static DocumentClassificationResult Unknown(
        string fileName,
        string reason)
        => new(
            fileName,
            DocumentKind.Unknown,
            0.10m,
            new[] { reason });
}
