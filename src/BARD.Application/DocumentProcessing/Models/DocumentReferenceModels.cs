namespace BARD.Application.DocumentProcessing.Models;

public enum DocumentReferenceType
{
    Unknown = 0,
    Mrn = 1,
    Drn = 2,
    Arc = 3,
    Lrn = 4,
    Other = 99,
}

public enum ReferenceResolutionMethod
{
    ExplicitLabel = 0,
    PatternInference = 1,
    ContextInference = 2,
}

public sealed record ResolvedDocumentReference(
    DocumentReferenceType Type,
    string Value,
    ReferenceResolutionMethod Method,
    decimal Confidence,
    string? PatternFamily,
    IReadOnlyList<string> Reasons
);
