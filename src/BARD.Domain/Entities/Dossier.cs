using BARD.Domain.Common;
using BARD.Domain.Enums;
using BARD.Domain.Events;

namespace BARD.Domain.Entities;

public class Dossier : AuditableEntity
{
    public string DossierReference { get; private set; } = default!;
    public Guid CompanyId { get; private set; }
    public DateOnly RefundApplicationDate { get; private set; }
    public DossierStatus Status { get; private set; }

    private readonly List<DossierLine> _lines = new();
    public IReadOnlyCollection<DossierLine> Lines => _lines.AsReadOnly();

    private readonly List<DossierDocument> _documents = new();
    public IReadOnlyCollection<DossierDocument> Documents => _documents.AsReadOnly();

    private readonly List<DossierStatusHistoryEntry> _statusHistory = new();
    public IReadOnlyCollection<DossierStatusHistoryEntry> StatusHistory => _statusHistory.AsReadOnly();

    protected Dossier() { }

    public static Dossier Create(string dossierReference, Guid companyId, DateOnly refundApplicationDate, Guid createdByUserId)
    {
        if (string.IsNullOrWhiteSpace(dossierReference))
            throw new ArgumentException("Dossier reference is required.", nameof(dossierReference));

        var dossier = new Dossier
        {
            Id = Guid.NewGuid(),
            DossierReference = dossierReference,
            CompanyId = companyId,
            RefundApplicationDate = refundApplicationDate,
            Status = DossierStatus.Intake,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = createdByUserId,
        };

        dossier._statusHistory.Add(DossierStatusHistoryEntry.Create(dossier.Id, DossierStatus.Intake, createdByUserId, "Dossier created."));
        dossier.AddDomainEvent(new DossierCreatedEvent(dossier.Id, dossierReference));
        return dossier;
    }

    public DossierLine AddLine(DossierLine line)
    {
        _lines.Add(line);
        return line;
    }

    public void AttachDocument(DossierDocument document) => _documents.Add(document);

    public void TransitionTo(DossierStatus newStatus, Guid changedByUserId, string reason)
    {
        if (Status == newStatus) return;

        Status = newStatus;
        ModifiedAtUtc = DateTime.UtcNow;
        ModifiedByUserId = changedByUserId;

        _statusHistory.Add(DossierStatusHistoryEntry.Create(Id, newStatus, changedByUserId, reason));
        AddDomainEvent(new DossierStatusChangedEvent(Id, newStatus, reason));
    }

    public void RecomputeStatusFromLines(Guid systemUserId)
    {
        if (_lines.Count == 0) return;

        var anyPendingReview = _lines.Any(l => l.OfficerDecision == OfficerDecision.PendingReview);
        var anyRejected = _lines.Any(l => l.OfficerDecision == OfficerDecision.Rejected);
        var allApproved = _lines.All(l => l.OfficerDecision == OfficerDecision.Approved);
        var anyApproved = _lines.Any(l => l.OfficerDecision == OfficerDecision.Approved);

        if (anyPendingReview)
        {
            TransitionTo(DossierStatus.PendingManualReview, systemUserId,
                "One or more lines still require officer review.");
        }
        else if (allApproved)
        {
            TransitionTo(DossierStatus.Approved, systemUserId, "All lines approved by officer.");
        }
        else if (anyApproved && anyRejected)
        {
            TransitionTo(DossierStatus.PartiallyApproved, systemUserId,
                "Dossier contains both approved and rejected lines.");
        }
        else if (!anyApproved)
        {
            TransitionTo(DossierStatus.Rejected, systemUserId, "All lines rejected by officer.");
        }
    }
}
