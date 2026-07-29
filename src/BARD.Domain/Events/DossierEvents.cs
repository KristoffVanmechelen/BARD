using BARD.Domain.Common;
using BARD.Domain.Enums;

namespace BARD.Domain.Events;

public sealed class DossierCreatedEvent : DomainEvent
{
    public Guid DossierId { get; }
    public string DossierReference { get; }

    public DossierCreatedEvent(Guid dossierId, string dossierReference)
    {
        DossierId = dossierId;
        DossierReference = dossierReference;
    }
}

public sealed class DossierStatusChangedEvent : DomainEvent
{
    public Guid DossierId { get; }
    public DossierStatus NewStatus { get; }
    public string Reason { get; }

    public DossierStatusChangedEvent(Guid dossierId, DossierStatus newStatus, string reason)
    {
        DossierId = dossierId;
        NewStatus = newStatus;
        Reason = reason;
    }
}
