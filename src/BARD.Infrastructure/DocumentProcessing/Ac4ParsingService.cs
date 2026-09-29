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

    private const string DateToken =
        @"[0-9]{1,2}[-/.][0-9]{1,2}[-/.][0-9]{2,4}";

    private static readonly Regex ValidationDatePattern = new(
        $@"Valideringsdatum\s*({DateToken})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PeriodPattern = new(
        $@"Aangifteperiode\s*Van\s*({DateToken})\s*Tot\s*({DateToken})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DeclarantPattern = new(
        @"Aangever\s*identificatie\s*([0-9]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PaymentTypePattern = new(
        @"Type\s*betaling\s*([A-Z0-9]+)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AccountPattern = new(
        @"Rekenings\S*\s*nummer\s*([A-Z0-9]+)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TotalAmountLabelPattern = new(
        @"Totaalbedrag\s*([\d.]+,\d{2})\s*€?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TotalAmountAfterAccountPattern = new(
        @"Rekenings\S*\s*nummer\s*[A-Z0-9]+\s*([\d.]+,\d{2})\s*€",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ArticleStartPattern = new(
        @"(?<!\d)(?<nr>\d{1,3})\s*(?<code>S\d{3})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ArticleBlockPattern = new(
        @"(?is)(?<nr>\d{1,3})\s*(?<code>S\d{3})\b(?<body>.*?)(?=\d{1,3}\s*S\d{3}\b|\z)",
        RegexOptions.Compiled);

    private static readonly Regex DatePattern = new(
        DateToken,
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ArticleDatesPattern = new(
        $@"(?<start>{DateToken})\s*(?<end>{DateToken})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex QuantityUnitPattern = new(
        @"(?<!\d)(?<qty>\d{1,9}(?:[.,]\d{1,4})?)\s*(?<unit>hl(?:°Plato)?)(?![A-Za-z])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AdditionalDescriptionAtEndPattern = new(
        @"\s+\b[A-Z]\d?\s*$",
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
            _ocrDetection.AssessPages(extraction.PageTexts);

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
                    .Select(p =>
                        ocr.TryGetValue(p.PageNumber, out var text)
                            ? p with { Text = text }
                            : p)
                    .ToList();

                method = ExtractionMethod.Ocr;

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

        var tableRows =
            pages.SelectMany(p => p.TableRows)
                .ToList();

        var tableText =
            string.Join(
                "\n",
                tableRows.Select(
                    row => string.Join(" ", row)));

        var primaryText =
            string.IsNullOrWhiteSpace(tableText)
                ? rawText
                : tableText;

        var references =
            _referenceResolver.ResolveAll(
                primaryText + "\n" + rawText,
                DocumentKind.Ac4Declaration);

        var drn =
            references.FirstOrDefault(
                r => r.Type == DocumentReferenceType.Drn)?.Value;

        var lrn =
            references.FirstOrDefault(
                r => r.Type == DocumentReferenceType.Lrn)?.Value;

        var mrn =
            references.FirstOrDefault(
                r => r.Type == DocumentReferenceType.Mrn)?.Value;

        var validationDate =
            ParseDate(
                CapturePreferred(
                    ValidationDatePattern,
                    tableText,
                    rawText));

        var periodSource =
            FirstMatchingText(
                PeriodPattern,
                tableText,
                rawText);

        var periodMatch =
            PeriodPattern.Match(periodSource ?? string.Empty);

        var periodStart =
            periodMatch.Success
                ? ParseDate(periodMatch.Groups[1].Value)
                : null;

        var periodEnd =
            periodMatch.Success
                ? ParseDate(periodMatch.Groups[2].Value)
                : null;

        var declarant =
            CapturePreferred(
                DeclarantPattern,
                tableText,
                rawText);

        var paymentType =
            CapturePreferred(
                PaymentTypePattern,
                tableText,
                rawText);

        var account =
            CapturePreferred(
                AccountPattern,
                tableText,
                rawText);

        var total =
            ParseBelgianDecimal(
                CapturePreferred(
                    TotalAmountLabelPattern,
                    tableText,
                    rawText)
                ?? CapturePreferred(
                    TotalAmountAfterAccountPattern,
                    tableText,
                    rawText));

        var structuredArticles =
            ParseArticlesFromTableRows(tableRows);

        var articles =
            (structuredArticles.Count > 0
                ? structuredArticles
                : ParseArticlesFromText(primaryText))
            .GroupBy(a => (a.ArticleNumber, a.ExciseCode))
            .Select(g => g.First())
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

        if (articles.Length == 1)
        {
            legacyDescription = articles[0].Description;
            legacyQuantity = articles[0].TaxBase;
            legacyCode = articles[0].ExciseCode;
        }

        var signals =
            new[]
            {
                drn is not null || mrn is not null,
                validationDate is not null,
                periodStart is not null && periodEnd is not null,
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

    private static IReadOnlyList<ParsedAc4Article>
        ParseArticlesFromTableRows(
            IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var result =
            new List<ParsedAc4Article>();

        for (var i = 0; i < rows.Count; i++)
        {
            var startLine =
                NormalizeWhitespace(
                    string.Join(" ", rows[i]));

            var startMatch =
                ArticleStartPattern.Match(startLine);

            if (!startMatch.Success)
                continue;

            if (!int.TryParse(
                    startMatch.Groups["nr"].Value,
                    out var articleNumber))
            {
                continue;
            }

            var code =
                startMatch.Groups["code"].Value.ToUpperInvariant();

            var blockLines =
                new List<string> { startLine };

            var j = i + 1;

            while (j < rows.Count)
            {
                var candidate =
                    NormalizeWhitespace(
                        string.Join(" ", rows[j]));

                if (ArticleStartPattern.IsMatch(candidate))
                    break;

                blockLines.Add(candidate);
                j++;
            }

            var firstLineDates =
                DatePattern.Matches(startLine);

            string? description = null;
            decimal? taxBase = null;
            string? unit = null;

            if (firstLineDates.Count >= 2)
            {
                var descriptionStart =
                    startMatch.Index + startMatch.Length;

                var descriptionEnd =
                    firstLineDates[0].Index;

                if (descriptionEnd > descriptionStart)
                {
                    description =
                        CleanDescription(
                            startLine[
                                descriptionStart..descriptionEnd]);
                }

                var afterDates =
                    startLine[
                        (firstLineDates[1].Index
                         + firstLineDates[1].Length)..];

                var quantityMatch =
                    QuantityUnitPattern.Match(afterDates);

                if (quantityMatch.Success)
                {
                    taxBase =
                        ParseBelgianDecimal(
                            quantityMatch.Groups["qty"].Value);

                    unit =
                        quantityMatch.Groups["unit"].Value;
                }
            }

            var descriptionParts =
                new List<string>();

            if (!string.IsNullOrWhiteSpace(description))
                descriptionParts.Add(description);

            foreach (var continuation in blockLines.Skip(1))
            {
                var quantityMatch =
                    QuantityUnitPattern.Match(continuation);

                if (!quantityMatch.Success)
                    continue;

                if (taxBase is null)
                {
                    taxBase =
                        ParseBelgianDecimal(
                            quantityMatch.Groups["qty"].Value);

                    unit =
                        quantityMatch.Groups["unit"].Value;
                }

                var prefix =
                    continuation[..quantityMatch.Index]
                        .Trim();

                if (prefix.Any(char.IsLetter))
                {
                    var clean =
                        CleanDescription(prefix);

                    if (!string.IsNullOrWhiteSpace(clean))
                        descriptionParts.Add(clean);
                }
            }

            var fullDescription =
                NormalizeWhitespace(
                    string.Join(
                        " ",
                        descriptionParts
                            .Where(p => !string.IsNullOrWhiteSpace(p))
                            .Distinct()));

            result.Add(
                new ParsedAc4Article(
                    articleNumber,
                    code,
                    string.IsNullOrWhiteSpace(fullDescription)
                        ? null
                        : fullDescription,
                    null,
                    taxBase,
                    unit));
        }

        return result;
    }

    private static IReadOnlyList<ParsedAc4Article>
        ParseArticlesFromText(string text)
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
                match.Groups["code"].Value.Trim();

            var body =
                match.Groups["body"].Value.Trim();

            var dates =
                ArticleDatesPattern.Match(body);

            if (!dates.Success)
                continue;

            var description =
                CleanDescription(
                    body[..dates.Index]);

            var afterDates =
                body[
                    (dates.Index + dates.Length)..];

            var quantityUnit =
                QuantityUnitPattern.Match(afterDates);

            decimal? taxBase = null;
            string? unit = null;

            if (quantityUnit.Success)
            {
                taxBase =
                    ParseBelgianDecimal(
                        quantityUnit.Groups["qty"].Value);

                unit =
                    quantityUnit.Groups["unit"].Value;
            }
            else
            {
                var quantity =
                    DecimalPattern.Match(afterDates);

                var unitMatch =
                    UnitPattern.Match(afterDates);

                if (quantity.Success)
                {
                    taxBase =
                        ParseBelgianDecimal(
                            quantity.Groups[1].Value);
                }

                if (unitMatch.Success)
                    unit = unitMatch.Groups[1].Value;
            }

            result.Add(
                new ParsedAc4Article(
                    articleNumber,
                    code,
                    description,
                    null,
                    taxBase,
                    unit));
        }

        return result;
    }

    private static string CleanDescription(
        string value)
    {
        var normalized =
            NormalizeWhitespace(value);

        return AdditionalDescriptionAtEndPattern
            .Replace(normalized, string.Empty)
            .Trim();
    }

    private static string NormalizeWhitespace(
        string value)
        => Regex.Replace(
            value ?? string.Empty,
            @"\s+",
            " ").Trim();

    private static string? CapturePreferred(
        Regex regex,
        params string[] texts)
    {
        foreach (var text in texts)
        {
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var match =
                regex.Match(text);

            if (match.Success)
                return match.Groups[1].Value.Trim();
        }

        return null;
    }

    private static string? FirstMatchingText(
        Regex regex,
        params string[] texts)
        => texts.FirstOrDefault(
            text =>
                !string.IsNullOrWhiteSpace(text)
                && regex.IsMatch(text));

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
