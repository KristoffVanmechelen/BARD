using BARD.Domain.Common;
using BARD.Domain.Enums;

namespace BARD.Domain.Entities;

public class DossierLine : Entity
{
    public Guid DossierId { get; private set; }
    public int RowIndex { get; private set; }

    public string? ClaimedInvoiceNumber { get; private set; }
    public string? ClaimedProductDescription { get; private set; }
    public string? ExciseCode { get; private set; }
    public decimal? ClaimedQuantity { get; private set; }
    public string? Mrn { get; private set; }
    public string? ClaimedDestinationCountry { get; private set; }

    public MatchStatus MatchStatus { get; private set; } = MatchStatus.NoMatch;
    public decimal ConfidenceScore { get; private set; }
    public Guid? MatchedDocumentId { get; private set; }
    public string? HardBlockReason { get; private set; }
    public string? MatchExplanation { get; private set; }

    public ExportConfirmationStatus ExportStatus { get; private set; } = ExportConfirmationStatus.Uncertain;
    public string? ExportCheckNotes { get; private set; }

    public MrnCumulativeStatus MrnCumulativeStatus { get; private set; } = MrnCumulativeStatus.NotChecked;
    public string? MrnCumulativeNotes { get; private set; }
    public Ac4Status Ac4Status { get; private set; } = Ac4Status.NotChecked;
    public string? Ac4Notes { get; private set; }

    public OfficerDecision OfficerDecision { get; private set; } = OfficerDecision.PendingReview;
    public string? OfficerRemarks { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }

    public string? AppliedExciseCode { get; private set; }
    public decimal? AppliedRate { get; private set; }
    public ExciseCalculationUnit? AppliedCalculationUnit { get; private set; }
    public decimal? CalculatedRefundAmount { get; private set; }
    public DateTime? CalculationTimestampUtc { get; private set; }
    public Guid? AppliedExciseRateVersionId { get; private set; }
    public string? CalculationNotes { get; private set; }

    protected DossierLine() { }

    public static DossierLine Create(Guid dossierId, int rowIndex, string? invoiceNumber,
        string? productDescription, string? exciseCode, decimal? quantity, string? mrn, string? destinationCountry)
    {
        return new DossierLine
        {
            Id = Guid.NewGuid(),
            DossierId = dossierId,
            RowIndex = rowIndex,
            ClaimedInvoiceNumber = invoiceNumber,
            ClaimedProductDescription = productDescription,
            ExciseCode = exciseCode,
            ClaimedQuantity = quantity,
            Mrn = mrn,
            ClaimedDestinationCountry = destinationCountry,
        };
    }

    public void SetMatchResult(MatchStatus status, decimal confidenceScore, Guid? matchedDocumentId,
        string? hardBlockReason, string explanation)
    {
        MatchStatus = status;
        ConfidenceScore = confidenceScore;
        MatchedDocumentId = matchedDocumentId;
        HardBlockReason = hardBlockReason;
        MatchExplanation = explanation;
    }

    public void SetExportStatus(ExportConfirmationStatus status, string notes)
    {
        ExportStatus = status;
        ExportCheckNotes = notes;
    }

    public void SetMrnCumulativeStatus(MrnCumulativeStatus status, string notes)
    {
        MrnCumulativeStatus = status;
        MrnCumulativeNotes = notes;
    }

    public void SetAc4Status(Ac4Status status, string notes)
    {
        Ac4Status = status;
        Ac4Notes = notes;
    }

    public void ApplyCalculation(string exciseCode, decimal rate, ExciseCalculationUnit unit,
        decimal refundAmount, Guid exciseRateVersionId, string? notes = null)
    {
        AppliedExciseCode = exciseCode;
        AppliedRate = rate;
        AppliedCalculationUnit = unit;
        CalculatedRefundAmount = refundAmount;
        CalculationTimestampUtc = DateTime.UtcNow;
        AppliedExciseRateVersionId = exciseRateVersionId;
        CalculationNotes = notes;
    }

    public void RecordOfficerDecision(OfficerDecision decision, string? remarks, Guid officerUserId)
    {
        if (decision == OfficerDecision.PendingReview)
            throw new InvalidOperationException("An officer decision must be Approved or Rejected, not PendingReview.");

        OfficerDecision = decision;
        OfficerRemarks = remarks;
        ReviewedByUserId = officerUserId;
        ReviewedAtUtc = DateTime.UtcNow;
    }

    public bool RequiresManualReview()
    {
        return MatchStatus is MatchStatus.ManualReviewRequired or MatchStatus.NoMatch
            || ExportStatus is ExportConfirmationStatus.Uncertain or ExportConfirmationStatus.NotConfirmed
            || Ac4Status is Ac4Status.Uncertain or Ac4Status.NotConfirmed
            || MrnCumulativeStatus is MrnCumulativeStatus.Uncertain or MrnCumulativeStatus.Exceeded
            || HardBlockReason is not null;
    }
}
