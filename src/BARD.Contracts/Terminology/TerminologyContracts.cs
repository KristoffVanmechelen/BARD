namespace BARD.Contracts.Terminology;

public record TerminologyEntryDto(
    string Key,
    string Category,
    string Module,
    string? Screen,
    string DefaultNl,
    string DefaultFr,
    string DefaultDe,
    string DefaultEn,
    string CurrentNl,
    string CurrentFr,
    string CurrentDe,
    string CurrentEn,
    bool HasOverrideNl,
    bool HasOverrideFr,
    bool HasOverrideDe,
    bool HasOverrideEn,
    bool IsProtected,
    bool IsAdministratorConfigurable
);

public record TerminologySearchRequest(
    string? SearchText,
    string? Module,
    string? Screen,
    string? Category,
    bool? OnlyMissingTranslations,
    bool? OnlyModified,
    int Page = 1,
    int PageSize = 50
);

public record TerminologySearchResultDto(
    IReadOnlyList<TerminologyEntryDto> Entries,
    int TotalCount,
    int Page,
    int PageSize
);

public record UpdateTerminologyRequest(
    string Key,
    string? Nl,
    string? Fr,
    string? De,
    string? En,
    string Source
);

public record RestoreTerminologyDefaultRequest(string Key, string? Language);

public record TerminologyHistoryEntryDto(
    Guid Id,
    string Language,
    string? PreviousValue,
    string? NewValue,
    string ChangedByDisplayName,
    DateTime ChangedAtUtc,
    string Source
);

public record LocalizationBundleDto(string Language, IReadOnlyDictionary<string, string> Translations);
