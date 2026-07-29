using BARD.Application.Common.Interfaces;
using BARD.Contracts.Terminology;
using BARD.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BARD.Application.Terminology.Queries;

public record SearchTerminologyQuery(TerminologySearchRequest Request) : IRequest<TerminologySearchResultDto>;

public class SearchTerminologyQueryHandler : IRequestHandler<SearchTerminologyQuery, TerminologySearchResultDto>
{
    private readonly IApplicationDbContext _db;

    public SearchTerminologyQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<TerminologySearchResultDto> Handle(SearchTerminologyQuery request, CancellationToken cancellationToken)
    {
        var req = request.Request;
        var query = _db.LocalizationEntries.Include(e => e.Overrides).AsQueryable();

        if (!string.IsNullOrWhiteSpace(req.Module))
            query = query.Where(e => e.Module == req.Module);
        if (!string.IsNullOrWhiteSpace(req.Screen))
            query = query.Where(e => e.Screen == req.Screen);
        if (!string.IsNullOrWhiteSpace(req.Category))
            query = query.Where(e => e.Category == Enum.Parse<TerminologyCategory>(req.Category));
        if (!string.IsNullOrWhiteSpace(req.SearchText))
        {
            var text = req.SearchText.ToLower();
            query = query.Where(e =>
                e.Key.ToLower().Contains(text) ||
                e.DefaultNl.ToLower().Contains(text) ||
                e.DefaultFr.ToLower().Contains(text) ||
                e.DefaultDe.ToLower().Contains(text) ||
                e.DefaultEn.ToLower().Contains(text));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var entries = await query
            .OrderBy(e => e.Module).ThenBy(e => e.Key)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = entries.Select(e =>
        {
            string CurrentOrDefault(UiLanguage lang, string def) =>
                e.Overrides.FirstOrDefault(o => o.Language == lang)?.Value ?? def;

            var dto = new TerminologyEntryDto(
                e.Key, e.Category.ToString(), e.Module, e.Screen,
                e.DefaultNl, e.DefaultFr, e.DefaultDe, e.DefaultEn,
                CurrentOrDefault(UiLanguage.NlBe, e.DefaultNl),
                CurrentOrDefault(UiLanguage.FrBe, e.DefaultFr),
                CurrentOrDefault(UiLanguage.DeBe, e.DefaultDe),
                CurrentOrDefault(UiLanguage.En, e.DefaultEn),
                e.Overrides.Any(o => o.Language == UiLanguage.NlBe),
                e.Overrides.Any(o => o.Language == UiLanguage.FrBe),
                e.Overrides.Any(o => o.Language == UiLanguage.DeBe),
                e.Overrides.Any(o => o.Language == UiLanguage.En),
                e.IsProtected, e.IsAdministratorConfigurable);
            return dto;
        }).ToList();

        if (req.OnlyMissingTranslations == true)
            dtos = dtos.Where(d => string.IsNullOrWhiteSpace(d.CurrentNl) || string.IsNullOrWhiteSpace(d.CurrentFr)
                || string.IsNullOrWhiteSpace(d.CurrentDe) || string.IsNullOrWhiteSpace(d.CurrentEn)).ToList();

        if (req.OnlyModified == true)
            dtos = dtos.Where(d => d.HasOverrideNl || d.HasOverrideFr || d.HasOverrideDe || d.HasOverrideEn).ToList();

        return new TerminologySearchResultDto(dtos, totalCount, req.Page, req.PageSize);
    }
}

public record GetLocalizationBundleQuery(string Language) : IRequest<LocalizationBundleDto>;

public class GetLocalizationBundleQueryHandler : IRequestHandler<GetLocalizationBundleQuery, LocalizationBundleDto>
{
    private readonly IApplicationDbContext _db;

    public GetLocalizationBundleQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<LocalizationBundleDto> Handle(GetLocalizationBundleQuery request, CancellationToken cancellationToken)
    {
        var language = Enum.TryParse<UiLanguage>(request.Language, out var parsed) ? parsed : UiLanguage.NlBe;

        var entries = await _db.LocalizationEntries.Include(e => e.Overrides).ToListAsync(cancellationToken);

        var translations = entries.ToDictionary(
            e => e.Key,
            e => e.Overrides.FirstOrDefault(o => o.Language == language)?.Value
                 ?? e.GetDefault(language)
                 ?? e.DefaultNl
                 ?? e.Key);

        return new LocalizationBundleDto(request.Language, translations);
    }
}

public record GetTerminologyHistoryQuery(string Key) : IRequest<IReadOnlyList<TerminologyHistoryEntryDto>>;

public class GetTerminologyHistoryQueryHandler : IRequestHandler<GetTerminologyHistoryQuery, IReadOnlyList<TerminologyHistoryEntryDto>>
{
    private readonly IApplicationDbContext _db;

    public GetTerminologyHistoryQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<TerminologyHistoryEntryDto>> Handle(GetTerminologyHistoryQuery request, CancellationToken cancellationToken)
    {
        var entries = await _db.TerminologyAuditEntries
            .Where(a => a.LocalizationKey == request.Key)
            .OrderByDescending(a => a.ChangedAtUtc)
            .ToListAsync(cancellationToken);

        return entries.Select(a => new TerminologyHistoryEntryDto(
            a.Id, a.Language.ToString(), a.PreviousValue, a.NewValue,
            "—",
            a.ChangedAtUtc, a.Source.ToString())).ToList();
    }
}
