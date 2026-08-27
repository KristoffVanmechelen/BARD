using BARD.Application.Common.Interfaces;
using BARD.Application.DocumentProcessing.Interfaces;
using BARD.Application.Dossiers.Commands;
using BARD.Application.Dossiers.Queries;
using BARD.Contracts.Dossiers;
using BARD.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BARD.API.Controllers;

[ApiController]
[Route("api/v1/dossiers")]
[Authorize]
public class DossiersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IBlobStorageService _blobStorage;
    private readonly IPdfTextExtractionService _pdfTextExtraction;
    private readonly IApplicationDbContext _db;
    private readonly BARD.Application.Reporting.IDossierExportService _exportService;

    public DossiersController(
        IMediator mediator,
        IBlobStorageService blobStorage,
        IPdfTextExtractionService pdfTextExtraction,
        IApplicationDbContext db,
        BARD.Application.Reporting.IDossierExportService exportService)
    {
        _mediator = mediator;
        _blobStorage = blobStorage;
        _pdfTextExtraction = pdfTextExtraction;
        _db = db;
        _exportService = exportService;
    }

    [HttpPost("search")]
    [Authorize(Policy = PermissionCodes.DossierView)]
    public async Task<ActionResult<DossierListResultDto>> Search(
        [FromBody] DossierListRequest request,
        CancellationToken ct)
        => Ok(await _mediator.Send(new GetDossierListQuery(request), ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionCodes.DossierView)]
    public async Task<ActionResult<DossierDetailDto>> GetById(
        Guid id,
        CancellationToken ct)
        => Ok(await _mediator.Send(new GetDossierDetailQuery(id), ct));

    /// <summary>
    /// Runs the full ingestion/matching/validation pipeline: officer
    /// uploads the Excel claim plus every dossier PDF together (no
    /// sorting required — documents are auto-classified server-side).
    /// </summary>
    [HttpPost("process")]
    [Authorize(Policy = PermissionCodes.DossierProcess)]
    [RequestSizeLimit(200_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ProcessDossierResult>> Process(
        [FromForm] string dossierReference,
        [FromForm] string companyName,
        [FromForm] string enterpriseNumber,
        [FromForm] string? companyAddressLine,
        [FromForm] string? companyPostalCode,
        [FromForm] string? companyCity,
        [FromForm] string? companyCountry,
        [FromForm] DateOnly refundApplicationDate,
        IFormFile excelFile,
        List<IFormFile> pdfFiles,
        CancellationToken ct)
    {
        async Task<UploadedFile> ToUploadedFile(IFormFile file)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);

            return new UploadedFile(
                file.FileName,
                ms.ToArray(),
                file.ContentType);
        }

        var excel = await ToUploadedFile(excelFile);

        var pdfs = new List<UploadedFile>();

        foreach (var file in pdfFiles)
        {
            pdfs.Add(await ToUploadedFile(file));
        }

        var result = await _mediator.Send(
            new ProcessDossierCommand(
                dossierReference,
                companyName,
                enterpriseNumber,
                companyAddressLine,
                companyPostalCode,
                companyCity,
                companyCountry,
                refundApplicationDate,
                excel,
                pdfs),
            ct);

        return Ok(result);
    }

    /// <summary>
    /// On-demand AI-assist: officer explicitly requests a second
    /// extraction attempt for ONE document whose classical parsing left
    /// fields unresolved. Separately permissioned from DossierProcess —
    /// never reachable from the automatic ingestion pipeline.
    /// </summary>
    [HttpPost("documents/{documentId:guid}/ai-assist")]
    [Authorize(Policy = PermissionCodes.DossierAiAssist)]
    public async Task<ActionResult<BARD.Application.AiAssist.AiAssistResult>> RequestAiAssist(
        Guid documentId,
        [FromBody] IReadOnlyList<string> fieldsNeeded,
        CancellationToken ct)
    {
        var document = await _db.DossierDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId, ct);

        if (document is null)
        {
            return NotFound();
        }

        using var stream = await _blobStorage.DownloadAsync(
            document.BlobStoragePath,
            ct);

        var extraction = await _pdfTextExtraction.ExtractAsync(
            stream,
            document.OriginalFileName,
            ct);

        var result = await _mediator.Send(
            new RequestAiAssistExtractionCommand(
                documentId,
                extraction.FullText,
                fieldsNeeded),
            ct);

        return Ok(result);
    }

    /// <summary>
    /// Phase 4: downloadable validation report. Starts from the original
    /// preserved Excel workbook (decision #5) and appends BARD's
    /// validation/status/confidence/calculation/officer-decision columns
    /// with conditional formatting.
    /// </summary>
    [HttpGet("{id:guid}/export")]
    [Authorize(Policy = PermissionCodes.DossierView)]
    public async Task<IActionResult> Export(
        Guid id,
        CancellationToken ct)
    {
        var result = await _exportService.GenerateReportAsync(id, ct);

        return File(
            result.Content,
            result.ContentType,
            result.FileName);
    }

    /// <summary>
    /// Allows an authorised dossier reviewer to correct the contextual
    /// role of an invoice. The correction is recorded as an authoritative
    /// user decision and cannot be overwritten by automatic classification.
    /// </summary>
    [HttpPut("documents/{documentId:guid}/invoice-role")]
    [Authorize(Policy = PermissionCodes.DossierReview)]
    public async Task<IActionResult> CorrectInvoiceRole(
        Guid documentId,
        [FromBody] CorrectInvoiceRoleRequest request,
        CancellationToken ct)
    {
        await _mediator.Send(
            new CorrectInvoiceRoleCommand(
                documentId,
                request),
            ct);

        return NoContent();
    }

    /// <summary>
    /// The only endpoint in the system that can move a dossier line out
    /// of PendingReview. Requires the DossierReview permission — never
    /// reachable from an automatic/system process (Core Philosophy).
    /// </summary>
    [HttpPost("lines/decision")]
    [Authorize(Policy = PermissionCodes.DossierReview)]
    public async Task<IActionResult> RecordOfficerDecision(
        [FromBody] RecordOfficerDecisionRequest request,
        CancellationToken ct)
    {
        await _mediator.Send(
            new RecordOfficerDecisionCommand(request),
            ct);

        return NoContent();
    }
}