using System.Globalization;
using System.Text.RegularExpressions;
using BARD.Application.DocumentProcessing.Interfaces;
using BARD.Application.DocumentProcessing.Models;
using BARD.Domain.Enums;

namespace BARD.Infrastructure.DocumentProcessing;

public class Ac4ParsingService : IAc4ParsingService
{
    private readonly IPdfTextExtractionService _pdfTextExtraction;
    private readonly IOcrDetectionService _ocrDetection;
    private readonly IOcrService _ocrService;
    private readonly IDocumentReferenceResolver _referenceResolver;

    public Ac4ParsingService(
        IPdfTextExtractionService pdfTextExtraction,
        IOcrDetectionService ocrDetection,
        IOcrService ocrService,
        IDocumentReferenceResolver referenceResolver)
    {
        _pdfTextExtraction = pdfTextExtraction;
        _ocrDetection = ocrDetection;
        _ocrService = ocrService;
        _referenceResolver = referenceResolver;
    }

    private static readonly Regex ValidationDatePattern = new(
        @"Valideringsdatum\s*([0-9]{1,2}[-/.][0-9]{1,2}[-/.][0-9]{2,4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PeriodPattern = new(
        @"Aangifteperiode\s*Van\s*([0-9]{1,2}[-/.][0-9]{1,2}[-/.][0-9]{2,4})\s*Tot\s*([0-9]{1,2}[-/.][0-9]{1,2}[-/.][0-9]{2,4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DeclarantPattern = new(
        @"Aangever\s*identificatie\s*([0-9]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PaymentTypePattern = new(
        @"Type\s*betaling\s*([A-Z0-9]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AccountPattern = new(
        @"Rekenings\S*\s*nummer\s*([A-Z0-9]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TotalAmountPattern = new(
        @"Rekenings\S*\s*nummer\s*[A-Z0-9]+\s*([\d.]+,\d{2})\s*€",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ArticleBlockPattern = new(
        @"(?is)(?<nr>\d{1,3})\s*(?<code>S\d{3})\b(?<body>.*?)(?=\d{1,3}\s*S\d{3}\b|\z)",
        RegexOptions.Compiled);

    private static readonly Regex ArticleDatesPattern = new(
        @"(?<start>[0-9]{1,2}[-/.][0-9]{1,2}[-/.][0-9]{2,4})\s*(?<end>[0-9]{1,2}[-/.][0-9]{1,2}[-/.][0-9]{2,4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AdditionalDescriptionPattern = new(
        @"(?s)^(?<description>.*?)(?<additional>[A-Z]\d?)\s*$",
        RegexOptions.Compiled);

    private static readonly Regex DecimalPattern = new(
        @"(?<!\d)(\d{1,9}(?:[.,]\d{1,4})?)(?!\d)",
        RegexOptions.Compiled);

    private static readonly Regex UnitPattern = new(
        @"\b(hl(?:°Plato)?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<ParsedAc4Declaration> ParseAsync(
        Stream pdfStream,
        string fileName,
        CancellationToken ct = default)
    {
        var warnings = new List<string>();

        PdfExtractionResult extraction;
        try
        {
            extraction = await _pdfTextExtraction.ExtractAsync(
                pdfStream,
                fileName,
                ct);
        }
        catch (Exception ex)
        {
            return Empty(
                fileName,
                $"Could not open PDF: {ex.Message}");
        }

        var assessment =
            _ocrDetection.AssessPages(
                extraction.PageTexts);

        var method =
            ExtractionMethod.ClassicalTextExtraction;

        var pages =
            extraction.Pages.ToList();

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
                    .Select(
                        p => ocr.TryGetValue(
                            p.PageNumber,
                            out var t)
                            ? p with { Text = t }
                            : p)
                    .ToList();

                method =
                    ExtractionMethod.Ocr;

                warnings.Add(
                    $"OCR applied to page(s) {string.Join(",", assessment.PagesNeedingOcr)}.");
            }
            catch (Exception ex)
            {
                warnings.Add(
                    $"OCR required but failed: {ex.Message}");
            }
        }

        var rawText =
            string.Join(
                "\n",
                pages.Select(p => p.Text));

        var tableText =
            string.Join(
                "\n",
                pages.SelectMany(
                    p => p.TableRows.Select(
                        row => string.Join(" ", row))));

        // PdfPig's Page.Text can concatenate neighbouring positioned words.
        // Table reconstruction often preserves the missing boundaries, so use both.
        var searchText =
            string.IsNullOrWhiteSpace(tableText)
                ? rawText
                : rawText + "\n" + tableText;

        var references =
            _referenceResolver.ResolveAll(
                searchText,
                DocumentKind.Ac4Declaration);

        var drn =
            references
                .FirstOrDefault(
                    r => r.Type == DocumentReferenceType.Drn)
                ?.Value;

        var lrn =
            references
                .FirstOrDefault(
                    r => r.Type == DocumentReferenceType.Lrn)
                ?.Value;

        var mrn =
            references
                .FirstOrDefault(
                    r => r.Type == DocumentReferenceType.Mrn)
                ?.Value;

        var validationDate =
            ParseDate(
                Capture(
                    ValidationDatePattern,
                    searchText));

