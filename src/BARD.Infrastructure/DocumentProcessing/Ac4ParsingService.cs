using System.Globalization;
using System.Text.RegularExpressions;
using BARD.Application.DocumentProcessing.Interfaces;
using BARD.Application.DocumentProcessing.Models;
using BARD.Domain.Enums;

namespace BARD.Infrastructure.DocumentProcessing;

/// <summary>Port of core/ingestion/ac4_parser.py.</summary>
public class Ac4ParsingService : IAc4ParsingService
{
    private readonly IPdfTextExtractionService _pdfTextExtraction;
    private readonly IOcrDetectionService _ocrDetection;
    private readonly IOcrService _ocrService;

    public Ac4ParsingService(IPdfTextExtractionService pdfTextExtraction, IOcrDetectionService ocrDetection, IOcrService ocrService)
    {
        _pdfTextExtraction = pdfTextExtraction;
        _ocrDetection = ocrDetection;
        _ocrService = ocrService;
    }

    private static readonly Regex MrnPattern = new(
        @"(?:MRN|movement\s*reference\s*number)[:\s]*([0-9]{2}[A-Z]{2}[A-Z0-9]{12,20})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MrnFallbackPattern = new(
        @"\b([0-9]{2}[A-Z]{2}[A-Z0-9]{12,20})\b", RegexOptions.Compiled);

    private static readonly Regex Ac4DateLabels = new(
        @"(?:date\s*of\s*(?:certification|discharge|receipt)|certification\s*date|discharge\s*date|" +
        @"datum\s*van\s*zuivering|date\s*de\s*decharge|ac4\s*date)[:\s]+" +
        @"([0-9]{1,2}[\/\-\.][0-9]{1,2}[\/\-\.][0-9]{2,4}|[0-9]{4}[\/\-\.][0-9]{1,2}[\/\-\.][0-9]{1,2})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ConsigneePattern = new(
        @"(?:consignee|geadresseerde|destinataire)[:\s]*\n?(.+?)(?:\n\s*\n|\z)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex QuantityPattern = new(
        @"(?:quantity|hoeveelheid|quantite)[:\s]+([\d.,]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ExciseCodePattern = new(
        @"(?:excise\s*code|excise\s*product\s*code|epc)[:\s]+([A-Z][0-9]{3})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<ParsedAc4Declaration> ParseAsync(Stream pdfStream, string fileName, CancellationToken ct = default)
    {
        var warnings = new List<string>();

        PdfExtractionResult extraction;
        try
        {
            extraction = await _pdfTextExtraction.ExtractAsync(pdfStream, fileName, ct);
        }
        catch (Exception ex)
        {
            return new ParsedAc4Declaration(null, null, null, null, null, null, fileName,
                ExtractionMethod.ClassicalTextExtraction, 0m, new[] { $"Could not open PDF: {ex.Message}" }, "");
        }

        var ocrAssessment = _ocrDetection.AssessPages(extraction.PageTexts);
        var method = ExtractionMethod.ClassicalTextExtraction;
        var pages = extraction.Pages.ToList();

        if (ocrAssessment.AnyPageNeedsOcr)
        {
            try
            {
                pdfStream.Position = 0;
                var ocrResults = await _ocrService.OcrPagesAsync(pdfStream, ocrAssessment.PagesNeedingOcr, ct);
                pages = pages.Select(p => ocrResults.TryGetValue(p.PageNumber, out var ocrText) ? p with { Text = ocrText } : p).ToList();
                method = ExtractionMethod.Ocr;
                warnings.Add($"OCR applied to page(s) {string.Join(",", ocrAssessment.PagesNeedingOcr)}.");
            }
            catch (Exception ex)
            {
                warnings.Add($"OCR required but failed: {ex.Message}");
            }
        }

        var fullText = string.Join("\n", pages.Select(p => p.Text));

        var mrnMatch = MrnPattern.Match(fullText);
        if (!mrnMatch.Success) mrnMatch = MrnFallbackPattern.Match(fullText);
        var mrn = mrnMatch.Success ? mrnMatch.Groups[1].Value.Trim() : null;
        if (mrn is null) warnings.Add("MRN could not be identified — manual review required.");

        DateOnly? ac4Date = null;
        var dateMatch = Ac4DateLabels.Match(fullText);
        if (dateMatch.Success)
        {
            var candidate = dateMatch.Groups[1].Value;
            string[] formats = { "d/M/yyyy", "d-M-yyyy", "d.M.yyyy", "dd/MM/yyyy", "yyyy/M/d", "yyyy-M-d" };
            if (DateTime.TryParseExact(candidate, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
                ac4Date = DateOnly.FromDateTime(exact);
            else if (DateTime.TryParse(candidate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fuzzy))
                ac4Date = DateOnly.FromDateTime(fuzzy);
        }
        if (ac4Date is null) warnings.Add("AC4 date (certification/discharge date) could not be identified.");

        string? consignee = null;
        var consigneeMatch = ConsigneePattern.Match(fullText);
        if (consigneeMatch.Success)
        {
            var lines = consigneeMatch.Groups[1].Value.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            consignee = lines.Count > 0 ? string.Join("\n", lines.Take(5)) : null;
        }

        decimal? quantity = null;
        var qtyMatch = QuantityPattern.Match(fullText);
        if (qtyMatch.Success && decimal.TryParse(qtyMatch.Groups[1].Value.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedQty))
            quantity = parsedQty;
        if (quantity is null) warnings.Add("Quantity could not be identified on the AC4 — manual review required.");

        var exciseMatch = ExciseCodePattern.Match(fullText);
        var exciseCode = exciseMatch.Success ? exciseMatch.Groups[1].Value.Trim() : null;

        var resolvedCount = new[] { mrn != null, ac4Date != null, consignee != null, quantity != null }.Count(x => x);
        var confidence = resolvedCount / 4m;

        return new ParsedAc4Declaration(mrn, ac4Date, consignee, null, quantity, exciseCode, fileName,
            method, confidence, warnings, fullText);
    }
}
