using BARD.Domain.Common;
using BARD.Domain.Enums;

namespace BARD.Domain.Entities;

public class DossierDocument : AuditableEntity
{
    public Guid DossierId { get; private set; }
    public string OriginalFileName { get; private set; } = default!;
    public string BlobStoragePath { get; private set; } = default!;
    public string ContentHash { get; private set; } = default!;
    public long FileSizeBytes { get; private set; }

    /// <summary>
    /// Intrinsic kind of the document, independent of its function
    /// within the dossier.
    /// </summary>
    public DocumentKind DocumentKind { get; private set; }

    /// <summary>
    /// Functional role assigned to the document within this dossier.
    /// </summary>
    public DocumentRole DocumentRole { get; private set; }

    public decimal RoleConfidence { get; private set; }
    public string? RoleReasons { get; private set; }
    public bool RoleConfirmedByUser { get; private set; }

    /// <summary>
    /// Legacy classification retained temporarily while the existing
    /// processing pipeline is migrated to DocumentKind and DocumentRole.
    /// </summary>
    public DocumentType DocumentType { get; private set; }

    public decimal ClassificationConfidence { get; private set; }
    public string? ClassificationReasons { get; private set; }

    public ExtractionMethod ExtractionMethod { get; private set; }
    public decimal ExtractionConfidence { get; private set; }
    public string? ExtractionWarnings { get; private set; }
    public bool OcrWasRequired { get; private set; }

    private readonly List<ExtractedField> _extractedFields = new();

    public IReadOnlyCollection<ExtractedField> ExtractedFields =>
        _extractedFields.AsReadOnly();

    protected DossierDocument()
    {
    }

    public static DossierDocument Create(
        Guid dossierId,
        string originalFileName,
        string blobStoragePath,
        string contentHash,
        long fileSizeBytes,
        Guid uploadedByUserId)
    {
        return new DossierDocument
        {
            Id = Guid.NewGuid(),
            DossierId = dossierId,
            OriginalFileName = originalFileName,
            BlobStoragePath = blobStoragePath,
            ContentHash = contentHash,
            FileSizeBytes = fileSizeBytes,

            DocumentKind = DocumentKind.Unknown,
            DocumentRole = DocumentRole.Unknown,
            RoleConfidence = 0m,
            RoleConfirmedByUser = false,

            DocumentType = DocumentType.Unknown,
            ClassificationConfidence = 0m,

            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = uploadedByUserId,
        };
    }

    /// <summary>
    /// Sets the intrinsic kind of the document.
    ///
    /// The legacy DocumentType value is kept in sync while the existing
    /// processing pipeline still depends on it.
    /// </summary>
    public void SetDocumentKind(
        DocumentKind kind,
        decimal confidence,
        string? reasons)
    {
        DocumentKind = kind;
        DocumentType = MapToLegacyDocumentType(kind);
        ClassificationConfidence = confidence;
        ClassificationReasons = reasons;
    }

    /// <summary>
    /// Sets the functional role inferred from the dossier context.
    /// </summary>
    public void SetDocumentRole(
        DocumentRole role,
        decimal confidence,
        string? reasons)
    {
        DocumentRole = role;
        RoleConfidence = confidence;
        RoleReasons = reasons;
        RoleConfirmedByUser = false;
    }

    /// <summary>
    /// Records a role selected or confirmed by a user.
    /// A user decision is authoritative over an automatically inferred role.
    /// </summary>
    public void ConfirmDocumentRole(
        DocumentRole role,
        string? reasons = null)
    {
        DocumentRole = role;
        RoleConfidence = 1m;
        RoleReasons = reasons;
        RoleConfirmedByUser = true;
    }

    /// <summary>
    /// Legacy classification method retained temporarily while the existing
    /// processing pipeline is migrated.
    ///
    /// The new DocumentKind value is also kept in sync.
    /// </summary>
    public void SetClassification(
        DocumentType type,
        decimal confidence,
        string? reasons)
    {
        DocumentType = type;
        DocumentKind = MapFromLegacyDocumentType(type);
        ClassificationConfidence = confidence;
        ClassificationReasons = reasons;
    }

    public void SetExtractionResult(
        ExtractionMethod method,
        decimal confidence,
        string? warnings,
        bool ocrRequired)
    {
        ExtractionMethod = method;
        ExtractionConfidence = confidence;
        ExtractionWarnings = warnings;
        OcrWasRequired = ocrRequired;
    }

    public ExtractedField RecordExtractedField(
        string fieldName,
        string? value,
        int? pageNumber,
        string? rawSnippet,
        decimal confidence)
    {
        var field = ExtractedField.Create(
            Id,
            fieldName,
            value,
            pageNumber,
            rawSnippet,
            confidence);

        _extractedFields.Add(field);

        return field;
    }

    private static DocumentType MapToLegacyDocumentType(
        DocumentKind kind)
    {
        return kind switch
        {
            DocumentKind.Invoice
                => DocumentType.SalesInvoice,

            DocumentKind.Ac4Declaration
                => DocumentType.Ac4Declaration,

            DocumentKind.EadEVadDocument
                => DocumentType.EadEVadDocument,

            DocumentKind.CompanyExcelClaim
                => DocumentType.CompanyExcelClaim,

            DocumentKind.SupportingEvidence
                => DocumentType.SupportingEvidence,

            _
                => DocumentType.Unknown,
        };
    }

    private static DocumentKind MapFromLegacyDocumentType(
        DocumentType type)
    {
        return type switch
        {
            DocumentType.SalesInvoice
                => DocumentKind.Invoice,

            DocumentType.Ac4Declaration
                => DocumentKind.Ac4Declaration,

            DocumentType.EadEVadDocument
                => DocumentKind.EadEVadDocument,

            DocumentType.CompanyExcelClaim
                => DocumentKind.CompanyExcelClaim,

            DocumentType.SupportingEvidence
                => DocumentKind.SupportingEvidence,

            _
                => DocumentKind.Unknown,
        };
    }
}

public class ExtractedField : Entity
{
    public Guid DossierDocumentId { get; private set; }
    public string FieldName { get; private set; } = default!;
    public string? Value { get; private set; }
    public int? PageNumber { get; private set; }
    public string? RawSnippet { get; private set; }
    public decimal Confidence { get; private set; }

    protected ExtractedField()
    {
    }

    public static ExtractedField Create(
        Guid documentId,
        string fieldName,
        string? value,
        int? pageNumber,
        string? rawSnippet,
        decimal confidence)
    {
        return new ExtractedField
        {
            Id = Guid.NewGuid(),
            DossierDocumentId = documentId,
            FieldName = fieldName,
            Value = value,
            PageNumber = pageNumber,
            RawSnippet = rawSnippet,
            Confidence = confidence,
        };
    }
}