using BARD.Application.Common.Exceptions;
using BARD.Application.Common.Interfaces;
using BARD.Contracts.Dossiers;
using BARD.Domain.Entities;
using BARD.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BARD.Application.Dossiers.Commands;

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
