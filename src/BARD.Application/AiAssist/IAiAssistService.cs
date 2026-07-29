namespace BARD.Application.AiAssist;

/// <summary>Ports core/ai_assist/openai_client.py's AIAssistResult.</summary>
public record AiAssistResult(
    IReadOnlyDictionary<string, string?> Fields,
    string RawResponse,
    string Model,
    int? PromptTokens,
    int? CompletionTokens
);

public class AiAssistException : Exception
{
    public AiAssistException(string message) : base(message) { }
    public AiAssistException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// On-demand AI-assisted field extraction. Ports core/ai_assist/openai_client.py.
///
/// This interface MUST ONLY be invoked in direct response to an
/// explicit officer action (a button click on one specific document) —
/// never automatically from the ingestion pipeline. The confirmed
/// policy ("AI allowed, but only on-demand per invoice — officer clicks
/// a button") is enforced structurally: ProcessDossierCommand never
/// references this interface at all, and the one command that does
/// (RequestAiAssistExtractionCommand) is gated by the DossierAiAssist
/// permission, which is never granted to a system/service identity.
/// </summary>
public interface IAiAssistService
{
    Task<AiAssistResult> ExtractFieldsAsync(
        string rawText,
        IReadOnlyList<string> fieldsNeeded,
        string promptTemplate,
        CancellationToken ct = default);
}
