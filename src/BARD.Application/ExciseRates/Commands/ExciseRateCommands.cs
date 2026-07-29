using BARD.Application.Common.Exceptions;
using BARD.Application.Common.Interfaces;
using BARD.Contracts.ExciseRates;
using BARD.Domain.Entities;
using BARD.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BARD.Application.ExciseRates.Commands;

public record CreateExciseRateCommand(CreateExciseRateRequest Request) : IRequest<Guid>;

public class CreateExciseRateCommandValidator : AbstractValidator<CreateExciseRateCommand>
{
    public CreateExciseRateCommandValidator()
    {
        RuleFor(x => x.Request.ExciseCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Request.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Request.InitialRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.CalculationUnit).NotEmpty();
    }
}

public class CreateExciseRateCommandHandler : IRequestHandler<CreateExciseRateCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogger _auditLogger;

    public CreateExciseRateCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IAuditLogger auditLogger)
    {
        _db = db;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    public async Task<Guid> Handle(
        CreateExciseRateCommand request,
        CancellationToken cancellationToken)
    {
        var req = request.Request;

        var exists = await _db.ExciseRates.AnyAsync(
            r => r.ExciseCode == req.ExciseCode,
            cancellationToken);

        if (exists)
        {
            throw new BARD.Application.Common.Exceptions.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure(
                    nameof(req.ExciseCode),
                    $"Excise code '{req.ExciseCode}' already exists.")
            });
        }

        var unit = Enum.Parse<ExciseCalculationUnit>(req.CalculationUnit);

        var rate = ExciseRate.Create(
            req.ExciseCode,
            req.Description,
            req.InitialRate,
            unit,
            req.EffectiveFrom,
            _currentUser.UserId,
            req.AdministrativeComment);

        _db.ExciseRates.Add(rate);

        var audit = ExciseRateAuditEntry.Create(
            rate.Id,
            rate.ExciseCode,
            null,
            req.InitialRate,
            null,
            unit,
            null,
            true,
            _currentUser.UserId,
            "Excise rate created.");

        _db.ExciseRateAuditEntries.Add(audit);

        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(
            nameof(ExciseRate),
            rate.Id,
            "Created",
            req,
            cancellationToken);

        return rate.Id;
    }
}

public record PublishExciseRateVersionCommand(
    Guid ExciseRateId,
    PublishExciseRateVersionRequest Request) : IRequest<Guid>;

public class PublishExciseRateVersionCommandValidator
    : AbstractValidator<PublishExciseRateVersionCommand>
{
    public PublishExciseRateVersionCommandValidator()
    {
        RuleFor(x => x.Request.Rate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.CalculationUnit).NotEmpty();
    }
}

public class PublishExciseRateVersionCommandHandler
    : IRequestHandler<PublishExciseRateVersionCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogger _auditLogger;

    public PublishExciseRateVersionCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IAuditLogger auditLogger)
    {
        _db = db;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    public async Task<Guid> Handle(
        PublishExciseRateVersionCommand request,
        CancellationToken cancellationToken)
    {
        var rate = await _db.ExciseRates
            .Include(r => r.Versions)
            .FirstOrDefaultAsync(
                r => r.Id == request.ExciseRateId,
                cancellationToken)
            ?? throw new NotFoundException(
                nameof(ExciseRate),
                request.ExciseRateId);

        var previousVersion = rate.GetCurrentVersion(
            DateOnly.FromDateTime(DateTime.UtcNow));

        var newUnit = Enum.Parse<ExciseCalculationUnit>(
            request.Request.CalculationUnit);

        var version = rate.PublishNewVersion(
            request.Request.Rate,
            newUnit,
            request.Request.EffectiveFrom,
            _currentUser.UserId);

        var audit = ExciseRateAuditEntry.Create(
            rate.Id,
            rate.ExciseCode,
            previousVersion.Rate,
            request.Request.Rate,
            previousVersion.CalculationUnit,
            newUnit,
            rate.IsActive,
            rate.IsActive,
            _currentUser.UserId,
            "New rate version published.");

        _db.ExciseRateAuditEntries.Add(audit);

        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(
            nameof(ExciseRate),
            rate.Id,
            "NewVersionPublished",
            request.Request,
            cancellationToken);

        return version.Id;
    }
}

