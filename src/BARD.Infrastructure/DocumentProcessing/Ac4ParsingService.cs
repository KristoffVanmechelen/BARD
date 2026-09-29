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

    public Ac4ParsingService(IPdfTextExtractionService pdfTextExtraction, IOcrDetectionService ocrDetection, IOcrService ocrService)
    {
        _pdfTextExtraction = pdfTextExtraction;
        _ocrDetection = ocrDetection;
        _ocrService = ocrService;
    }

    private static readonly Regex DrnPattern = new(@"\bDRN\s+([0-9]{2}[A-Z]{2}[A-Z0-9]+)\b", RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static readonly Regex LrnPattern = new(@"\bLRN\s+([^\r\n]+)", RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static readonly Regex MrnPattern = new(@"(?:MRN|movement\s*reference\s*number)[:\s]*([0-9]{2}[A-Z]{2}[A-Z0-9]{12,20})", RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static readonly Regex ValidationDatePattern = new(@"Valideringsdatum\s*([0-9]{1,2}[-/.][0-9]{1,2}[-/.][0-9]{2,4})", RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static readonly Regex PeriodPattern = new(@"Aangifteperiode\s*Van\s*([0-9]{1,2}[-/.][0-9]{1,2}[-/.][0-9]{2,4})\s*Tot\s*([0-9]{1,2}[-/.][0-9]{1,2}[-/.][0-9]{2,4})", RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static readonly Regex DeclarantPattern = new(@"Aangever\s*identificatie\s*([0-9]+)", RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static readonly Regex PaymentTypePattern = new(@"Type\s*betaling\s*([A-Z0-9]+)", RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static readonly Regex AccountPattern = new(@"Rekenings\S*\s*nummer\s*([A-Z0-9]+)", RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static readonly Regex TotalAmountPattern = new(@"Rekenings\S*\s*nummer\s*[A-Z0-9]+\s*([\d.]+,\d{2})\s*€", RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static readonly Regex ArticleBlockPattern = new(@"(?ms)^\s*(?<nr>\d+)\s+(?<code>S\d{3})\b(?<body>.*?)(?=^\s*\d+\s+S\d{3}\b|\z)", RegexOptions.Compiled);
    private static readonly Regex ArticleDatesPattern = new(@"(?<start>[0-9]{1,2}[-/.][0-9]{1,2}[-/.][0-9]{2,4})\s+(?<end>[0-9]{1,2}[-/.][0-9]{1,2}[-/.][0-9]{2,4})", RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static readonly Regex AdditionalDescriptionPattern = new(@"(?s)^(?<description>.*?)(?<additional>[A-Z]\d?)\s*$", RegexOptions.Compiled);
    private static readonly Regex DecimalPattern = new(@"(?<!\d)(\d{1,9}(?:[.,]\d{1,4})?)(?!\d)", RegexOptions.Compiled);
    private static readonly Regex UnitPattern = new(@"\b(hl(?:°Plato)?)\b", RegexOptions.IgnoreCase|RegexOptions.Compiled);

    public async Task<ParsedAc4Declaration> ParseAsync(Stream pdfStream, string fileName, CancellationToken ct = default)
    {
        var warnings = new List<string>();
        PdfExtractionResult extraction;
        try { extraction = await _pdfTextExtraction.ExtractAsync(pdfStream,fileName,ct); }
        catch(Exception ex) { return Empty(fileName,$"Could not open PDF: {ex.Message}"); }

        var assessment=_ocrDetection.AssessPages(extraction.PageTexts);
        var method=ExtractionMethod.ClassicalTextExtraction;
        var pages=extraction.Pages.ToList();
        if(assessment.AnyPageNeedsOcr)
        {
            try
            {
                pdfStream.Position=0;
                var ocr=await _ocrService.OcrPagesAsync(pdfStream,assessment.PagesNeedingOcr,ct);
                pages=pages.Select(p=>ocr.TryGetValue(p.PageNumber,out var t)?p with {Text=t}:p).ToList();
                method=ExtractionMethod.Ocr;
                warnings.Add($"OCR applied to page(s) {string.Join(",",assessment.PagesNeedingOcr)}.");
            }
            catch(Exception ex){ warnings.Add($"OCR required but failed: {ex.Message}"); }
        }

        var text=string.Join("\n",pages.Select(p=>p.Text));
        var drn=Capture(DrnPattern,text);
        var lrn=Capture(LrnPattern,text);
        var mrn=Capture(MrnPattern,text);
        var validationDate=ParseDate(Capture(ValidationDatePattern,text));
        var pm=PeriodPattern.Match(text);
        var periodStart=pm.Success?ParseDate(pm.Groups[1].Value):null;
        var periodEnd=pm.Success?ParseDate(pm.Groups[2].Value):null;
        var declarant=Capture(DeclarantPattern,text);
        var paymentType=Capture(PaymentTypePattern,text);
        var account=Capture(AccountPattern,text);
        var total=ParseBelgianDecimal(Capture(TotalAmountPattern,text));
        var articles=ParseArticles(text);

        if(drn is null && mrn is null) warnings.Add("Neither DRN nor MRN could be identified — manual review required.");
        if(validationDate is null) warnings.Add("Validation date could not be identified.");
        if(articles.Count==0) warnings.Add("No AC4 article lines could be identified — manual review required.");

        string? legacyDescription=null; decimal? legacyQuantity=null; string? legacyCode=null;
        if(articles.Count==1){ legacyDescription=articles[0].Description; legacyQuantity=articles[0].TaxBase; legacyCode=articles[0].ExciseCode; }

        var signals=new[]{drn is not null||mrn is not null,validationDate is not null,periodStart is not null&&periodEnd is not null,articles.Count>0,lrn is not null}.Count(x=>x);
        return new ParsedAc4Declaration(mrn,validationDate,null,legacyDescription,legacyQuantity,legacyCode,fileName,method,signals/5m,warnings,text,drn,lrn,periodStart,periodEnd,declarant,paymentType,account,total,articles);
    }

    private static IReadOnlyList<ParsedAc4Article> ParseArticles(string text)
    {
        var result=new List<ParsedAc4Article>();
        foreach(Match m in ArticleBlockPattern.Matches(text))
        {
            if(!int.TryParse(m.Groups["nr"].Value,out var nr)) continue;
            var code=m.Groups["code"].Value.Trim();
            var body=m.Groups["body"].Value.Trim();
            var dm=ArticleDatesPattern.Match(body);
            if(!dm.Success) continue;
            var before=body[..dm.Index].Trim();
            string? description=before, additional=null;
            var am=AdditionalDescriptionPattern.Match(before);
            if(am.Success){ description=am.Groups["description"].Value.Trim(); additional=am.Groups["additional"].Value.Trim(); }
            var after=body[(dm.Index+dm.Length)..];
            var qm=DecimalPattern.Match(after);
            var taxBase=qm.Success?ParseBelgianDecimal(qm.Groups[1].Value):null;
            var um=UnitPattern.Match(after);
            var unit=um.Success?um.Groups[1].Value:null;
            result.Add(new ParsedAc4Article(nr,code,description,additional,taxBase,unit));
        }
        return result;
    }

    private static string? Capture(Regex r,string text){var m=r.Match(text);return m.Success?m.Groups[1].Value.Trim():null;}
    private static DateOnly? ParseDate(string? v)
    {
        if(string.IsNullOrWhiteSpace(v)) return null;
        string[] formats={"d-M-yyyy","dd-MM-yyyy","d/M/yyyy","dd/MM/yyyy","d.M.yyyy","dd.MM.yyyy"};
        return DateTime.TryParseExact(v,formats,CultureInfo.InvariantCulture,DateTimeStyles.None,out var d)?DateOnly.FromDateTime(d):null;
    }
    private static decimal? ParseBelgianDecimal(string? v)
    {
        if(string.IsNullOrWhiteSpace(v)) return null;
        var n=v.Replace(".","").Replace(",",".");
        return decimal.TryParse(n,NumberStyles.Number,CultureInfo.InvariantCulture,out var d)?d:null;
    }
    private static ParsedAc4Declaration Empty(string fileName,string warning)=>new(null,null,null,null,null,null,fileName,ExtractionMethod.ClassicalTextExtraction,0m,new[]{warning},"");
}
