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

    public DocumentType DocumentType { get; private set; }
    public decimal ClassificationConfidence { get; private set; }
    public string? ClassificationReasons { get; private set; }

    public ExtractionMethod ExtractionMethod { get; private set; }
    public decimal ExtractionConfidence { get; private set; }
    public string? ExtractionWarnings { get; private set; }
    public bool OcrWasRequired { get; private set; }

    private readonly List<ExtractedField> _extractedFields = new();
    public IReadOnlyCollection<ExtractedField> ExtractedFields => _extractedFields.AsReadOnly();

    protected DossierDocument() { }

    public static DossierDocument Create(Guid dossierId, string originalFileName, string blobStoragePath,
        string contentHash, long fileSizeBytes, Guid uploadedByUserId)
    {
        return new DossierDocument
        {
            Id = Guid.NewGuid(),
            DossierId = dossierId,
            OriginalFileName = originalFileName,
            BlobStoragePath = blobStoragePath,
            ContentHash = contentHash,
            FileSizeBytes = fileSizeBytes,
            DocumentType = DocumentType.Unknown,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = uploadedByUserId,
        };
    }

    public void SetClassification(DocumentType type, decimal confidence, string reasons)
    {
        DocumentType = type;
        ClassificationConfidence = confidence;
        ClassificationReasons = reasons;
    }

    public void SetExtractionResult(ExtractionMethod method, decimal confidence, string? warnings, bool ocrRequired)
    {
        ExtractionMethod = method;
        ExtractionConfidence = confidence;
        ExtractionWarnings = warnings;
        OcrWasRequired = ocrRequired;
    }

    public ExtractedField RecordExtractedField(string fieldName, string? value, int? pageNumber, string? rawSnippet, decimal confidence)
    {
        var field = ExtractedField.Create(Id, fieldName, value, pageNumber, rawSnippet, confidence);
        _extractedFields.Add(field);
        return field;
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

    protected ExtractedField() { }

    public static ExtractedField Create(Guid documentId, string fieldName, string? value, int? pageNumber, string? rawSnippet, decimal confidence) =>
        new()
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
