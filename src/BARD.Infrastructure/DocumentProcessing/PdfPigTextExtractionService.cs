using BARD.Application.DocumentProcessing.Models;
using BARD.Application.DocumentProcessing.Interfaces;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace BARD.Infrastructure.DocumentProcessing;

/// <summary>
/// Classical (non-OCR) PDF text extraction via PdfPig. Ports
/// core/ingestion/pdf_reader.py.
///
/// Note on parity with the Python prototype: the prototype used
/// PyMuPDF for text and pdfplumber for table detection, preferring
/// whichever engine produced more text per page. PdfPig is the single
/// managed (no native binary) library covering both text extraction
/// and word/position data, so table detection here uses PdfPig's word
/// bounding boxes directly (see BorderlessTableExtractor) rather than
/// two separate engines. Text-extraction behaviour (page-by-page plain
/// text) is otherwise equivalent.
/// </summary>
public class PdfPigTextExtractionService : IPdfTextExtractionService
{
    public Task<PdfExtractionResult> ExtractAsync(Stream pdfStream, string sourceFileName, CancellationToken ct = default)
    {
        var pages = new List<PdfPageExtraction>();

        using var document = PdfDocument.Open(pdfStream);
        foreach (Page page in document.GetPages())
        {
            var text = page.Text ?? string.Empty;
            var tables = BorderlessTableExtractor.ExtractTables(page);

            pages.Add(new PdfPageExtraction(page.Number - 1, text, tables)); // 0-indexed, matching the Python prototype
        }

        return Task.FromResult(new PdfExtractionResult(sourceFileName, pages));
    }
}
