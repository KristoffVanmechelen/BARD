using BARD.Application.Common.Exceptions;
using BARD.Application.Common.Interfaces;
using BARD.Contracts.Dossiers;
using BARD.Domain.Entities;
using BARD.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BARD.Application.Dossiers.Queries;

public record GetDossierListQuery(DossierListRequest Request)
    : IRequest<DossierListResultDto>;

public class GetDossierListQueryHandler
    : IRequestHandler<GetDossierListQuery, DossierListResultDto>
{
    private readonly IApplicationDbContext _db;

    public GetDossierListQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DossierListResultDto> Handle(
        GetDossierListQuery request,
        CancellationToken cancellationToken)
    {
        var req = request.Request;

        var query = _db.Dossiers
            .Include(d => d.Lines)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(req.Status))
        {
            query = query.Where(
                d => d.Status == Enum.Parse<DossierStatus>(req.Status));
        }

        if (!string.IsNullOrWhiteSpace(req.SearchText))
        {
            query = query.Where(
                d => d.DossierReference.Contains(req.SearchText));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var dossiers = await query
            .OrderByDescending(d => d.CreatedAtUtc)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .ToListAsync(cancellationToken);

        var companyIds = dossiers
            .Select(d => d.CompanyId)
            .Distinct()
            .ToList();

        var companies = await _db.Companies
            .Where(c => companyIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var dtos = dossiers
            .Select(d => new DossierSummaryDto(
                d.Id,
                d.DossierReference,
                companies.TryGetValue(d.CompanyId, out var company)
                    ? company.Name
                    : "(unknown company)",
                d.RefundApplicationDate,
                d.Status.ToString(),
                d.Lines.Count,
                d.Lines.Count(l => l.RequiresManualReview()),
                d.Lines.Sum(l => l.CalculatedRefundAmount)))
            .ToList();

        return new DossierListResultDto(
            dtos,
            totalCount,
            req.Page,
            req.PageSize);
    }
}

public record GetDossierDetailQuery(Guid DossierId)
    : IRequest<DossierDetailDto>;

public class GetDossierDetailQueryHandler
    : IRequestHandler<GetDossierDetailQuery, DossierDetailDto>
{
    private readonly IApplicationDbContext _db;

    public GetDossierDetailQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DossierDetailDto> Handle(
        GetDossierDetailQuery request,
        CancellationToken cancellationToken)
    {
        var dossier = await _db.Dossiers
            .Include(d => d.Lines)
            .Include(d => d.Documents)
            .FirstOrDefaultAsync(
                d => d.Id == request.DossierId,
                cancellationToken)
            ?? throw new NotFoundException(
                nameof(Dossier),
                request.DossierId);

        var company = await _db.Companies
            .FirstOrDefaultAsync(
                c => c.Id == dossier.CompanyId,
                cancellationToken);

        var reviewerIds = dossier.Lines
            .Where(l => l.ReviewedByUserId != null)
            .Select(l => l.ReviewedByUserId!.Value)
            .Concat(
                dossier.Documents
                    .Where(d =>
                        d.RoleConfirmedByUserId != null)
                    .Select(d =>
                        d.RoleConfirmedByUserId!.Value))
            .Distinct()
            .ToList();

        var reviewers = await _db.Users
            .Where(u => reviewerIds.Contains(u.Id))
            .ToDictionaryAsync(
                u => u.Id,
                cancellationToken);

        var lineDtos = dossier.Lines
            .OrderBy(l => l.RowIndex)
            .Select(l => new DossierLineDto(
                l.Id,
                l.RowIndex,
                l.ClaimedInvoiceNumber,
                l.ClaimedProductDescription,
                l.ExciseCode,
                l.ClaimedQuantity,
                l.Mrn,
                l.ClaimedDestinationCountry,
                l.MatchStatus.ToString(),
                l.ConfidenceScore,
                l.HardBlockReason,
                l.MatchExplanation,
                l.ExportStatus.ToString(),
                l.ExportCheckNotes,
                l.MrnCumulativeStatus.ToString(),
                l.MrnCumulativeNotes,
                l.Ac4Status.ToString(),
                l.Ac4Notes,
                l.OfficerDecision.ToString(),
                l.OfficerRemarks,
                l.ReviewedByUserId != null
                    && reviewers.TryGetValue(
                        l.ReviewedByUserId.Value,
                        out var reviewer)
                        ? reviewer.DisplayName
                        : null,
                l.ReviewedAtUtc,
                l.CalculatedRefundAmount,
                l.CalculationNotes,
                l.RequiresManualReview()))
            .ToList();

        var documentDtos = dossier.Documents
            .Select(doc => new DossierDocumentDto(
                doc.Id,
                doc.OriginalFileName,
                doc.DocumentKind.ToString(),
                doc.ClassificationConfidence,
                doc.DocumentRole.ToString(),
                doc.RoleConfidence,
                doc.RoleReasons,
                doc.RoleConfirmedByUser,
                doc.RoleConfirmedByUserId != null
                    && reviewers.TryGetValue(
                        doc.RoleConfirmedByUserId.Value,
                        out var roleConfirmer)
                        ? roleConfirmer.DisplayName
                        : null,
                doc.RoleConfirmedAtUtc,
                doc.ExtractionMethod.ToString(),
                doc.ExtractionConfidence,
                doc.OcrWasRequired,
                doc.ExtractionWarnings))
            .ToList();

        return new DossierDetailDto(
            dossier.Id,
            dossier.DossierReference,
            company?.Name ?? "(unknown company)",
            company?.EnterpriseNumber,
            dossier.RefundApplicationDate,
            dossier.Status.ToString(),
            lineDtos,
            documentDtos);
    }
}