using BARD.Application.DocumentProcessing.Models;

namespace BARD.Application.DocumentProcessing.Interfaces;

/// <summary>Ports core/ingestion/pdf_reader.py.</summary>
public interface IPdfTextExtractionService
{
    Task<PdfExtractionResult> ExtractAsync(
        Stream pdfStream,
        string sourceFileName,
        CancellationToken ct = default);
}

/// <summary>Ports core/ingestion/ocr_detector.py.</summary>
public interface IOcrDetectionService
{
    DocumentOcrAssessment AssessPages(
        IReadOnlyList<string> pageTexts);
}

/// <summary>Ports core/ingestion/ocr_engine.py.</summary>
public interface IOcrService
{
    Task<IReadOnlyDictionary<int, string>> OcrPagesAsync(
        Stream pdfStream,
        IReadOnlyList<int> pageNumbers,
        CancellationToken ct = default);
}

/// <summary>
/// Determines the intrinsic kind of a document from its contents.
/// Classification must never depend solely on the filename.
/// </summary>
public interface IDocumentClassifierService
{
    Task<DocumentClassificationResult> ClassifyAsync(
        Stream pdfStream,
        string fileName,
        CancellationToken ct = default);
}

/// <summary>
/// Determines the contextual role of a document within a dossier.
///
/// This stage runs after document-kind classification and may use the
/// parsed document contents together with the Excel refund claim.
/// </summary>
public interface IDocumentRoleClassifierService
{
    DocumentRoleClassificationResult ClassifyRole(
        DocumentClassificationResult classification,
        ParsedInvoice? invoice,
        ParsedAc4Declaration? ac4Declaration,
        IReadOnlyList<ParsedExcelClaimRow> excelRows);
}

/// <summary>Ports core/ingestion/invoice_parser.py.</summary>
public interface IInvoiceParsingService
{
    Task<ParsedInvoice> ParseAsync(
        Stream pdfStream,
        string fileName,
        CancellationToken ct = default);
}

/// <summary>Ports core/ingestion/ac4_parser.py.</summary>
public interface IAc4ParsingService
{
    Task<ParsedAc4Declaration> ParseAsync(
        Stream pdfStream,
        string fileName,
        CancellationToken ct = default);
}

/// <summary>Ports core/ingestion/excel_reader.py.</summary>
public interface IExcelClaimReaderService
{
    IReadOnlyList<ParsedExcelClaimRow> Read(
        Stream excelStream,
        string fileName);
}

/// <summary>Ports core/matching/alias_resolver.py.</summary>
public interface IAliasResolverService
{
    string? Resolve(string productDescription);

    bool SameProduct(
        string descriptionA,
        string descriptionB);
}

/// <summary>Ports core/matching/matcher.py + scoring.py.</summary>
public interface IMatchingService
{
    IReadOnlyList<MatchResult> MatchAll(
        IReadOnlyList<ParsedExcelClaimRow> excelRows,
        IReadOnlyList<ParsedInvoice> invoices);
}

/// <summary>Ports core/validation/export_check.py.</summary>
public interface IExportValidationService
{
    (
        Domain.Enums.ExportConfirmationStatus Status,
        string Notes
    ) CheckExport(MatchResult matchResult);
}

/// <summary>
/// Ports core/validation/mrn_validation.py — batch operation over all
/// lines sharing an MRN.
/// </summary>
public interface IMrnValidationService
{
    IReadOnlyDictionary<
        Guid,
        (
            Domain.Enums.MrnCumulativeStatus Status,
            string Notes,
            Domain.Enums.Ac4Status Ac4Status,
            string Ac4Notes,
            ParsedAc4Declaration? MatchedAc4
        )> Validate(
            IReadOnlyList<(
                Guid LineId,
                ParsedExcelClaimRow Row
            )> lines,
            IReadOnlyList<ParsedAc4Declaration> ac4Declarations);
}

/// <summary>
/// Ports core/validation/deadline_check.py — AC4 date versus refund
/// application date, never the current date.
/// </summary>
public interface IRefundDeadlineValidationService
{
    (
        Domain.Enums.Ac4Status Status,
        string Notes
    ) CheckDeadline(
        ParsedAc4Declaration ac4,
        DateOnly refundApplicationDate);
}