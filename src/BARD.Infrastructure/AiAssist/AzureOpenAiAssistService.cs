using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using BARD.Application.AiAssist;
using BARD.Application.Common.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace BARD.Infrastructure.AiAssist;

/// <summary>
/// Port of core/ai_assist/openai_client.py's extract_fields_with_ai.
/// Uses Azure OpenAI (consistent with the rest of the Azure-hosted
/// enterprise stack — Blob Storage, SQL Server, Entra ID) rather than
/// the public OpenAI API the Python prototype targeted; behaviour
/// (prompt, retry count, JSON-only response contract) is preserved
/// exactly, only the client/endpoint differs.
/// </summary>
public class AzureOpenAiAssistService : IAiAssistService
{
    private readonly AiAssistOptions _options;
    private readonly ChatClient _chatClient;
    private readonly ILogger<AzureOpenAiAssistService> _logger;

    public AzureOpenAiAssistService(IConfiguration configuration, IOptions<AiAssistOptions> options, ILogger<AzureOpenAiAssistService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var endpoint = configuration["AzureOpenAi:Endpoint"]
            ?? throw new InvalidOperationException("Missing 'AzureOpenAi:Endpoint' configuration.");
        var apiKey = configuration["AzureOpenAi:ApiKey"]
            ?? throw new InvalidOperationException("Missing 'AzureOpenAi:ApiKey' configuration.");

        var azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        _chatClient = azureClient.GetChatClient(_options.Model);
    }

    public async Task<AiAssistResult> ExtractFieldsAsync(
        string rawText, IReadOnlyList<string> fieldsNeeded, string promptTemplate, CancellationToken ct = default)
    {
        var prompt = PromptTemplates.Build(promptTemplate, fieldsNeeded, rawText);

        Exception? lastError = null;

        for (var attempt = 0; attempt <= _options.MaxRetries; attempt++)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

                var response = await _chatClient.CompleteChatAsync(
                    new List<ChatMessage> { new UserChatMessage(prompt) },
                    new ChatCompletionOptions { MaxOutputTokenCount = 1000 },
                    cts.Token);

                var content = response.Value.Content.Count > 0 ? response.Value.Content[0].Text : "{}";
                var cleaned = content.Trim();
                if (cleaned.StartsWith("```json")) cleaned = cleaned["```json".Length..];
                if (cleaned.StartsWith("```")) cleaned = cleaned["```".Length..];
                if (cleaned.EndsWith("```")) cleaned = cleaned[..^"```".Length];
                cleaned = cleaned.Trim();

                var fields = JsonSerializer.Deserialize<Dictionary<string, string?>>(cleaned) ?? new();

                var usage = response.Value.Usage;
                _logger.LogInformation("AI-assist call: model={Model} promptTokens={PromptTokens} completionTokens={CompletionTokens}",
                    _options.Model, usage?.InputTokenCount, usage?.OutputTokenCount);

                return new AiAssistResult(fields, content, _options.Model, usage?.InputTokenCount, usage?.OutputTokenCount);
            }
            catch (Exception ex)
            {
                lastError = ex;
                _logger.LogWarning(ex, "AI-assist attempt {Attempt} failed", attempt + 1);
            }
        }

        throw new AiAssistException($"AI-assist extraction failed after {_options.MaxRetries + 1} attempts.", lastError!);
    }
}
