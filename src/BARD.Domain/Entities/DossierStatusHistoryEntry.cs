using BARD.Domain.Common;
using BARD.Domain.Enums;

namespace BARD.Domain.Entities;

public class DossierStatusHistoryEntry : Entity
{
    public Guid DossierId { get; private set; }
    public DossierStatus Status { get; private set; }
    public string Reason { get; private set; } = default!;
    public Guid ChangedByUserId { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }

    protected DossierStatusHistoryEntry() { }

    public static DossierStatusHistoryEntry Create(Guid dossierId, DossierStatus status, Guid changedByUserId, string reason) =>
        new()
        {
            Id = Guid.NewGuid(),
            DossierId = dossierId,
            Status = status,
            Reason = reason,
            ChangedByUserId = changedByUserId,
            ChangedAtUtc = DateTime.UtcNow,
        };
}