        var periodMatch =
            PeriodPattern.Match(searchText);

        var periodStart =
            periodMatch.Success
                ? ParseDate(
                    periodMatch.Groups[1].Value)
                : null;

        var periodEnd =
            periodMatch.Success
                ? ParseDate(
                    periodMatch.Groups[2].Value)
                : null;

        var declarant =
            Capture(
                DeclarantPattern,
                searchText);

        var paymentType =
            Capture(
                PaymentTypePattern,
                searchText);

        var account =
            Capture(
                AccountPattern,
                searchText);

        var total =
            ParseBelgianDecimal(
                Capture(
                    TotalAmountPattern,
                    searchText));

        var articles =
            ParseArticles(searchText)
                .GroupBy(a => (a.ArticleNumber, a.ExciseCode))
                .Select(g => g
                    .OrderByDescending(
                        a => a.TaxBase.HasValue)
                    .First())
                .OrderBy(a => a.ArticleNumber)
                .ToArray();

        if (drn is null && mrn is null)
        {
            warnings.Add(
                "Neither DRN nor MRN could be identified — manual review required.");
        }

        if (validationDate is null)
        {
            warnings.Add(
                "Validation date could not be identified.");
        }

        if (articles.Length == 0)
        {
            warnings.Add(
                "No AC4 article lines could be identified — manual review required.");
        }

        string? legacyDescription = null;
        decimal? legacyQuantity = null;
        string? legacyCode = null;

        // Never flatten a multi-article AC4 into one code/quantity.
        if (articles.Length == 1)
        {
            legacyDescription =
                articles[0].Description;

            legacyQuantity =
                articles[0].TaxBase;

            legacyCode =
                articles[0].ExciseCode;
        }

        var signals =
            new[]
            {
                drn is not null || mrn is not null,
                validationDate is not null,
                periodStart is not null
                    && periodEnd is not null,
                articles.Length > 0,
                lrn is not null,
            }
            .Count(x => x);

        return new ParsedAc4Declaration(
            mrn,
            validationDate,
            null,
            legacyDescription,
            legacyQuantity,
            legacyCode,
            fileName,
            method,
            signals / 5m,
            warnings,
            rawText,
            drn,
            lrn,
            periodStart,
            periodEnd,
            declarant,
            paymentType,
            account,
            total,
            articles);
    }

    private static IReadOnlyList<ParsedAc4Article> ParseArticles(
        string text)
    {
        var result =
            new List<ParsedAc4Article>();

        foreach (Match match in
                 ArticleBlockPattern.Matches(text))
        {
            if (!int.TryParse(
                    match.Groups["nr"].Value,
                    out var articleNumber))
            {
                continue;
            }

            var code =
                match.Groups["code"]
                    .Value
                    .Trim();

            var body =
                match.Groups["body"]
                    .Value
                    .Trim();

            var dates =
                ArticleDatesPattern.Match(body);

            if (!dates.Success)
                continue;

            var beforeDates =
                body[..dates.Index]
                    .Trim();

            string? description =
                beforeDates;

            string? additional =
                null;

            var additionalMatch =
                AdditionalDescriptionPattern.Match(
                    beforeDates);

            if (additionalMatch.Success)
            {
                description =
                    additionalMatch
                        .Groups["description"]
                        .Value
                        .Trim();

                additional =
                    additionalMatch
                        .Groups["additional"]
                        .Value
                        .Trim();
            }

            var afterDates =
                body[
                    (dates.Index + dates.Length)..];

            var quantityMatch =
                DecimalPattern.Match(afterDates);

            var taxBase =
                quantityMatch.Success
                    ? ParseBelgianDecimal(
                        quantityMatch
                            .Groups[1]
                            .Value)
                    : null;

            var unitMatch =
                UnitPattern.Match(afterDates);

            var unit =
                unitMatch.Success
                    ? unitMatch.Groups[1].Value
                    : null;

            result.Add(
                new ParsedAc4Article(
                    articleNumber,
                    code,
                    description,
                    additional,
                    taxBase,
                    unit));
        }

        return result;
    }

    private static string? Capture(
        Regex regex,
        string text)
    {
        var match =
            regex.Match(text);

        return match.Success
            ? match.Groups[1].Value.Trim()
            : null;
    }

    private static DateOnly? ParseDate(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string[] formats =
        {
            "d-M-yyyy",
            "dd-MM-yyyy",
            "d/M/yyyy",
            "dd/MM/yyyy",
            "d.M.yyyy",
            "dd.MM.yyyy",
        };

        return DateTime.TryParseExact(
            value,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? DateOnly.FromDateTime(date)
            : null;
    }

    private static decimal? ParseBelgianDecimal(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized =
            value
                .Replace(".", "")
                .Replace(",", ".");

        return decimal.TryParse(
            normalized,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var number)
            ? number
            : null;
    }

    private static ParsedAc4Declaration Empty(
        string fileName,
        string warning)
        => new(
            null,
            null,
            null,
            null,
            null,
            null,
            fileName,
            ExtractionMethod.ClassicalTextExtraction,
            0m,
            new[] { warning },
            "");
}