public record SetExciseRateActiveStatusCommand(
    Guid ExciseRateId,
    bool IsActive,
    string? Reason) : IRequest;

public class SetExciseRateActiveStatusCommandHandler
    : IRequestHandler<SetExciseRateActiveStatusCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogger _auditLogger;

    public SetExciseRateActiveStatusCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IAuditLogger auditLogger)
    {
        _db = db;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    public async Task Handle(
        SetExciseRateActiveStatusCommand request,
        CancellationToken cancellationToken)
    {
        var rate = await _db.ExciseRates
            .FirstOrDefaultAsync(
                r => r.Id == request.ExciseRateId,
                cancellationToken)
            ?? throw new NotFoundException(
                nameof(ExciseRate),
                request.ExciseRateId);

        var wasActive = rate.IsActive;

        if (request.IsActive)
        {
            rate.Activate(_currentUser.UserId);
        }
        else
        {
            rate.Deactivate(_currentUser.UserId);
        }

        var currentVersion = rate.GetCurrentVersion(
            DateOnly.FromDateTime(DateTime.UtcNow));

        var audit = ExciseRateAuditEntry.Create(
            rate.Id,
            rate.ExciseCode,
            currentVersion.Rate,
            currentVersion.Rate,
            currentVersion.CalculationUnit,
            currentVersion.CalculationUnit,
            wasActive,
            request.IsActive,
            _currentUser.UserId,
            request.Reason);

        _db.ExciseRateAuditEntries.Add(audit);

        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(
            nameof(ExciseRate),
            rate.Id,
            request.IsActive ? "Activated" : "Deactivated",
            request.Reason,
            cancellationToken);
    }
}

public record DeleteExciseRateCommand(Guid ExciseRateId) : IRequest;

public class DeleteExciseRateCommandHandler
    : IRequestHandler<DeleteExciseRateCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditLogger _auditLogger;

    public DeleteExciseRateCommandHandler(
        IApplicationDbContext db,
        IAuditLogger auditLogger)
    {
        _db = db;
        _auditLogger = auditLogger;
    }

    public async Task Handle(
        DeleteExciseRateCommand request,
        CancellationToken cancellationToken)
    {
        var rate = await _db.ExciseRates
            .Include(r => r.Versions)
            .FirstOrDefaultAsync(
                r => r.Id == request.ExciseRateId,
                cancellationToken)
            ?? throw new NotFoundException(
                nameof(ExciseRate),
                request.ExciseRateId);

        var versionIds = rate.Versions
            .Select(v => v.Id)
            .ToList();

        var usedInDossier = await _db.DossierLines.AnyAsync(
            l => l.AppliedExciseRateVersionId != null
                 && versionIds.Contains(
                     l.AppliedExciseRateVersionId.Value),
            cancellationToken);

        if (usedInDossier)
        {
            throw new BusinessRuleViolationException(
                $"Excise code '{rate.ExciseCode}' has already been used in one or more dossiers and cannot be deleted. " +
                "Deactivate it instead.",
                "errors.exciserate.cannot_delete_in_use",
                new Dictionary<string, string>
                {
                    ["code"] = rate.ExciseCode
                });
        }

        _db.ExciseRates.Remove(rate);

        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(
            nameof(ExciseRate),
            rate.Id,
            "Deleted",
            null,
            cancellationToken);
    }
}

public record UpdateExciseRateDescriptionCommand(
    Guid ExciseRateId,
    string Description) : IRequest;

public class UpdateExciseRateDescriptionCommandHandler
    : IRequestHandler<UpdateExciseRateDescriptionCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateExciseRateDescriptionCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(
        UpdateExciseRateDescriptionCommand request,
        CancellationToken cancellationToken)
    {
        var rate = await _db.ExciseRates
            .FirstOrDefaultAsync(
                r => r.Id == request.ExciseRateId,
                cancellationToken)
            ?? throw new NotFoundException(
                nameof(ExciseRate),
                request.ExciseRateId);

        rate.UpdateDescription(
            request.Description,
            _currentUser.UserId);

        await _db.SaveChangesAsync(cancellationToken);
    }
}