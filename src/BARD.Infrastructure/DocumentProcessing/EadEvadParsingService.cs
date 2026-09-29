using System.Globalization;
using System.Text.RegularExpressions;
using BARD.Application.DocumentProcessing.Interfaces;
using BARD.Application.DocumentProcessing.Models;
using BARD.Domain.Enums;

namespace BARD.Infrastructure.DocumentProcessing;

public sealed class EadEvadParsingService : IEadEvadParsingService
{
    private readonly IPdfTextExtractionService _pdfTextExtraction;
    private readonly IOcrDetectionService _ocrDetection;
    private readonly IOcrService _ocrService;
    private readonly IDocumentReferenceResolver _referenceResolver;
    private readonly IExciseCodeMappingService _mappingService;

    private static readonly Regex LrnPattern = new(
        @"(?:Lokaal\s+referentienummer\s*\(?LRN\)?|LRN)\s*[:.\-]?\s*([A-Z0-9][A-Z0-9/_\-.]{2,80})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ValidationDateTimePattern = new(
        @"Datum\s+en\s+tijdstip\s+geldigmaking\s+e-?VAD\s*[:.\-]?\s*([0-9]{1,2}[/-][0-9]{1,2}[/-][0-9]{2,4}\s+[0-9]{1,2}:[0-9]{2}(?::[0-9]{2})?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RecordNumberPattern = new(
        @"Unieke\s+referentie\s+record[^\d]{0,30}(?<number>\d{1,4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ExciseCodePattern = new(
        @"Code\s+accijnsgoed\s*[:.\-]?\s*(?<code>[A-Z]\d{3})\b(?<description>.*?)(?=(?:GN-?code|Hoeveelheid|Bruto\s+massa|Netto\s+massa|Alcoholgehalte|Graden\s+Plato|$))",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ExciseCodeAnchorPattern = new(
        @"Code\s+accijnsgoed\s*[:.\-]?\s*(?<code>[A-Z]\d{3})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex QuantityPattern = new(
        @"Hoeveelheid\s*[:.\-]?\s*(?<value>\d{1,9}(?:[.,]\d{1,6})?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CnCodePattern = new(
        @"GN-?code\s*[:.\-]?\s*(?<value>\d{6,10})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AlcoholPattern = new(
        @"Alcoholgehalte\s*[:.\-]?\s*(?<value>\d{1,3}(?:[.,]\d{1,4})?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PlatoPattern = new(
        @"Graden\s+Plato\s*[:.\-]?\s*(?<value>\d{1,3}(?:[.,]\d{1,4})?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FallbackCodeQuantityPattern = new(
        @"Code\s+accijnsgoed\s*[:.\-]?\s*(?<code>[A-Z]\d{3})\b(?<description>.*?)(?=Hoeveelheid)\s*Hoeveelheid\s*[:.\-]?\s*(?<quantity>\d{1,9}(?:[.,]\d{1,6})?)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public EadEvadParsingService(
        IPdfTextExtractionService pdfTextExtraction,
        IOcrDetectionService ocrDetection,
        IOcrService ocrService,
        IDocumentReferenceResolver referenceResolver,
        IExciseCodeMappingService mappingService)
    {
        _pdfTextExtraction = pdfTextExtraction;
        _ocrDetection = ocrDetection;
        _ocrService = ocrService;
        _referenceResolver = referenceResolver;
        _mappingService = mappingService;
    }

    public async Task<ParsedMovementDocument> ParseAsync(
        Stream pdfStream,
        string fileName,
        CancellationToken ct = default)
    {
        var warnings = new List<string>();

        PdfExtractionResult extraction;
        try
        {
            extraction = await _pdfTextExtraction.ExtractAsync(pdfStream, fileName, ct);
        }
        catch (Exception ex)
        {
            return Empty(fileName, $"Could not open movement PDF: {ex.Message}");
        }

        var pages = extraction.Pages.ToList();
        var assessment = _ocrDetection.AssessPages(extraction.PageTexts);
        var method = ExtractionMethod.ClassicalTextExtraction;

        if (assessment.AnyPageNeedsOcr)
        {
            try
            {
                pdfStream.Position = 0;

                var ocr =
                    await _ocrService.OcrPagesAsync(
                        pdfStream,
                        assessment.PagesNeedingOcr,
                        ct);

                pages = pages
                    .Select(page =>
                        ocr.TryGetValue(page.PageNumber, out var ocrText)
                            ? page with { Text = ocrText }
                            : page)
                    .ToList();

                method = ExtractionMethod.Ocr;
                warnings.Add(
                    $"OCR applied to page(s) {string.Join(",", assessment.PagesNeedingOcr)}.");
            }
            catch (Exception ex)
            {
                warnings.Add($"OCR required but failed: {ex.Message}");
            }
        }

        var rawText = string.Join("\n\n", pages.Select(p => p.Text));

        var references =
            _referenceResolver.ResolveAll(
                rawText,
                DocumentKind.EadEVadDocument);

        var arc =
            references.FirstOrDefault(
                r => r.Type == DocumentReferenceType.Arc)?.Value;

        var lrn = Capture(LrnPattern, rawText);
        var validationDateTime = Capture(ValidationDateTimePattern, rawText);
        var movementDocumentType = DetectMovementDocumentType(rawText);
        var records = ParseRecords(rawText);

        if (string.IsNullOrWhiteSpace(arc))
            warnings.Add("ARC could not be identified — manual review required.");

        if (records.Count == 0)
            warnings.Add("No e-AD/e-VAD goods records could be identified — manual review required.");

        var unmapped =
            records.Count(r => string.IsNullOrWhiteSpace(r.BelgianExciseCode));

        if (unmapped > 0)
        {
            warnings.Add(
                $"{unmapped} movement record(s) have no deterministic Belgian S-code mapping; raw EMCS codes were retained.");
        }

        var signals =
            new[]
            {
                !string.IsNullOrWhiteSpace(arc),
                records.Count > 0,
                records.Count > 0 && records.All(r => !string.IsNullOrWhiteSpace(r.EmcsExciseCode)),
                records.Count > 0 && records.All(r => r.QuantityLitres.HasValue),
            }
            .Count(x => x);

        return new ParsedMovementDocument(
            arc,
            lrn,
            validationDateTime,
            movementDocumentType,
            records,
            fileName,
            method,
            signals / 4m,
            warnings,
            rawText);
    }

    private IReadOnlyList<ParsedMovementRecord> ParseRecords(
        string text)
    {
        var codeAnchors =
            ExciseCodeAnchorPattern.Matches(text)
                .Cast<Match>()
                .ToList();

        if (codeAnchors.Count == 0)
            return Array.Empty<ParsedMovementRecord>();

        var records =
            new List<ParsedMovementRecord>();

        for (var i = 0; i < codeAnchors.Count; i++)
        {
            var anchor = codeAnchors[i];

            var prefixStart =
                i == 0
                    ? Math.Max(0, anchor.Index - 500)
                    : codeAnchors[i - 1].Index
                      + codeAnchors[i - 1].Length;

            var prefix =
                text[prefixStart..anchor.Index];

            var blockEnd =
                i + 1 < codeAnchors.Count
                    ? codeAnchors[i + 1].Index
                    : text.Length;

            var block =
                text[anchor.Index..blockEnd];

            var recordNumber =
                ResolveRecordNumber(
                    prefix,
                    fallback: i + 1);

            var parsed =
                ParseRecordBlock(
                    recordNumber,
                    block);

            if (parsed is null)
                continue;

            records.Add(parsed);
        }

        return records
            .GroupBy(
                record =>
                    new
                    {
                        record.RecordNumber,
                        record.EmcsExciseCode,
                        record.QuantityLitres,
                    })
            .Select(group => group.First())
            .OrderBy(record => record.RecordNumber)
            .ToArray();
    }

    private static int ResolveRecordNumber(
        string block,
        int fallback)
    {
        var matches =
            RecordNumberPattern.Matches(block)
                .Cast<Match>()
                .ToList();

        for (var i = matches.Count - 1; i >= 0; i--)
        {
            if (int.TryParse(
                    matches[i].Groups["number"].Value,
                    out var parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }

    private static string? DetectMovementDocumentType(
        string text)
    {
        if (Regex.IsMatch(
                text,
                @"\be\s*[-–]?\s*VAD\b",
                RegexOptions.IgnoreCase)
            || text.Contains(
                "vereenvoudigd administratief document",
                StringComparison.OrdinalIgnoreCase))
        {
            return "e-VAD";
        }

        if (Regex.IsMatch(
                text,
                @"\be\s*[-–]?\s*AD\b",
                RegexOptions.IgnoreCase))
        {
            return "e-AD";
        }

        return null;
    }

    private ParsedMovementRecord? ParseRecordBlock(
        int recordNumber,
        string block)
    {
        var codeMatch = ExciseCodePattern.Match(block);

        if (!codeMatch.Success)
            return null;

        var code =
            codeMatch.Groups["code"].Value.Trim().ToUpperInvariant();

        var description =
            NormalizeDescription(codeMatch.Groups["description"].Value);

        var quantity =
            ParseDecimal(Capture(QuantityPattern, block, "value"));

        var cnCode =
            Capture(CnCodePattern, block, "value");

        var alcohol =
            ParseDecimal(Capture(AlcoholPattern, block, "value"));

        var plato =
            ParseDecimal(Capture(PlatoPattern, block, "value"));

        var mapping =
            _mappingService.Map(code, description, alcohol, plato);

        return new ParsedMovementRecord(
            recordNumber,
            code,
            mapping.BelgianExciseCode,
            quantity,
            "L",
            cnCode,
            alcohol,
            plato,
            description,
            mapping.Reason);
    }

    private static string NormalizeDescription(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Regex.Replace(value, @"\s+", " ").Trim();

    private static string? Capture(
        Regex regex,
        string text,
        string group = "1")
    {
        var match = regex.Match(text);

        if (!match.Success)
            return null;

        return group == "1"
            ? match.Groups[1].Value.Trim()
            : match.Groups[group].Value.Trim();
    }

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Replace(",", ".");

        return decimal.TryParse(
            normalized,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
    }

    private static ParsedMovementDocument Empty(
        string fileName,
        string warning)
        => new(
            null,
            null,
            null,
            null,
            Array.Empty<ParsedMovementRecord>(),
            fileName,
            ExtractionMethod.ClassicalTextExtraction,
            0m,
            new[] { warning },
            string.Empty);
}
