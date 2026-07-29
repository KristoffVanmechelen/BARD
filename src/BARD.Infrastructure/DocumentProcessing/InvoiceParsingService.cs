using System.Globalization;
using System.Text.RegularExpressions;
using BARD.Application.Common.Options;
using BARD.Application.DocumentProcessing.Interfaces;
using BARD.Application.DocumentProcessing.Models;
using BARD.Domain.Enums;
using Microsoft.Extensions.Options;

namespace BARD.Infrastructure.DocumentProcessing;

/// <summary>
/// Full port of core/ingestion/invoice_parser.py. Preserves every regex
/// pattern, the two-tier country name/code matching (fixing the
/// documented "de" false-positive bug found during prototype testing),
/// and the section-stop-label block truncation for customer/delivery
/// address extraction.
/// </summary>
public class InvoiceParsingService : IInvoiceParsingService
{
    private readonly IPdfTextExtractionService _pdfTextExtraction;
    private readonly IOcrDetectionService _ocrDetection;
    private readonly IOcrService _ocrService;
    private readonly OcrOptions _ocrOptions;

    public InvoiceParsingService(
        IPdfTextExtractionService pdfTextExtraction,
        IOcrDetectionService ocrDetection,
        IOcrService ocrService,
        IOptions<OcrOptions> ocrOptions)
    {
        _pdfTextExtraction = pdfTextExtraction;
        _ocrDetection = ocrDetection;
        _ocrService = ocrService;
        _ocrOptions = ocrOptions.Value;
    }

    private static readonly Regex InvoiceNumberPattern = new(
        @"(?:invoice\s*(?:no|number|nr|#)?[:\s]|facture\s*n[°o]?[:\s]|factuurnummer[:\s]|factuur\s*nr[:\s])\s*([A-Z0-9][A-Z0-9\-/\.]{2,})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DateLabelPattern = new(
        @"(?:invoice\s*date|date\s*(?:of\s*)?invoice|facture\s*date|factuurdatum|datum)[:\s]+" +
        @"([0-9]{1,2}[\/\-\.][0-9]{1,2}[\/\-\.][0-9]{2,4}|[0-9]{4}[\/\-\.][0-9]{1,2}[\/\-\.][0-9]{1,2}|[A-Za-z]+\s+[0-9]{1,2},?\s+[0-9]{4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FallbackDatePattern = new(
        @"\b([0-9]{1,2}[\/\-\.][0-9]{1,2}[\/\-\.][0-9]{2,4})\b", RegexOptions.Compiled);

    private static readonly Regex DeliveryAddressPattern = new(
        @"(?:ship\s*to|delivery\s*address|deliver\s*to|leveringsadres|adresse\s*de\s*livraison)[:\s]*\n?(.+?)(?:\n\s*\n|\z)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex CustomerPattern = new(
        @"(?:bill\s*to|customer|klant|client|facturer\s*a)[:\s]*\n?(.+?)(?:\n\s*\n|\z)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Dictionary<string, string> CountryFullNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["belgium"] = "BE", ["belgie"] = "BE", ["belgique"] = "BE",
        ["france"] = "FR",
        ["germany"] = "DE", ["deutschland"] = "DE",
        ["netherlands"] = "NL", ["nederland"] = "NL", ["holland"] = "NL",
        ["luxembourg"] = "LU", ["luxemburg"] = "LU",
        ["spain"] = "ES", ["espana"] = "ES",
        ["italy"] = "IT", ["italia"] = "IT",
        ["united kingdom"] = "GB", ["great britain"] = "GB",
        ["poland"] = "PL", ["polska"] = "PL",
        ["portugal"] = "PT",
        ["austria"] = "AT", ["osterreich"] = "AT",
        ["switzerland"] = "CH", ["suisse"] = "CH", ["schweiz"] = "CH",
        ["ireland"] = "IE",
        ["denmark"] = "DK", ["danmark"] = "DK",
        ["sweden"] = "SE", ["sverige"] = "SE",
        ["czech republic"] = "CZ", ["czechia"] = "CZ",
    };

    private static readonly HashSet<string> CountryIsoCodes = new()
    {
        "BE", "FR", "DE", "NL", "LU", "ES", "IT", "GB", "UK", "PL", "PT", "AT", "CH", "IE", "DK", "SE", "CZ",
    };

