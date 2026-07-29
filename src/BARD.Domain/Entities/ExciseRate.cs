using BARD.Domain.Common;
using BARD.Domain.Enums;

namespace BARD.Domain.Entities;

public class ExciseRate : AuditableEntity
{
    public string ExciseCode { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;
    public string? AdministrativeComment { get; private set; }

    private readonly List<ExciseRateVersion> _versions = new();
    public IReadOnlyCollection<ExciseRateVersion> Versions => _versions.AsReadOnly();

    protected ExciseRate() { }

    public static ExciseRate Create(string exciseCode, string description, decimal initialRate,
        ExciseCalculationUnit unit, DateOnly effectiveFrom, Guid createdByUserId, string? comment = null)
    {
        if (string.IsNullOrWhiteSpace(exciseCode))
            throw new ArgumentException("Excise code is required.", nameof(exciseCode));

        var rate = new ExciseRate
        {
            Id = Guid.NewGuid(),
            ExciseCode = exciseCode,
            Description = description,
            AdministrativeComment = comment,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = createdByUserId,
        };

        rate._versions.Add(ExciseRateVersion.Create(rate.Id, initialRate, unit, effectiveFrom, createdByUserId));
        return rate;
    }

    public ExciseRateVersion GetCurrentVersion(DateOnly asOfDate) =>
        _versions
            .Where(v => v.EffectiveFrom <= asOfDate)
            .OrderByDescending(v => v.EffectiveFrom)
            .First();

    public ExciseRateVersion PublishNewVersion(decimal rate, ExciseCalculationUnit unit, DateOnly effectiveFrom, Guid changedByUserId)
    {
        var version = ExciseRateVersion.Create(Id, rate, unit, effectiveFrom, changedByUserId);
        _versions.Add(version);
        ModifiedAtUtc = DateTime.UtcNow;
        ModifiedByUserId = changedByUserId;
        return version;
    }

    public void UpdateDescription(string description, Guid changedByUserId)
    {
        Description = description;
        ModifiedAtUtc = DateTime.UtcNow;
        ModifiedByUserId = changedByUserId;
    }

    public void Activate(Guid changedByUserId)
    {
        IsActive = true;
        ModifiedAtUtc = DateTime.UtcNow;
        ModifiedByUserId = changedByUserId;
    }

    public void Deactivate(Guid changedByUserId)
    {
        IsActive = false;
        ModifiedAtUtc = DateTime.UtcNow;
        ModifiedByUserId = changedByUserId;
    }
}

public class ExciseRateVersion : Entity
{
    public Guid ExciseRateId { get; private set; }
    public decimal Rate { get; private set; }
    public ExciseCalculationUnit CalculationUnit { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    protected ExciseRateVersion() { }

    public static ExciseRateVersion Create(Guid exciseRateId, decimal rate, ExciseCalculationUnit unit,
        DateOnly effectiveFrom, Guid createdByUserId) =>
        new()
        {
            Id = Guid.NewGuid(),
            ExciseRateId = exciseRateId,
            Rate = rate,
            CalculationUnit = unit,
            EffectiveFrom = effectiveFrom,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = DateTime.UtcNow,
        };
}

public class ExciseRateAuditEntry : Entity
{
    public Guid ExciseRateId { get; private set; }
    public string ExciseCode { get; private set; } = default!;
    public decimal? PreviousRate { get; private set; }
    public decimal NewRate { get; private set; }
    public ExciseCalculationUnit? PreviousUnit { get; private set; }
    public ExciseCalculationUnit NewUnit { get; private set; }
    public bool? PreviousActiveStatus { get; private set; }
    public bool NewActiveStatus { get; private set; }
    public Guid ChangedByUserId { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }
    public string? Reason { get; private set; }

    protected ExciseRateAuditEntry() { }

    public static ExciseRateAuditEntry Create(Guid exciseRateId, string exciseCode, decimal? previousRate,
        decimal newRate, ExciseCalculationUnit? previousUnit, ExciseCalculationUnit newUnit,
        bool? previousActive, bool newActive, Guid changedByUserId, string? reason) =>
        new()
        {
            Id = Guid.NewGuid(),
            ExciseRateId = exciseRateId,
            ExciseCode = exciseCode,
            PreviousRate = previousRate,
            NewRate = newRate,
            PreviousUnit = previousUnit,
            NewUnit = newUnit,
            PreviousActiveStatus = previousActive,
            NewActiveStatus = newActive,
            ChangedByUserId = changedByUserId,
            ChangedAtUtc = DateTime.UtcNow,
            Reason = reason,
        };
}
