using BARD.Application.Common.Options;
using BARD.Application.DocumentProcessing.Interfaces;
using Microsoft.Extensions.Options;
using PDFtoImage;
using SkiaSharp;
using Tesseract;

namespace BARD.Infrastructure.DocumentProcessing;

/// <summary>
/// OCR fallback for scanned pages. Ports core/ingestion/ocr_engine.py:
/// renders the flagged page(s) to an image (PDFtoImage/PDFium — the
/// managed-code equivalent of the Python prototype's PyMuPDF/pdfplumber
/// rasterization) then runs Tesseract, matching the prototype's
/// "Tesseract via pytesseract" default engine exactly (core/config.py
/// OCR.engine == "tesseract").
/// </summary>
public class TesseractOcrService : IOcrService
{
    private readonly OcrOptions _options;

    public TesseractOcrService(IOptions<OcrOptions> options) => _options = options.Value;

    public Task<IReadOnlyDictionary<int, string>> OcrPagesAsync(Stream pdfStream, IReadOnlyList<int> pageNumbers, CancellationToken ct = default)
    {
        var results = new Dictionary<int, string>();

        using var engine = new TesseractEngine(_options.TessDataPath, _options.Language, EngineMode.Default);

        // PDFtoImage needs the stream position reset for each render call
        // since it re-reads the document; buffer to a byte array once.
        using var memoryStream = new MemoryStream();
        pdfStream.Position = 0;
        pdfStream.CopyTo(memoryStream);
        var pdfBytes = memoryStream.ToArray();

        foreach (var pageNumber in pageNumbers)
        {
            ct.ThrowIfCancellationRequested();

            using var bitmap = Conversion.ToImage(pdfBytes, page: pageNumber, options: new(Dpi: _options.Dpi));
            using var pngStream = new MemoryStream();
            bitmap.Encode(pngStream, SKEncodedImageFormat.Png, 100);
            pngStream.Position = 0;

            using var pix = Pix.LoadFromMemory(pngStream.ToArray());
            using var ocrPage = engine.Process(pix);
            results[pageNumber] = ocrPage.GetText();
        }

        return Task.FromResult<IReadOnlyDictionary<int, string>>(results);
    }
}
