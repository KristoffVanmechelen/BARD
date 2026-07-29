using BARD.Application.DocumentProcessing.Interfaces;
using BARD.Application.DocumentProcessing.Models;
using BARD.Domain.Enums;

namespace BARD.Infrastructure.DocumentProcessing;

/// <summary>Direct port of core/ingestion/document_classifier.py.</summary>
public class DocumentClassifierService : IDocumentClassifierService
{
    private static readonly string[] InvoiceMarkers =
    {
        "invoice", "facture", "factuur", "bill to", "ship to", "customer",
        "invoice number", "invoice date",
    };

    private static readonly string[] Ac4Markers =
    {
        "mrn", "movement reference number", "ac4", "excise movement", "e-ad", "eadesc",
    };

    private readonly IPdfTextExtractionService _pdfTextExtraction;

    public DocumentClassifierService(IPdfTextExtractionService pdfTextExtraction) => _pdfTextExtraction = pdfTextExtraction;

    public static bool IsLikelyAc4(string text)
    {
        var lowered = text.ToLowerInvariant();
        return Ac4Markers.Any(lowered.Contains);
    }

    public async Task<DocumentClassificationResult> ClassifyAsync(Stream pdfStream, string fileName, CancellationToken ct = default)
    {
        PdfExtractionResult extraction;
        try
        {
            extraction = await _pdfTextExtraction.ExtractAsync(pdfStream, fileName, ct);
        }
        catch (Exception ex)
        {
            return new DocumentClassificationResult(fileName, DocumentType.Unknown, 0m,
                new[] { $"Could not open PDF: {ex.Message}" });
        }

        var text = extraction.FullText.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(text))
        {
            return new DocumentClassificationResult(fileName, DocumentType.Unknown, 0m,
                new[] { "No extractable text found (may require OCR before classification)." });
        }

        var ac4Hit = IsLikelyAc4(text);
        var invoiceHits = InvoiceMarkers.Where(text.Contains).ToList();

        if (ac4Hit && invoiceHits.Count == 0)
        {
            return new DocumentClassificationResult(fileName, DocumentType.Ac4Declaration, 0.8m,
                new[] { "Document contains AC4/MRN/excise-movement markers." });
        }

        if (invoiceHits.Count > 0 && !ac4Hit)
        {
            var confidence = Math.Min(0.5m + 0.1m * invoiceHits.Count, 0.9m);
            return new DocumentClassificationResult(fileName, DocumentType.SalesInvoice, confidence,
                new[] { $"Document contains invoice markers: {string.Join(", ", invoiceHits.Take(3))}" });
        }

        if (ac4Hit && invoiceHits.Count > 0)
        {
            return new DocumentClassificationResult(fileName, DocumentType.Unknown, 0.3m,
                new[] { "Document contains BOTH invoice and AC4 markers — ambiguous, needs manual classification." });
        }

        return new DocumentClassificationResult(fileName, DocumentType.Unknown, 0.1m,
            new[] { "No recognisable invoice or AC4 markers found." });
    }
}
