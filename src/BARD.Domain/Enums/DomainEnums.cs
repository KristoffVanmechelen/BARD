namespace BARD.Domain.Enums;

public enum DossierStatus
{
    Intake = 0,
    UnderAutomaticProcessing = 1,
    PendingManualReview = 2,
    ManualReviewInProgress = 3,
    Approved = 4,
    Rejected = 5,
    PartiallyApproved = 6,
    Closed = 7,
    Archived = 8,
}

public enum MatchStatus
{
    AutoMatch = 0,
    LikelyMatch = 1,
    ManualReviewRequired = 2,
    NoMatch = 3,
}

public enum ExportConfirmationStatus
{
    Confirmed = 0,
    NotConfirmed = 1,
    Uncertain = 2,
}

public enum Ac4Status
{
    NotChecked = 0,
    Confirmed = 1,
    NotConfirmed = 2,
    Uncertain = 3,
}

public enum MrnCumulativeStatus
{
    NotChecked = 0,
    WithinLimit = 1,
    Exceeded = 2,
    Uncertain = 3,
}

public enum OfficerDecision
{
    PendingReview = 0,
    Approved = 1,
    Rejected = 2,
}

public enum ExtractionMethod
{
    ClassicalTextExtraction = 0,
    Ocr = 1,
    AiAssisted = 2,
    ManualEntry = 3,
}

/// <summary>
/// Intrinsic type of the document.
/// This never depends on the dossier context.
/// </summary>
public enum DocumentKind
{
    Unknown = 0,
    Invoice = 1,
    Ac4Declaration = 2,
    EadEVadDocument = 3,
    CompanyExcelClaim = 4,
    SupportingEvidence = 5,
}

/// <summary>
/// Functional role of a document within a dossier.
/// The same document kind may have different roles.
/// </summary>
public enum DocumentRole
{
    Unknown = 0,
    PurchaseInvoice = 1,
    SalesInvoice = 2,
    RefundClaim = 3,
    AcquisitionEvidence = 4,
    DispatchEvidence = 5,
    SupportingEvidence = 6,
    Irrelevant = 7,
}

/// <summary>
/// Legacy classification.
/// Kept temporarily while migrating to DocumentKind + DocumentRole.
/// </summary>
public enum DocumentType
{
    Unknown = 0,
    SalesInvoice = 1,
    Ac4Declaration = 2,
    EadEVadDocument = 3,
    CompanyExcelClaim = 4,
    SupportingEvidence = 5,
}

public enum UiLanguage
{
    NlBe = 0,
    FrBe = 1,
    DeBe = 2,
    En = 3,
}

public enum TerminologyChangeSource
{
    InlineEditor = 0,
    CentralAdministration = 1,
    ConfigurationImport = 2,
    DefaultRestoration = 3,
    SystemSeed = 4,
}

public enum TerminologyCategory
{
    PageTitle = 0,
    SectionTitle = 1,
    MenuItem = 2,
    NavigationLabel = 3,
    FieldLabel = 4,
    ButtonCaption = 5,
    TableHeading = 6,
    ColumnHeading = 7,
    TabName = 8,
    StatusName = 9,
    ValidationMessage = 10,
    InformationalMessage = 11,
    WarningMessage = 12,
    ConfirmationMessage = 13,
    Tooltip = 14,
    HelpText = 15,
    EmptyStateMessage = 16,
    DocumentLabel = 17,
    Other = 18,
}

public enum ExciseCalculationUnit
{
    PerHectolitre = 0,
    PerHectolitreOfPureAlcohol = 1,
    PerDegreePlatoPerHectolitre = 2,
    PerHectolitreAlcoholicStrength = 3,
    Other = 99,
}

public enum RecalculationMethod
{
    OriginallyAppliedRate = 0,
    CurrentlyConfiguredRate = 1,
}