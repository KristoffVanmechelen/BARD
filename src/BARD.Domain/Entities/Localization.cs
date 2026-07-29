using BARD.Domain.Common;
using BARD.Domain.Enums;

namespace BARD.Domain.Entities;

public class LocalizationEntry : Entity
{
    public string Key { get; private set; } = default!;
    public TerminologyCategory Category { get; private set; }
    public string Module { get; private set; } = default!;
    public string? Screen { get; private set; }

    public string DefaultNl { get; private set; } = default!;
    public string DefaultFr { get; private set; } = default!;
    public string DefaultDe { get; private set; } = default!;
    public string DefaultEn { get; private set; } = default!;

    public bool IsProtected { get; private set; }
    public bool IsAdministratorConfigurable { get; private set; } = true;

    private readonly List<TerminologyOverride> _overrides = new();
    public IReadOnlyCollection<TerminologyOverride> Overrides => _overrides.AsReadOnly();

    protected LocalizationEntry() { }

    public static LocalizationEntry Create(string key, TerminologyCategory category, string module, string? screen,
        string defaultNl, string defaultFr, string defaultDe, string defaultEn,
        bool isProtected = false, bool isAdministratorConfigurable = true) =>
        new()
        {
            Id = Guid.NewGuid(),
            Key = key,
            Category = category,
            Module = module,
            Screen = screen,
            DefaultNl = defaultNl,
            DefaultFr = defaultFr,
            DefaultDe = defaultDe,
            DefaultEn = defaultEn,
            IsProtected = isProtected,
            IsAdministratorConfigurable = isAdministratorConfigurable,
        };

    public string GetDefault(UiLanguage language) => language switch
    {
        UiLanguage.NlBe => DefaultNl,
        UiLanguage.FrBe => DefaultFr,
        UiLanguage.DeBe => DefaultDe,
        UiLanguage.En => DefaultEn,
        _ => Key,
    };
}

public class TerminologyOverride : Entity
{
    public Guid LocalizationEntryId { get; private set; }
    public UiLanguage Language { get; private set; }
    public string Value { get; private set; } = default!;
    public Guid LastModifiedByUserId { get; private set; }
    public DateTime LastModifiedAtUtc { get; private set; }

    protected TerminologyOverride() { }

    public static TerminologyOverride Create(Guid localizationEntryId, UiLanguage language, string value, Guid modifiedByUserId) =>
        new()
        {
            Id = Guid.NewGuid(),
            LocalizationEntryId = localizationEntryId,
            Language = language,
            Value = value,
            LastModifiedByUserId = modifiedByUserId,
            LastModifiedAtUtc = DateTime.UtcNow,
        };

    public void UpdateValue(string value, Guid modifiedByUserId)
    {
        Value = value;
        LastModifiedByUserId = modifiedByUserId;
        LastModifiedAtUtc = DateTime.UtcNow;
    }
}

public class TerminologyAuditEntry : Entity
{
    public string LocalizationKey { get; private set; } = default!;
    public UiLanguage Language { get; private set; }
    public string? PreviousValue { get; private set; }
    public string? NewValue { get; private set; }
    public Guid ChangedByUserId { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }
    public TerminologyChangeSource Source { get; private set; }

    protected TerminologyAuditEntry() { }

    public static TerminologyAuditEntry Create(string localizationKey, UiLanguage language, string? previousValue,
        string? newValue, Guid changedByUserId, TerminologyChangeSource source) =>
        new()
        {
            Id = Guid.NewGuid(),
            LocalizationKey = localizationKey,
            Language = language,
            PreviousValue = previousValue,
            NewValue = newValue,
            ChangedByUserId = changedByUserId,
            ChangedAtUtc = DateTime.UtcNow,
            Source = source,
        };
}