    private static readonly Regex CountryNamePattern = new(
        @"\b(" + string.Join("|", CountryFullNames.Keys.OrderByDescending(k => k.Length).Select(Regex.Escape)) + @")\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CountryCodePattern = new(
        @"(?:,|-|\n)\s*(" + string.Join("|", CountryIsoCodes.OrderByDescending(c => c.Length)) + @")\s*$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex SectionStopLabels = new(
        @"^\s*(?:ship\s*to|bill\s*to|deliver\s*to|delivery\s*address|leveringsadres|adresse\s*de\s*livraison|" +
        @"invoice\s*(?:no|number|date)|qty|quantity|description|product|total|subtotal)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<ParsedInvoice> ParseAsync(Stream pdfStream, string fileName, CancellationToken ct = default)
    {
        var warnings = new List<string>();

        PdfExtractionResult extraction;
        try
        {
            extraction = await _pdfTextExtraction.ExtractAsync(pdfStream, fileName, ct);
        }
        catch (Exception ex)
        {
            return new ParsedInvoice(null, null, null, null, null, Array.Empty<ParsedInvoiceLine>(), fileName,
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
                pages = pages.Select(p => ocrResults.TryGetValue(p.PageNumber, out var ocrText)
                    ? p with { Text = ocrText }
                    : p).ToList();
                method = ExtractionMethod.Ocr;
                warnings.Add($"OCR applied to page(s) {string.Join(",", ocrAssessment.PagesNeedingOcr)} " +
                             $"(low/no extractable text detected, engine={_ocrOptions.Engine}).");
            }
            catch (Exception ex)
            {
                warnings.Add($"OCR required but failed: {ex.Message}");
            }
        }

        var fullText = string.Join("\n", pages.Select(p => p.Text));
        var allTables = pages.SelectMany(p => p.TableRows).ToList();

        var invoiceNumber = ExtractInvoiceNumber(fullText);
        var invoiceDate = ExtractInvoiceDate(fullText);
        var customer = ExtractLabelledBlock(fullText, CustomerPattern);
        var deliveryAddress = ExtractLabelledBlock(fullText, DeliveryAddressPattern);
        var destinationCountry = ExtractDestinationCountry(fullText, deliveryAddress);
        var lines = ExtractProductLinesFromTables(allTables);

        if (invoiceNumber is null) warnings.Add("Invoice number could not be identified by classical parsing.");
        if (invoiceDate is null) warnings.Add("Invoice date could not be identified by classical parsing.");
        if (lines.Count == 0)
            warnings.Add("No product lines could be identified from tables in this PDF. The invoice may use a non-tabular layout — consider AI-assist.");
        if (destinationCountry is null) warnings.Add("Destination country could not be determined — flag for manual review.");

        var resolvedCount = new[] { invoiceNumber != null, invoiceDate != null, customer != null, deliveryAddress != null, lines.Count > 0 }
            .Count(x => x);
        var confidence = resolvedCount / 5m;

        return new ParsedInvoice(invoiceNumber, invoiceDate, customer, deliveryAddress, destinationCountry,
            lines, fileName, method, confidence, warnings, fullText);
    }

    private static string? ExtractInvoiceNumber(string text)
    {
        var match = InvoiceNumberPattern.Match(text);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static DateOnly? ExtractInvoiceDate(string text)
    {
        var match = DateLabelPattern.Match(text);
        var candidate = match.Success ? match.Groups[1].Value : null;

        if (candidate is null)
        {
            var fallback = FallbackDatePattern.Match(text.Length > 800 ? text[..800] : text);
            candidate = fallback.Success ? fallback.Groups[1].Value : null;
        }

        if (candidate is null) return null;

        string[] formats = { "d/M/yyyy", "d-M-yyyy", "d.M.yyyy", "dd/MM/yyyy", "dd-MM-yyyy", "yyyy/M/d", "yyyy-M-d", "MMMM d, yyyy", "MMMM d yyyy" };
        if (DateTime.TryParseExact(candidate, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
            return DateOnly.FromDateTime(exact);
        if (DateTime.TryParse(candidate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fuzzy))
            return DateOnly.FromDateTime(fuzzy);

        return null;
    }

    private static string? ExtractLabelledBlock(string text, Regex pattern)
    {
        var match = pattern.Match(text);
        if (!match.Success) return null;

        var block = match.Groups[1].Value.Trim();
        var lines = block.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

        var truncated = new List<string>();
        foreach (var line in lines.Take(8))
        {
            if (truncated.Count > 0 && SectionStopLabels.IsMatch(line))
                break;
            truncated.Add(line);
        }

        return truncated.Count > 0 ? string.Join("\n", truncated) : null;
    }

    private static string? ExtractDestinationCountry(string text, string? deliveryAddress)
    {
        var searchScope = deliveryAddress ?? text;

        var nameMatch = CountryNamePattern.Match(searchScope);
        if (nameMatch.Success)
            return CountryFullNames[nameMatch.Groups[1].Value.ToLowerInvariant()];

        var codeMatch = CountryCodePattern.Match(searchScope);
        if (codeMatch.Success)
            return codeMatch.Groups[1].Value;

        return null;
    }

    private static List<ParsedInvoiceLine> ExtractProductLinesFromTables(List<IReadOnlyList<string>> tables)
    {
        var lines = new List<ParsedInvoiceLine>();
        var lineIndex = 0;
        var numericPattern = new Regex(@"^-?[\d.,]+$", RegexOptions.Compiled);

        if (tables.Count < 2) return lines;

        var rows = tables.Skip(1).ToList();
        var colCount = tables.Max(r => r.Count);

        var numericScores = new int[colCount];
        foreach (var row in rows)
            for (var c = 0; c < row.Count && c < colCount; c++)
                if (numericPattern.IsMatch((row[c] ?? "").Trim()))
                    numericScores[c]++;

        if (rows.Count == 0 || numericScores.Max() == 0) return lines;

        var qtyCol = Array.IndexOf(numericScores, numericScores.Max());

        var textColLengths = new int[colCount];
        foreach (var row in rows)
            for (var c = 0; c < row.Count && c < colCount; c++)
                if (c != qtyCol && !string.IsNullOrEmpty(row[c]))
                    textColLengths[c] += row[c].Length;

        if (textColLengths.Max() == 0) return lines;
        var descCol = Array.IndexOf(textColLengths, textColLengths.Max());

        foreach (var row in rows)
        {
            if (qtyCol >= row.Count || descCol >= row.Count) continue;
            var qtyRaw = (row[qtyCol] ?? "").Trim().Replace(",", ".");
            var descRaw = (row[descCol] ?? "").Trim();
            if (string.IsNullOrEmpty(descRaw) || string.IsNullOrEmpty(qtyRaw)) continue;

            var cleaned = Regex.Replace(qtyRaw, @"[^\d.\-]", "");
            if (!decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var qty)) continue;

            lines.Add(new ParsedInvoiceLine(descRaw, qty, null, null,
                string.Join(" | ", row.Where(c => !string.IsNullOrEmpty(c))), lineIndex, null));
            lineIndex++;
        }

        return lines;
    }
}
