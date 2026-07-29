namespace BARD.Application.AiAssist;

/// <summary>
/// Versioned prompt templates, ports core/ai_assist/prompts.py. Kept
/// separate from the calling code so prompt changes are reviewable
/// independently.
/// </summary>
public static class PromptTemplates
{
    public const string InvoiceFieldExtractionV1 = """
        You are assisting a Belgian Customs officer in extracting structured
        fields from a sales invoice that automated parsing could not fully
        resolve. Extract ONLY the following fields: {fields}.

        Respond with ONLY a JSON object mapping each requested field name to
        its value (or null if genuinely not present in the text). Do not
        include any explanation, preamble, or markdown formatting — JSON only.

        Invoice text:
        ---
        {text}
        ---
        """;

    public const string Ac4FieldExtractionV1 = """
        You are assisting a Belgian Customs officer in extracting structured
        fields from an AC4 excise movement declaration that automated
        parsing could not fully resolve. Extract ONLY the following fields:
        {fields}.

        Respond with ONLY a JSON object mapping each requested field name to
        its value (or null if genuinely not present in the text). Do not
        include any explanation, preamble, or markdown formatting — JSON only.

        AC4 text:
        ---
        {text}
        ---
        """;

    public static string Build(string template, IEnumerable<string> fields, string text) =>
        template
            .Replace("{fields}", string.Join(", ", fields))
            .Replace("{text}", text.Length > 8000 ? text[..8000] : text);
}
