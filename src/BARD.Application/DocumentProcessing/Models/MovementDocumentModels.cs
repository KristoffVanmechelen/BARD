using BARD.Domain.Enums;

namespace BARD.Application.DocumentProcessing.Models;

public sealed record ExciseCodeMappingResult(
    string? BelgianExciseCode,
    decimal Confidence,
    string Reason
);

public sealed record ParsedMovementRecord(
    int RecordNumber,
    string? EmcsExciseCode,
    string? BelgianExciseCode,
    decimal? QuantityLitres,
    string Unit,
    string? CnCode,
    decimal? AlcoholStrength,
    decimal? DegreesPlato,
    string? RawDescription,
    string MappingReason
);

public sealed record ParsedMovementDocument(
    string? Arc,
    string? Lrn,
    string? ValidationDateTime,
    string? MovementDocumentType,
    IReadOnlyList<ParsedMovementRecord> Records,
    string SourceFile,
    ExtractionMethod ExtractionMethod,
    decimal ExtractionConfidence,
    IReadOnlyList<string> ExtractionWarnings,
    string RawText
);
