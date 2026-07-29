using BARD.Application.Common.Exceptions;
using BARD.Application.Common.Interfaces;
using BARD.Contracts.Terminology;
using BARD.Domain.Entities;
using BARD.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BARD.Application.Terminology.Commands;

public record UpdateTerminologyCommand(UpdateTerminologyRequest Request) : IRequest;

public class UpdateTerminologyCommandHandler : IRequestHandler<UpdateTerminologyCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateTerminologyCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateTerminologyCommand request, CancellationToken cancellationToken)
    {
        var req = request.Request;

        var entry = await _db.LocalizationEntries
            .Include(e => e.Overrides)
            .FirstOrDefaultAsync(e => e.Key == req.Key, cancellationToken)
            ?? throw new NotFoundException(nameof(LocalizationEntry), req.Key);

        if (!entry.IsAdministratorConfigurable)
            throw new BusinessRuleViolationException($"Localization key '{req.Key}' is protected and not administrator-configurable.");

        var source = Enum.Parse<TerminologyChangeSource>(req.Source);
        if (source == TerminologyChangeSource.InlineEditor && entry.IsProtected)
            throw new BusinessRuleViolationException(
                $"Localization key '{req.Key}' is protected and cannot be changed via inline editing. Use central administration instead.",
                "errors.terminology.protected_key",
                new Dictionary<string, string> { ["key"] = req.Key });

        ApplyLanguageChange(entry, UiLanguage.NlBe, req.Nl, source);
        ApplyLanguageChange(entry, UiLanguage.FrBe, req.Fr, source);
        ApplyLanguageChange(entry, UiLanguage.DeBe, req.De, source);
        ApplyLanguageChange(entry, UiLanguage.En, req.En, source);

        await _db.SaveChangesAsync(cancellationToken);
    }

    private void ApplyLanguageChange(LocalizationEntry entry, UiLanguage language, string? newValue, TerminologyChangeSource source)
    {
        if (newValue is null) return;

        var existingOverride = entry.Overrides.FirstOrDefault(o => o.Language == language);
        var previousValue = existingOverride?.Value ?? entry.GetDefault(language);

        if (existingOverride is null)
        {
            var newOverride = TerminologyOverride.Create(entry.Id, language, newValue, _currentUser.UserId);
            _db.TerminologyOverrides.Add(newOverride);
        }
        else
        {
            existingOverride.UpdateValue(newValue, _currentUser.UserId);
        }

        var audit = TerminologyAuditEntry.Create(entry.Key, language, previousValue, newValue, _currentUser.UserId, source);
        _db.TerminologyAuditEntries.Add(audit);
    }
}

public record RestoreTerminologyDefaultCommand(RestoreTerminologyDefaultRequest Request) : IRequest;

public class RestoreTerminologyDefaultCommandHandler : IRequestHandler<RestoreTerminologyDefaultCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public RestoreTerminologyDefaultCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(RestoreTerminologyDefaultCommand request, CancellationToken cancellationToken)
    {
        var req = request.Request;

        var entry = await _db.LocalizationEntries
            .Include(e => e.Overrides)
            .FirstOrDefaultAsync(e => e.Key == req.Key, cancellationToken)
            ?? throw new NotFoundException(nameof(LocalizationEntry), req.Key);

        var languagesToRestore = req.Language is null
            ? entry.Overrides.Select(o => o.Language).Distinct().ToList()
            : new List<UiLanguage> { Enum.Parse<UiLanguage>(req.Language) };

        foreach (var language in languagesToRestore)
        {
            var existingOverride = entry.Overrides.FirstOrDefault(o => o.Language == language);
            if (existingOverride is null) continue;

            var audit = TerminologyAuditEntry.Create(entry.Key, language, existingOverride.Value,
                entry.GetDefault(language), _currentUser.UserId, TerminologyChangeSource.DefaultRestoration);
            _db.TerminologyAuditEntries.Add(audit);

            _db.TerminologyOverrides.Remove(existingOverride);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
