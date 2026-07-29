using BARD.Application.AiAssist;
using BARD.Application.Common.Exceptions;
using BARD.Application.Common.Interfaces;
using BARD.Application.Common.Options;
using BARD.Domain.Entities;
using BARD.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BARD.Application.Dossiers.Commands;

/// <summary>
/// Ports the Python prototype's Phase-7 on-demand AI-assist button.
/// Called ONLY from the UI's "Try AI extraction" action on one specific
/// document — never from ProcessDossierCommand or any other automatic
/// path. Requires the DossierAiAssist permission.
///
/// RawText is supplied by the caller (API layer re-fetches the blob and
/// re-runs IPdfTextExtractionService against it) rather than this
/// command re-deriving it, since text extraction is already fully
/// implemented and tested in the ingestion pipeline — duplicating that
/// call here would be redundant, not a missing capability.
/// </summary>
public record RequestAiAssistExtractionCommand(
    Guid DossierDocumentId,
    string RawText,
    IReadOnlyList<string> FieldsNeeded
) : IRequest<AiAssistResult>;

public class RequestAiAssistExtractionCommandValidator : AbstractValidator<RequestAiAssistExtractionCommand>
{
    public RequestAiAssistExtractionCommandValidator()
    {
        RuleFor(x => x.FieldsNeeded).NotEmpty()
            .WithMessage("At least one field must be specified for AI-assisted extraction.");
        RuleFor(x => x.RawText).NotEmpty();
    }
}

public class RequestAiAssistExtractionCommandHandler : IRequestHandler<RequestAiAssistExtractionCommand, AiAssistResult>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAiAssistService _aiAssist;
    private readonly IAuditLogger _auditLogger;
    private readonly AiAssistOptions _options;

    public RequestAiAssistExtractionCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IAiAssistService aiAssist,
        IAuditLogger auditLogger,
        IOptions<AiAssistOptions> options)
    {
        _db = db;
        _currentUser = currentUser;
        _aiAssist = aiAssist;
        _auditLogger = auditLogger;
        _options = options.Value;
    }

    public async Task<AiAssistResult> Handle(RequestAiAssistExtractionCommand request, CancellationToken ct)
    {
        if (!_options.Enabled)
            throw new BusinessRuleViolationException("AI-assist is disabled in this deployment.");

        // Redundant with the fact that nothing else calls this — kept as
        // a hard, auditable guard per the confirmed on-demand-only policy,
        // matching core/ai_assist/openai_client.py's identical check.
        if (_options.AutoTrigger)
            throw new BusinessRuleViolationException(
                "AiAssistOptions.AutoTrigger is true, which violates the confirmed on-demand-only policy. " +
                "Refusing to proceed — fix configuration.");

        if (!_currentUser.HasPermission(PermissionCodes.DossierAiAssist))
            throw new ForbiddenAccessException(PermissionCodes.DossierAiAssist);

        var document = await _db.DossierDocuments.FirstOrDefaultAsync(d => d.Id == request.DossierDocumentId, ct)
            ?? throw new NotFoundException(nameof(DossierDocument), request.DossierDocumentId);

        var promptTemplate = document.DocumentType switch
        {
            DocumentType.SalesInvoice => PromptTemplates.InvoiceFieldExtractionV1,
            DocumentType.Ac4Declaration or DocumentType.EadEVadDocument => PromptTemplates.Ac4FieldExtractionV1,
            _ => throw new BusinessRuleViolationException(
                $"AI-assist extraction is not defined for document type '{document.DocumentType}'."),
        };

        var result = await _aiAssist.ExtractFieldsAsync(request.RawText, request.FieldsNeeded, promptTemplate, ct);

        // Record every AI-derived field with full traceability, clearly
        // labelled as AI-assisted so it is never confused with
        // deterministic classical/OCR extraction (Golden Rule: the
        // application must explain what it found and how).
        foreach (var (fieldName, value) in result.Fields)
            document.RecordExtractedField($"AiAssist:{fieldName}", value, null,
                $"model={result.Model}", 0.5m); // 0.5 confidence marker: AI-assisted values are never auto-trusted at full confidence

        document.SetExtractionResult(ExtractionMethod.AiAssisted, document.ExtractionConfidence,
            document.ExtractionWarnings, document.OcrWasRequired);

        await _db.SaveChangesAsync(ct);
        await _auditLogger.LogAsync(nameof(DossierDocument), document.Id, "AiAssistExtraction",
            new { request.FieldsNeeded, result.Model, result.PromptTokens, result.CompletionTokens }, ct);

        return result;
    }
}
