namespace BARD.Application.Common.Options;

/// <summary>
/// Ports core/config.py. Every threshold/weight the Python prototype
/// hardcoded as module-level constants is preserved here exactly, bound
/// from appsettings.json so it stays externally configurable without a
/// rebuild — but the VALUES are unchanged from the documented prototype.
/// </summary>
public class MatchThresholdsOptions
{
    public const string SectionName = "MatchThresholds";

    public decimal AutoMatchMin { get; set; } = 95.0m;
    public decimal LikelyMatchMin { get; set; } = 80.0m;
}

public class ScoringWeightsOptions
{
    public const string SectionName = "ScoringWeights";

    public decimal InvoiceNumber { get; set; } = 0.35m;
    public decimal Quantity { get; set; } = 0.20m;
    public decimal ExciseCode { get; set; } = 0.20m;
    public decimal Description { get; set; } = 0.15m;
    public decimal DestinationCountry { get; set; } = 0.10m;
}

public class BusinessRulesOptions
{
    public const string SectionName = "BusinessRules";

    /// <summary>Confirmed hard rule: excise code mismatch/absence always forces manual review.</summary>
    public bool ExciseCodeMismatchIsHardBlock { get; set; } = true;

    /// <summary>No tolerance/rounding permitted for regulatory quantity validation. Exact match only.</summary>
    public decimal QuantityTolerancePct { get; set; } = 0.0m;

    /// <summary>Fuzzy match floor (0-100) below which a description is a hard non-match.</summary>
    public int DescriptionFuzzyFloor { get; set; } = 40;

    /// <summary>Statutory refund deadline in months, measured AC4 date -> refund application date.</summary>
    public int RefundDeadlineMonths { get; set; } = 12;

    /// <summary>
    /// Minimum document-classification confidence required to act on a
    /// classification automatically; below this, a file is routed to
    /// "unclassified" rather than guessed. Single source of truth,
    /// referenced by both DocumentClassifierService and
    /// ProcessDossierCommandHandler (previously duplicated — audit
    /// finding M1).
    /// </summary>
    public decimal ClassificationConfidenceFloor { get; set; } = 0.4m;
}

public class OcrOptions
{
    public const string SectionName = "Ocr";

    public string Engine { get; set; } = "Tesseract";
    public int MinTextCharsPerPage { get; set; } = 40;
    public int Dpi { get; set; } = 300;
    /// <summary>Path to the Tesseract tessdata directory (language files).</summary>
    public string TessDataPath { get; set; } = "./tessdata";
    public string Language { get; set; } = "eng+nld+fra+deu";
}

/// <summary>
/// Ports core/config.py's AIAssistConfig. Confirmed decision: AI-assist
/// is ON-DEMAND ONLY — the pipeline must never call this automatically.
/// AutoTrigger exists here only as an explicit, auditable safety flag
/// that every caller must check and refuse to proceed if true; nothing
/// in ProcessDossierCommand references this service at all.
/// </summary>
public class AiAssistOptions
{
    public const string SectionName = "AiAssist";

    public bool Enabled { get; set; } = true;
    /// <summary>MUST remain false. See RequestAiAssistExtractionCommandHandler's guard.</summary>
    public bool AutoTrigger { get; set; } = false;
    public string Model { get; set; } = "gpt-4o-mini";
    public int MaxRetries { get; set; } = 2;
    public int TimeoutSeconds { get; set; } = 30;
}
