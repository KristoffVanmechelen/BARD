using BARD.Application.Common.Exceptions;
using BARD.Application.Common.Interfaces;
using BARD.Contracts.Dossiers;
using BARD.Domain.Entities;
using BARD.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BARD.Application.Dossiers.Commands;

public record CorrectInvoiceRoleCommand(
    Guid DossierDocumentId,
    CorrectInvoiceRoleRequest Request
) : IRequest;

public class CorrectInvoiceRoleCommandValidator
    : AbstractValidator<CorrectInvoiceRoleCommand>
{
    public CorrectInvoiceRoleCommandValidator()
    {
        RuleFor(x => x.DossierDocumentId)
            .NotEmpty();

        RuleFor(x => x.Request.DocumentRole)
            .Must(IsInvoiceRole)
            .WithMessage(
                "Document role must be 'PurchaseInvoice' or 'SalesInvoice'.");

        RuleFor(x => x.Request.Reasons)
            .NotEmpty()
            .WithMessage(
                "A reason is required when correcting an invoice role.")
            .MaximumLength(1000);
    }

    private static bool IsInvoiceRole(string role)
    {
        return Enum.TryParse<DocumentRole>(
                   role,
                   ignoreCase: true,
                   out var parsedRole)
               && parsedRole is DocumentRole.PurchaseInvoice
                   or DocumentRole.SalesInvoice;
    }
}

public class CorrectInvoiceRoleCommandHandler
    : IRequestHandler<CorrectInvoiceRoleCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogger _auditLogger;

    public CorrectInvoiceRoleCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IAuditLogger auditLogger)
    {
        _db = db;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    public async Task Handle(
        CorrectInvoiceRoleCommand request,
        CancellationToken cancellationToken)
    {
        var document = await _db.DossierDocuments
            .FirstOrDefaultAsync(
                d => d.Id == request.DossierDocumentId,
                cancellationToken)
            ?? throw new NotFoundException(
                nameof(DossierDocument),
                request.DossierDocumentId);

        if (document.DocumentKind != DocumentKind.Invoice)
        {
            throw new BusinessRuleViolationException(
                $"Document '{document.OriginalFileName}' is not an invoice and cannot be assigned an invoice role.");
        }

        var role = Enum.Parse<DocumentRole>(
            request.Request.DocumentRole,
            ignoreCase: true);

        var previousRole = document.DocumentRole;
        var previousConfidence = document.RoleConfidence;
        var previousReasons = document.RoleReasons;
        var previouslyConfirmedByUser =
            document.RoleConfirmedByUser;
        var previousConfirmedByUserId =
            document.RoleConfirmedByUserId;
        var previousConfirmedAtUtc =
            document.RoleConfirmedAtUtc;

        document.ConfirmDocumentRole(
            role,
            request.Request.Reasons,
            _currentUser.UserId);

        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(
            nameof(DossierDocument),
            document.Id,
            $"DocumentRoleConfirmed:{role}",
            new
            {
                PreviousRole = previousRole.ToString(),
                PreviousConfidence = previousConfidence,
                PreviousReasons = previousReasons,
                PreviouslyConfirmedByUser =
                    previouslyConfirmedByUser,
                PreviousConfirmedByUserId =
                    previousConfirmedByUserId,
                PreviousConfirmedAtUtc =
                    previousConfirmedAtUtc,
                NewRole = role.ToString(),
                NewReasons = request.Request.Reasons,
            },
            cancellationToken);
    }
}

public record RecordOfficerDecisionCommand(RecordOfficerDecisionRequest Request) : IRequest;

public class RecordOfficerDecisionCommandValidator : AbstractValidator<RecordOfficerDecisionCommand>
{
    public RecordOfficerDecisionCommandValidator()
    {
        RuleFor(x => x.Request.Decision).Must(d => d is "Approved" or "Rejected")
            .WithMessage("Decision must be 'Approved' or 'Rejected'.");
    }
}

public class RecordOfficerDecisionCommandHandler : IRequestHandler<RecordOfficerDecisionCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogger _auditLogger;

    public RecordOfficerDecisionCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IAuditLogger auditLogger)
    {
        _db = db;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    public async Task Handle(RecordOfficerDecisionCommand request, CancellationToken cancellationToken)
    {
        var req = request.Request;

        var line = await _db.DossierLines.FirstOrDefaultAsync(l => l.Id == req.DossierLineId, cancellationToken)
            ?? throw new NotFoundException(nameof(DossierLine), req.DossierLineId);

        var dossier = await _db.Dossiers
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == line.DossierId, cancellationToken)
            ?? throw new NotFoundException(nameof(Dossier), line.DossierId);

        var decision = Enum.Parse<OfficerDecision>(req.Decision);
        line.RecordOfficerDecision(decision, req.Remarks, _currentUser.UserId);

        dossier.RecomputeStatusFromLines(_currentUser.UserId);

        await _db.SaveChangesAsync(cancellationToken);
        await _auditLogger.LogAsync(nameof(DossierLine), line.Id, $"OfficerDecision:{decision}",
            new { req.Remarks }, cancellationToken);
    }
}