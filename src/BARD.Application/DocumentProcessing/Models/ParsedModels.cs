using BARD.Domain.Enums;

namespace BARD.Application.DocumentProcessing.Models;

/// <summary>
/// Ports core/models.py's InvoiceLine. Transient — never persisted
/// directly; the matching stage consumes these and the results are
/// written onto DossierLine / DossierDocument / ExtractedField.
/// </summary>
public record ParsedInvoiceLine(
    string ProductDescription,
    decimal? Quantity,
    string? Unit,
    decimal? UnitPrice,
    string RawTextSnippet,
    int LineIndex,
    int? SourcePage
);

/// <summary>Ports core/models.py's Invoice.</summary>
public record ParsedInvoice(
    string? InvoiceNumber,
    DateOnly? InvoiceDate,
    string? Customer,
    string? DeliveryAddress,
    string? DestinationCountry,
    IReadOnlyList<ParsedInvoiceLine> Lines,
    string SourceFile,
    ExtractionMethod ExtractionMethod,
    decimal ExtractionConfidence,
    IReadOnlyList<string> ExtractionWarnings,
    string RawText
);

/// <summary>Ports core/ingestion/ac4_parser.py's Ac4Declaration dataclass.</summary>
public record ParsedAc4Declaration(
    string? Mrn,
    DateOnly? Ac4Date,
    string? Consignee,
    string? ProductDescription,
    decimal? Quantity,
    string? ExciseCode,
    string SourceFile,
    ExtractionMethod ExtractionMethod,
    decimal ExtractionConfidence,
    IReadOnlyList<string> ExtractionWarnings,
    string RawText
);

/// <summary>
/// Result of the intrinsic document-kind classification stage.
///
/// DocumentKind describes what the document physically is.
/// DocumentType remains temporarily available for compatibility while
/// the existing processing pipeline is migrated.
/// </summary>
public sealed record DocumentClassificationResult
{
    public string FileName { get; }

    public DocumentKind DocumentKind { get; }

    public decimal Confidence { get; }

    public IReadOnlyList<string> Reasons { get; }

    /// <summary>
    /// Temporary compatibility property for code that still consumes
    /// the legacy DocumentType enum.
    /// </summary>
    public DocumentType DocumentType =>
        MapToLegacyDocumentType(DocumentKind);

    /// <summary>
    /// Preferred constructor for new classification code.
    /// </summary>
    public DocumentClassificationResult(
        string fileName,
        DocumentKind documentKind,
        decimal confidence,
        IReadOnlyList<string> reasons)
    {
        FileName = fileName;
        DocumentKind = documentKind;
        Confidence = confidence;
        Reasons = reasons;
    }

    /// <summary>
    /// Temporary compatibility constructor for code that still creates
    /// results with the legacy DocumentType enum.
    /// </summary>
    public DocumentClassificationResult(
        string fileName,
        DocumentType documentType,
        decimal confidence,
        IReadOnlyList<string> reasons)
        : this(
            fileName,
            MapFromLegacyDocumentType(documentType),
            confidence,
            reasons)
    {
    }

    private static DocumentType MapToLegacyDocumentType(
        DocumentKind documentKind)
    {
        return documentKind switch
        {
            DocumentKind.Invoice =>
                DocumentType.SalesInvoice,

            DocumentKind.Ac4Declaration =>
                DocumentType.Ac4Declaration,

            DocumentKind.EadEVadDocument =>
                DocumentType.EadEVadDocument,

            DocumentKind.CompanyExcelClaim =>
                DocumentType.CompanyExcelClaim,

            DocumentKind.SupportingEvidence =>
                DocumentType.SupportingEvidence,

            _ =>
                DocumentType.Unknown,
        };
    }

    private static DocumentKind MapFromLegacyDocumentType(
        DocumentType documentType)
    {
        return documentType switch
        {
            DocumentType.SalesInvoice =>
                DocumentKind.Invoice,

            DocumentType.Ac4Declaration =>
                DocumentKind.Ac4Declaration,

            DocumentType.EadEVadDocument =>
                DocumentKind.EadEVadDocument,

            DocumentType.CompanyExcelClaim =>
                DocumentKind.CompanyExcelClaim,

            DocumentType.SupportingEvidence =>
                DocumentKind.SupportingEvidence,

            _ =>
                DocumentKind.Unknown,
        };
    }
}

/// <summary>
/// Result of the contextual document-role classification stage.
///
/// DocumentRole describes the function fulfilled by the document within
/// the dossier. It must be determined after the intrinsic DocumentKind.
/// </summary>
public sealed record DocumentRoleClassificationResult(
    string FileName,
    DocumentRole DocumentRole,
    decimal Confidence,
    IReadOnlyList<string> Reasons
);

/// <summary>Ports core/ingestion/excel_reader.py's ExcelRow.</summary>
public record ParsedExcelClaimRow(
    int RowIndex,
    string? InvoiceNumber,
    string? ProductDescription,
    string? ExciseCode,
    decimal? Quantity,
    string? Mrn,
    string? DestinationCountry,
    IReadOnlyDictionary<string, string?> RawRow
);

/// <summary>
/// Per-page text + detected tables from classical PDF extraction. Ports
/// core/ingestion/pdf_reader.py's PageExtraction/PDFExtraction.
/// </summary>
public record PdfPageExtraction(
    int PageNumber,
    string Text,
    IReadOnlyList<IReadOnlyList<string>> TableRows
);

public record PdfExtractionResult(
    string SourceFile,
    IReadOnlyList<PdfPageExtraction> Pages)
{
    public string FullText =>
        string.Join("\n", Pages.Select(p => p.Text));

    public IReadOnlyList<string> PageTexts =>
        Pages.Select(p => p.Text).ToList();
}

/// <summary>Ports core/ingestion/ocr_detector.py's DocumentOCRAssessment.</summary>
public record PageOcrAssessment(
    int PageNumber,
    int TextCharCount,
    bool NeedsOcr
);

public record DocumentOcrAssessment(
    int TotalPages,
    IReadOnlyList<PageOcrAssessment> Pages)
{
    public bool AnyPageNeedsOcr =>
        Pages.Any(p => p.NeedsOcr);

    public IReadOnlyList<int> PagesNeedingOcr =>
        Pages
            .Where(p => p.NeedsOcr)
            .Select(p => p.PageNumber)
            .ToList();
}

/// <summary>Ports core/matching/scoring.py's ScoreBreakdown.</summary>
public record ScoreBreakdown(
    decimal InvoiceNumberScore,
    decimal QuantityScore,
    decimal ExciseCodeScore,
    decimal DescriptionScore,
    decimal DestinationCountryScore,
    bool InvoiceNumberMatch,
    bool QuantityMatch,
    bool ExciseCodeMatch,
    bool DescriptionMatch,
    bool DestinationCountryMatch,
    bool AliasResolved,
    string? AliasCanonicalName,
    IReadOnlyList<string> Notes
);

/// <summary>
/// Ports core/matching/matcher.py's MatchResult
/// (transient — pre-persistence).
/// </summary>
public record MatchResult(
    ParsedExcelClaimRow ExcelRow,
    ParsedInvoice? MatchedInvoice,
    ParsedInvoiceLine? MatchedLine,
    decimal ConfidenceScore,
    ScoreBreakdown? ScoreBreakdown,
    MatchStatus Status,
    string? HardBlockReason,
    IReadOnlyList<(string InvoiceSourceFile, decimal Score)> AlternativeCandidates
);