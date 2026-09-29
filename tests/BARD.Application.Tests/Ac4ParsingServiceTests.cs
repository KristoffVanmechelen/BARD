using BARD.Application.DocumentProcessing.Interfaces;
using BARD.Application.DocumentProcessing.Models;
using BARD.Infrastructure.DocumentProcessing;
using FluentAssertions;
using Xunit;

namespace BARD.Application.Tests;

public class Ac4ParsingServiceTests
{
    [Fact]
    public async Task BelgianWeekAc4_ExtractsRealStructure()
    {
        const string text = """
AC4 AANGIFTE DRN 26BEAC4C5G2YU6XJ7B2S9 LRN Weekaccijns2026-34
Valideringsdatum
26-08-2026
Aangifteperiode
Van 17-08-2026 Tot 23-08-2026
Aangever
identificatie
0808065824
Type betaling FRCT Rekenings-nummer 340AB8902
6.233,60 €
3 S101
Niet-mousserende wijnen in wegwerpverpakking
B1 17-08-2026 23-08-2026
51,63
51,63
51,63
hl
hl
hl
100
200
500
4 S109 Mousserende wijnen in wegwerpverpakking B2 17-08-2026 23-08-2026
6,25
6,25
6,25
hl
hl
hl
100
200
500
""";
        var sut=new Ac4ParsingService(new PdfReader(text),new OcrDetector(),new OcrService());
        await using var stream=new MemoryStream(new byte[]{1});
        var r=await sut.ParseAsync(stream,"AC4.pdf");
        r.Drn.Should().Be("26BEAC4C5G2YU6XJ7B2S9");
        r.Mrn.Should().BeNull();
        r.Lrn.Should().Be("Weekaccijns2026-34");
        r.Ac4Date.Should().Be(new DateOnly(2026,8,26));
        r.PeriodStart.Should().Be(new DateOnly(2026,8,17));
        r.PeriodEnd.Should().Be(new DateOnly(2026,8,23));
        r.TotalAmount.Should().Be(6233.60m);
        r.Articles.Should().HaveCount(2);
        r.Articles![0].ExciseCode.Should().Be("S101");
        r.Articles[0].TaxBase.Should().Be(51.63m);
        r.Articles[1].ExciseCode.Should().Be("S109");
        r.Articles[1].TaxBase.Should().Be(6.25m);
        r.Quantity.Should().BeNull();
        r.ExciseCode.Should().BeNull();
        r.ExtractionWarnings.Should().BeEmpty();
    }

    private sealed class PdfReader(string text):IPdfTextExtractionService
    {
        public Task<PdfExtractionResult> ExtractAsync(Stream s,string name,CancellationToken ct=default)=>Task.FromResult(new PdfExtractionResult(name,new[]{new PdfPageExtraction(1,text,Array.Empty<IReadOnlyList<string>>())}));
    }
    private sealed class OcrDetector:IOcrDetectionService
    {
        public DocumentOcrAssessment AssessPages(IReadOnlyList<string> texts)=>new(texts.Count,texts.Select((t,i)=>new PageOcrAssessment(i+1,t.Length,false)).ToArray());
    }
    private sealed class OcrService:IOcrService
    {
        public Task<IReadOnlyDictionary<int,string>> OcrPagesAsync(Stream s,IReadOnlyList<int> p,CancellationToken ct=default)=>Task.FromResult<IReadOnlyDictionary<int,string>>(new Dictionary<int,string>());
    }
}
