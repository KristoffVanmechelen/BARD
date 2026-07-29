using BARD.Domain.Common;
using BARD.Domain.Enums;

namespace BARD.Domain.Entities;

/// <summary>
/// Structured data parsed from an AC4 (or eAD/eVAD) declaration
/// document. One-to-one with a DossierDocument classified as
/// DocumentType.Ac4Declaration or EadEVadDocument. Kept as a dedicated
/// entity (rather than folded into generic ExtractedField rows) because
/// AC4 matching (MRN cumulative validation, deadline check) is a
/// first-class documented business process, not incidental metadata.
/// </summary>
public class Ac4Declaration : Entity
{
    public Guid DossierDocumentId { get; private set; }
    public string? Mrn { get; private set; }
    public DateOnly? Ac4Date { get; private set; }
    public string? Consignee { get; private set; }
    public string? ProductDescription { get; private set; }
    public decimal? Quantity { get; private set; }
    public string? ExciseCode { get; private set; }

    public ExtractionMethod ExtractionMethod { get; private set; }
    public decimal ExtractionConfidence { get; private set; }
    public string? ExtractionWarnings { get; private set; }

    protected Ac4Declaration() { }

    public static Ac4Declaration Create(Guid dossierDocumentId, string? mrn, DateOnly? ac4Date,
        string? consignee, string? productDescription, decimal? quantity, string? exciseCode,
        ExtractionMethod extractionMethod, decimal extractionConfidence, string? extractionWarnings) =>
        new()
        {
            Id = Guid.NewGuid(),
            DossierDocumentId = dossierDocumentId,
            Mrn = mrn,
            Ac4Date = ac4Date,
            Consignee = consignee,
            ProductDescription = productDescription,
            Quantity = quantity,
            ExciseCode = exciseCode,
            ExtractionMethod = extractionMethod,
            ExtractionConfidence = extractionConfidence,
            ExtractionWarnings = extractionWarnings,
        };
}
