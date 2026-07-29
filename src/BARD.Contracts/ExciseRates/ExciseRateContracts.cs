namespace BARD.Contracts.ExciseRates;

public record ExciseRateDto(
    Guid Id,
    string ExciseCode,
    string Description,
    decimal CurrentRate,
    string CalculationUnit,
    DateOnly EffectiveFrom,
    bool IsActive,
    string? AdministrativeComment,
    DateTime? LastModifiedAtUtc
);

public record ExciseRateVersionDto(
    Guid Id,
    decimal Rate,
    string CalculationUnit,
    DateOnly EffectiveFrom,
    DateTime CreatedAtUtc,
    string CreatedByDisplayName
);

public record ExciseRateDetailDto(
    ExciseRateDto Rate,
    IReadOnlyList<ExciseRateVersionDto> VersionHistory
);

public record CreateExciseRateRequest(
    string ExciseCode,
    string Description,
    decimal InitialRate,
    string CalculationUnit,
    DateOnly EffectiveFrom,
    string? AdministrativeComment
);

public record PublishExciseRateVersionRequest(
    decimal Rate,
    string CalculationUnit,
    DateOnly EffectiveFrom
);

public record UpdateExciseRateDescriptionRequest(string Description);

public record ExciseRateAuditEntryDto(
    Guid Id,
    decimal? PreviousRate,
    decimal NewRate,
    string? PreviousUnit,
    string NewUnit,
    bool? PreviousActiveStatus,
    bool NewActiveStatus,
    string ChangedByDisplayName,
    DateTime ChangedAtUtc,
    string? Reason
);
