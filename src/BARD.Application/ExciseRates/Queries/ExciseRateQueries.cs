using BARD.Application.Common.Exceptions;
using BARD.Application.Common.Interfaces;
using BARD.Contracts.ExciseRates;
using BARD.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BARD.Application.ExciseRates.Queries;

public record GetExciseRatesQuery(bool? ActiveOnly) : IRequest<IReadOnlyList<ExciseRateDto>>;

public class GetExciseRatesQueryHandler : IRequestHandler<GetExciseRatesQuery, IReadOnlyList<ExciseRateDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public GetExciseRatesQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ExciseRateDto>> Handle(GetExciseRatesQuery request, CancellationToken cancellationToken)
    {
        var query = _db.ExciseRates.Include(r => r.Versions).AsQueryable();
        if (request.ActiveOnly == true)
            query = query.Where(r => r.IsActive);

        var rates = await query.ToListAsync(cancellationToken);
        var today = _clock.TodayUtc;

        return rates.Select(r =>
        {
            var current = r.GetCurrentVersion(today);
            return new ExciseRateDto(r.Id, r.ExciseCode, r.Description, current.Rate,
                current.CalculationUnit.ToString(), current.EffectiveFrom, r.IsActive,
                r.AdministrativeComment, r.ModifiedAtUtc);
        }).ToList();
    }
}

public record GetExciseRateDetailQuery(Guid ExciseRateId) : IRequest<ExciseRateDetailDto>;

public class GetExciseRateDetailQueryHandler : IRequestHandler<GetExciseRateDetailQuery, ExciseRateDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public GetExciseRateDetailQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<ExciseRateDetailDto> Handle(GetExciseRateDetailQuery request, CancellationToken cancellationToken)
    {
        var rate = await _db.ExciseRates
            .Include(r => r.Versions)
            .FirstOrDefaultAsync(r => r.Id == request.ExciseRateId, cancellationToken)
            ?? throw new NotFoundException(nameof(ExciseRate), request.ExciseRateId);

        var today = _clock.TodayUtc;
        var current = rate.GetCurrentVersion(today);

        var dto = new ExciseRateDto(rate.Id, rate.ExciseCode, rate.Description, current.Rate,
            current.CalculationUnit.ToString(), current.EffectiveFrom, rate.IsActive,
            rate.AdministrativeComment, rate.ModifiedAtUtc);

        var versions = rate.Versions
            .OrderByDescending(v => v.EffectiveFrom)
            .Select(v => new ExciseRateVersionDto(v.Id, v.Rate, v.CalculationUnit.ToString(), v.EffectiveFrom, v.CreatedAtUtc, "—"))
            .ToList();

        return new ExciseRateDetailDto(dto, versions);
    }
}

public record GetExciseRateAuditHistoryQuery(Guid ExciseRateId) : IRequest<IReadOnlyList<ExciseRateAuditEntryDto>>;

public class GetExciseRateAuditHistoryQueryHandler : IRequestHandler<GetExciseRateAuditHistoryQuery, IReadOnlyList<ExciseRateAuditEntryDto>>
{
    private readonly IApplicationDbContext _db;

    public GetExciseRateAuditHistoryQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ExciseRateAuditEntryDto>> Handle(GetExciseRateAuditHistoryQuery request, CancellationToken cancellationToken)
    {
        var entries = await _db.ExciseRateAuditEntries
            .Where(a => a.ExciseRateId == request.ExciseRateId)
            .OrderByDescending(a => a.ChangedAtUtc)
            .ToListAsync(cancellationToken);

        return entries.Select(a => new ExciseRateAuditEntryDto(
            a.Id, a.PreviousRate, a.NewRate,
            a.PreviousUnit?.ToString(), a.NewUnit.ToString(),
            a.PreviousActiveStatus, a.NewActiveStatus,
            "—",
            a.ChangedAtUtc, a.Reason)).ToList();
    }
}
