namespace BARD.Contracts.Dossiers;

public record DossierSummaryDto(
    Guid Id,
    string DossierReference,
    string CompanyName,
    DateOnly RefundApplicationDate,
    string Status,
    int TotalLines,
    int FlaggedLines,
    decimal? TotalCalculatedRefund
);

public record DossierListRequest(
    string? SearchText,
    string? Status,
    int Page = 1,
    int PageSize = 25
);

public record DossierListResultDto(
    IReadOnlyList<DossierSummaryDto> Dossiers,
    int TotalCount,
    int Page,
    int PageSize
);

public record DossierLineDto(
    Guid Id,
    int RowIndex,
    string? ClaimedInvoiceNumber,
    string? ClaimedProductDescription,
    string? ExciseCode,
    decimal? ClaimedQuantity,
    string? Mrn,
    string? ClaimedDestinationCountry,
    string MatchStatus,
    decimal ConfidenceScore,
    string? HardBlockReason,
    string? MatchExplanation,
    string ExportStatus,
    string? ExportCheckNotes,
    string MrnCumulativeStatus,
    string? MrnCumulativeNotes,
    string Ac4Status,
    string? Ac4Notes,
    string OfficerDecision,
    string? OfficerRemarks,
    string? ReviewedByDisplayName,
    DateTime? ReviewedAtUtc,
    decimal? CalculatedRefundAmount,
    string? CalculationNotes,
    bool RequiresManualReview
);

public record DossierDetailDto(
    Guid Id,
    string DossierReference,
    string CompanyName,
    string? CompanyEnterpriseNumber,
    DateOnly RefundApplicationDate,
    string Status,
    IReadOnlyList<DossierLineDto> Lines,
    IReadOnlyList<DossierDocumentDto> Documents
);

public record DossierDocumentDto(
    Guid Id,
    string OriginalFileName,
    string DocumentKind,
    decimal ClassificationConfidence,
    string ExtractionMethod,
    decimal ExtractionConfidence,
    bool OcrWasRequired,
    string? ExtractionWarnings
);

public record RecordOfficerDecisionRequest(
    Guid DossierLineId,
    string Decision,
    string? Remarks
);