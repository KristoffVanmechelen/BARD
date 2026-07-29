using BARD.Application.Common.Options;
using BARD.Application.DocumentProcessing.Interfaces;
using BARD.Application.DocumentProcessing.Models;
using Microsoft.Extensions.Options;

namespace BARD.Infrastructure.DocumentProcessing;

/// <summary>Direct port of core/ingestion/ocr_detector.py — identical threshold logic.</summary>
public class OcrDetectionService : IOcrDetectionService
{
    private readonly OcrOptions _options;

    public OcrDetectionService(IOptions<OcrOptions> options) => _options = options.Value;

    public DocumentOcrAssessment AssessPages(IReadOnlyList<string> pageTexts)
    {
        var pages = pageTexts.Select((text, idx) =>
        {
            var charCount = (text ?? string.Empty).Trim().Length;
            var needsOcr = charCount < _options.MinTextCharsPerPage;
            return new PageOcrAssessment(idx, charCount, needsOcr);
        }).ToList();

        return new DocumentOcrAssessment(pageTexts.Count, pages);
    }
}
