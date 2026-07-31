using System.Security.Cryptography;
using BARD.Application.Common.Interfaces;
using BARD.Application.Common.Services;
using BARD.Application.DocumentProcessing.Interfaces;
using BARD.Application.DocumentProcessing.Models;
using BARD.Domain.Entities;
using BARD.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BARD.Application.Dossiers.Commands;

public record UploadedFile(
    string FileName,
    byte[] Content,
    string ContentType);

public record ProcessDossierCommand(
    string DossierReference,
    string CompanyName,
    string EnterpriseNumber,
    string? CompanyAddressLine,
    string? CompanyPostalCode,
    string? CompanyCity,
    string? CompanyCountry,
    DateOnly RefundApplicationDate,
    UploadedFile ExcelFile,
    IReadOnlyList<UploadedFile> PdfFiles
) : IRequest<ProcessDossierResult>;

public record ProcessDossierResult(
    Guid DossierId,
    int RowCount,
    int InvoiceCount,
    int Ac4Count,
    IReadOnlyList<string> UnclassifiedFiles,
    IReadOnlyList<string> Errors
);

public class ProcessDossierCommandValidator
    : AbstractValidator<ProcessDossierCommand>
{
    public ProcessDossierCommandValidator()
    {
        RuleFor(x => x.DossierReference)
            .NotEmpty();

        RuleFor(x => x.CompanyName)
            .NotEmpty()
            .WithMessage(
                "Company name is required on the upload form.");

        RuleFor(x => x.EnterpriseNumber)
            .NotEmpty()
            .WithMessage(
                "Enterprise/VAT number is required on the upload form.");

        RuleFor(x => x.ExcelFile)
            .NotNull();

        RuleFor(x => x.PdfFiles)
            .NotEmpty()
            .WithMessage(
                "At least one dossier document (invoice or AC4) must be uploaded.");
    }
}

/// <summary>
/// Full pipeline orchestrator, ports core/pipeline.py exactly, including
/// the "never guess — route to manual review" principle: a document
/// below the classification confidence floor is left unprocessed and
/// reported back, never force-classified.
/// </summary>
public class ProcessDossierCommandHandler
    : IRequestHandler<ProcessDossierCommand, ProcessDossierResult>
{
    private const string InvoiceBlobContainer =
        "dossier-invoices";

    private const string Ac4BlobContainer =
        "dossier-ac4";

    private const string ExcelBlobContainer =
        "dossier-excel-claims";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBlobStorageService _blobStorage;
    private readonly IExcelClaimReaderService _excelReader;
    private readonly IDocumentClassifierService _documentClassifier;
    private readonly IDocumentRoleClassifierService _documentRoleClassifier;
    private readonly IInvoiceParsingService _invoiceParser;
    private readonly IAc4ParsingService _ac4Parser;
    private readonly IMatchingService _matchingService;
    private readonly IExportValidationService _exportValidation;
    private readonly IMrnValidationService _mrnValidation;
    private readonly IRefundDeadlineValidationService _deadlineValidation;
    private readonly IRefundCalculationService _refundCalculation;

    private readonly Microsoft.Extensions.Options.IOptions<
        Common.Options.BusinessRulesOptions> _businessRules;

    public ProcessDossierCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBlobStorageService blobStorage,
        IExcelClaimReaderService excelReader,
        IDocumentClassifierService documentClassifier,
        IDocumentRoleClassifierService documentRoleClassifier,
        IInvoiceParsingService invoiceParser,
        IAc4ParsingService ac4Parser,
        IMatchingService matchingService,
        IExportValidationService exportValidation,
        IMrnValidationService mrnValidation,
        IRefundDeadlineValidationService deadlineValidation,
        IRefundCalculationService refundCalculation,
        Microsoft.Extensions.Options.IOptions<
            Common.Options.BusinessRulesOptions> businessRules)
    {
        _db = db;
        _currentUser = currentUser;
        _blobStorage = blobStorage;
        _excelReader = excelReader;
        _documentClassifier = documentClassifier;
        _documentRoleClassifier = documentRoleClassifier;
        _invoiceParser = invoiceParser;
        _ac4Parser = ac4Parser;
        _matchingService = matchingService;
        _exportValidation = exportValidation;
        _mrnValidation = mrnValidation;
        _deadlineValidation = deadlineValidation;
        _refundCalculation = refundCalculation;
        _businessRules = businessRules;
    }

    public async Task<ProcessDossierResult> Handle(
        ProcessDossierCommand request,
        CancellationToken ct)
    {
        var errors = new List<string>();

        IReadOnlyList<ParsedExcelClaimRow> excelRows;

        try
        {
            using var excelStream =
                new MemoryStream(request.ExcelFile.Content);

            excelRows = _excelReader.Read(
                excelStream,
                request.ExcelFile.FileName);
        }
        catch (Exception ex)
        {
            return new ProcessDossierResult(
                Guid.Empty,
                0,
                0,
                0,
                Array.Empty<string>(),
                new[]
                {
                    $"Excel parsing failed: {ex.Message}"
                });
        }

        var invoices = new List<ParsedInvoice>();
        var ac4Declarations =
            new List<ParsedAc4Declaration>();

        var unclassifiedFiles =
            new List<string>();

        var classifications =
            new List<DocumentClassificationResult>();

        foreach (var pdfFile in request.PdfFiles)
        {
            using var classifyStream =
                new MemoryStream(pdfFile.Content);

            var classification =
                await _documentClassifier.ClassifyAsync(
                    classifyStream,
                    pdfFile.FileName,
                    ct);

            classifications.Add(classification);

            if (classification.Confidence <
                _businessRules.Value.ClassificationConfidenceFloor)
            {
                unclassifiedFiles.Add(pdfFile.FileName);
                continue;
            }

            using var parseStream =
                new MemoryStream(pdfFile.Content);

            switch (classification.DocumentKind)
            {
                case DocumentKind.Invoice:
                    invoices.Add(
                        await _invoiceParser.ParseAsync(
                            parseStream,
                            pdfFile.FileName,
                            ct));
                    break;

                case DocumentKind.Ac4Declaration:
                case DocumentKind.EadEVadDocument:
                    ac4Declarations.Add(
                        await _ac4Parser.ParseAsync(
                            parseStream,
                            pdfFile.FileName,
                            ct));
                    break;

                default:
                    unclassifiedFiles.Add(pdfFile.FileName);
                    break;
            }
        }

        var roleContext =
            new DocumentRoleClassificationContext(
                request.CompanyName,
                request.EnterpriseNumber,
                excelRows);

        var roleClassifications =
            classifications.ToDictionary(
                classification => classification.FileName,
                classification =>
                    _documentRoleClassifier.ClassifyRole(
                        classification,
                        invoices.FirstOrDefault(
                            invoice =>
                                string.Equals(
                                    invoice.SourceFile,
                                    classification.FileName,
                                    StringComparison.OrdinalIgnoreCase)),
                        ac4Declarations.FirstOrDefault(
                            ac4 =>
                                string.Equals(
                                    ac4.SourceFile,
                                    classification.FileName,
                                    StringComparison.OrdinalIgnoreCase)),
                        roleContext),
                StringComparer.OrdinalIgnoreCase);

        var salesInvoices = invoices
            .Where(invoice =>
                roleClassifications.TryGetValue(
                    invoice.SourceFile,
                    out var roleClassification)
                && roleClassification.DocumentRole
                == DocumentRole.SalesInvoice)
            .ToList();

        var matchResults =
            _matchingService.MatchAll(
                excelRows,
                salesInvoices);

        var company =
            await ResolveOrCreateCompany(request, ct);

        var dossier = Dossier.Create(
            request.DossierReference,
            company.Id,
            request.RefundApplicationDate,
            _currentUser.UserId);

        var lineByExcelRow =
            new Dictionary<int, DossierLine>();

        foreach (var match in matchResults)
        {
            var row = match.ExcelRow;

            var line = DossierLine.Create(
                dossier.Id,
                row.RowIndex,
                row.InvoiceNumber,
                row.ProductDescription,
                row.ExciseCode,
                row.Quantity,
                row.Mrn,
                row.DestinationCountry);

            line.SetMatchResult(
                match.Status,
                match.ConfidenceScore,
                null,
                match.HardBlockReason,
                BuildMatchExplanation(match));

            var (exportStatus, exportNotes) =
                _exportValidation.CheckExport(match);

            line.SetExportStatus(
                exportStatus,
                exportNotes);

            dossier.AddLine(line);

            lineByExcelRow[row.RowIndex] = line;
        }

        var linesForMrnCheck = matchResults
            .Select(m => (
                lineByExcelRow[m.ExcelRow.RowIndex].Id,
                m.ExcelRow))
            .ToList();

        var mrnResults =
            _mrnValidation.Validate(
                linesForMrnCheck,
                ac4Declarations);

        foreach (var (lineId, result) in mrnResults)
        {
            var line = dossier.Lines
                .First(l => l.Id == lineId);

            line.SetMrnCumulativeStatus(
                result.Status,
                result.Notes);

            var ac4Notes = result.Ac4Notes;
            var ac4Status = result.Ac4Status;

            if (result.MatchedAc4 is not null)
            {
                var (deadlineStatus, deadlineNotes) =
                    _deadlineValidation.CheckDeadline(
                        result.MatchedAc4,
                        request.RefundApplicationDate);

                ac4Status =
                    deadlineStatus == Ac4Status.Confirmed
                        ? ac4Status
                        : deadlineStatus;

                ac4Notes =
                    $"{ac4Notes} {deadlineNotes}".Trim();
            }

            line.SetAc4Status(
                ac4Status,
                ac4Notes);
        }

        // --- Step 6b: refund calculation (quantity x rate against the
        // ExciseRate configured for this line's excise code, resolved as
        // of the refund application date — never "today", same
        // historical-reproducibility principle as the rate versioning
        // itself). Never invents a rate: a line whose excise code has no
        // matching configured ExciseRate is left uncalculated rather than
        // guessed, consistent with "never repair incomplete evidence".
        foreach (var match in matchResults)
        {
            var line =
                lineByExcelRow[match.ExcelRow.RowIndex];

            if (string.IsNullOrWhiteSpace(line.ExciseCode)
                || line.ClaimedQuantity is null)
            {
                continue;
            }

            var exciseRate = await _db.ExciseRates
                .Include(r => r.Versions)
                .FirstOrDefaultAsync(
                    r => r.ExciseCode == line.ExciseCode
                         && r.IsActive,
                    ct);

            if (exciseRate is null)
            {
                continue;
            }

            var rateVersion =
                exciseRate.GetCurrentVersion(
                    request.RefundApplicationDate);

            var amount =
                _refundCalculation.Calculate(
                    rateVersion,
                    line.ClaimedQuantity.Value,
                    out var calcNotes);

            if (amount is not null)
            {
                line.ApplyCalculation(
                    exciseRate.ExciseCode,
                    rateVersion.Rate,
                    rateVersion.CalculationUnit,
                    amount.Value,
                    rateVersion.Id,
                    calcNotes);
            }
        }

        _db.Dossiers.Add(dossier);

        await PersistExcelDocument(
            dossier.Id,
            request.ExcelFile,
            ct);

        await PersistPdfDocuments(
            dossier,
            request.PdfFiles,
            classifications,
            invoices,
            ac4Declarations,
            roleClassifications,
            ct);

        dossier.RecomputeStatusFromLines(
            _currentUser.UserId);

        await _db.SaveChangesAsync(ct);

        return new ProcessDossierResult(
            dossier.Id,
            matchResults.Count,
            invoices.Count,
            ac4Declarations.Count,
            unclassifiedFiles,
            errors);
    }

    /// <summary>
    /// Resolves an existing Company by normalized enterprise/VAT number,
    /// or creates a new one. The upload form's explicitly supplied values
    /// are authoritative (decision #4) — this never derives identity from
    /// the Excel claim. On a re-match, contact details are refreshed but
    /// the company's identity (normalized number) never changes.
    /// </summary>
    private async Task<Company> ResolveOrCreateCompany(
        ProcessDossierCommand request,
        CancellationToken ct)
    {
        var normalized =
            CompanyNumberNormalizer.Normalize(
                request.EnterpriseNumber);

        var existing = await _db.Companies
            .FirstOrDefaultAsync(
                c => c.NormalizedEnterpriseNumber == normalized,
                ct);

        if (existing is not null)
        {
            existing.UpdateContactDetails(
                request.CompanyName,
                request.CompanyAddressLine,
                request.CompanyPostalCode,
                request.CompanyCity,
                request.CompanyCountry,
                _currentUser.UserId);

            return existing;
        }

        var company = Company.Create(
            request.CompanyName,
            request.EnterpriseNumber,
            normalized,
            _currentUser.UserId,
            request.CompanyAddressLine,
            request.CompanyPostalCode,
            request.CompanyCity,
            request.CompanyCountry);

        _db.Companies.Add(company);

        // Concurrency guard (audit finding M5): two simultaneous uploads
        // for a brand-new company can both pass the existence check above.
        // The DB's unique index on NormalizedEnterpriseNumber is the real
        // guarantee; if it rejects this insert, the other request won the
        // race — re-read and use its row instead of failing the upload.
        try
        {
            await _db.SaveChangesAsync(ct);
            return company;
        }
        catch (DbUpdateException)
        {
            _db.Companies.Remove(company);

            var winner = await _db.Companies
                .FirstOrDefaultAsync(
                    c => c.NormalizedEnterpriseNumber
                         == normalized,
                    ct);

            if (winner is null)
            {
                throw;
            }

            winner.UpdateContactDetails(
                request.CompanyName,
                request.CompanyAddressLine,
                request.CompanyPostalCode,
                request.CompanyCity,
                request.CompanyCountry,
                _currentUser.UserId);

            return winner;
        }
    }

    private static string BuildMatchExplanation(
        MatchResult match)
    {
        if (match.ScoreBreakdown is null)
        {
            return "No candidate invoice line found.";
        }

        var b = match.ScoreBreakdown;

        return
            $"invoice#={b.InvoiceNumberMatch} " +
            $"qty={b.QuantityMatch} " +
            $"excise={b.ExciseCodeMatch} " +
            $"description={b.DescriptionMatch}" +
            $"(alias={b.AliasResolved}) " +
            $"country={b.DestinationCountryMatch}. " +
            string.Join(" ", b.Notes);
    }

    private async Task PersistExcelDocument(
        Guid dossierId,
        UploadedFile excelFile,
        CancellationToken ct)
    {
        using var stream =
            new MemoryStream(excelFile.Content);

        var hash =
            Convert.ToHexString(
                SHA256.HashData(excelFile.Content));

        var blobPath =
            await _blobStorage.UploadAsync(
                ExcelBlobContainer,
                excelFile.FileName,
                stream,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ct);

        var document =
            DossierDocument.Create(
                dossierId,
                excelFile.FileName,
                blobPath,
                hash,
                excelFile.Content.Length,
                _currentUser.UserId);

        document.SetDocumentKind(
            DocumentKind.CompanyExcelClaim,
            1.0m,
            "Uploaded as the company Excel refund claim.");

        document.SetDocumentRole(
            DocumentRole.RefundClaim,
            1.0m,
            "The uploaded Excel document forms the basis of the refund claim.");

        document.SetExtractionResult(
            ExtractionMethod.ClassicalTextExtraction,
            1.0m,
            null,
            false);

        _db.DossierDocuments.Add(document);
    }

    private async Task PersistPdfDocuments(
        Dossier dossier,
        IReadOnlyList<UploadedFile> pdfFiles,
        List<DocumentClassificationResult> classifications,
        List<ParsedInvoice> invoices,
        List<ParsedAc4Declaration> ac4Declarations,
        IReadOnlyDictionary<
            string,
            DocumentRoleClassificationResult> roleClassifications,
        CancellationToken ct)
    {
        foreach (var pdfFile in pdfFiles)
        {
            var classification =
                classifications.First(
                    c => c.FileName == pdfFile.FileName);

            var hash =
                Convert.ToHexString(
                    SHA256.HashData(pdfFile.Content));

            var containerName =
                classification.DocumentKind switch
                {
                    DocumentKind.Ac4Declaration
                        or DocumentKind.EadEVadDocument
                        => Ac4BlobContainer,

                    _ => InvoiceBlobContainer
                };

            using var stream =
                new MemoryStream(pdfFile.Content);

            var blobPath =
                await _blobStorage.UploadAsync(
                    containerName,
                    pdfFile.FileName,
                    stream,
                    "application/pdf",
                    ct);

            var document =
                DossierDocument.Create(
                    dossier.Id,
                    pdfFile.FileName,
                    blobPath,
                    hash,
                    pdfFile.Content.Length,
                    _currentUser.UserId);

            document.SetDocumentKind(
                classification.DocumentKind,
                classification.Confidence,
                string.Join(
                    " ",
                    classification.Reasons));

            var invoice =
                invoices.FirstOrDefault(
                    i => i.SourceFile
                         == pdfFile.FileName);

            var ac4 =
                ac4Declarations.FirstOrDefault(
                    a => a.SourceFile
                         == pdfFile.FileName);

              var roleClassification =
                roleClassifications[
                    classification.FileName];

            document.SetDocumentRole(
                roleClassification.DocumentRole,
                roleClassification.Confidence,
                string.Join(
                    " ",
                    roleClassification.Reasons));

            if (classification.DocumentKind
                == DocumentKind.Invoice)
            {
                if (invoice is not null)
                {
                    document.SetExtractionResult(
                        invoice.ExtractionMethod,
                        invoice.ExtractionConfidence,
                        invoice.ExtractionWarnings.Count > 0
                            ? string.Join(
                                " ",
                                invoice.ExtractionWarnings)
                            : null,
                        invoice.ExtractionMethod
                        == ExtractionMethod.Ocr);

                    RecordInvoiceProvenance(
                        document,
                        invoice);
                }
            }
            else if (classification.DocumentKind
                     is DocumentKind.Ac4Declaration
                     or DocumentKind.EadEVadDocument)
            {
                if (ac4 is not null)
                {
                    document.SetExtractionResult(
                        ac4.ExtractionMethod,
                        ac4.ExtractionConfidence,
                        ac4.ExtractionWarnings.Count > 0
                            ? string.Join(
                                " ",
                                ac4.ExtractionWarnings)
                            : null,
                        ac4.ExtractionMethod
                        == ExtractionMethod.Ocr);

                    _db.Ac4Declarations.Add(
                        Domain.Entities.Ac4Declaration.Create(
                            document.Id,
                            ac4.Mrn,
                            ac4.Ac4Date,
                            ac4.Consignee,
                            ac4.ProductDescription,
                            ac4.Quantity,
                            ac4.ExciseCode,
                            ac4.ExtractionMethod,
                            ac4.ExtractionConfidence,
                            ac4.ExtractionWarnings.Count > 0
                                ? string.Join(
                                    " ",
                                    ac4.ExtractionWarnings)
                                : null));
                }
            }

            _db.DossierDocuments.Add(document);
        }
    }

    private static void RecordInvoiceProvenance(
        DossierDocument document,
        ParsedInvoice invoice)
    {
        if (invoice.InvoiceNumber is not null)
        {
            document.RecordExtractedField(
                "InvoiceNumber",
                invoice.InvoiceNumber,
                null,
                null,
                invoice.ExtractionConfidence);
        }

        if (invoice.InvoiceDate is not null)
        {
            document.RecordExtractedField(
                "InvoiceDate",
                invoice.InvoiceDate.Value.ToString("O"),
                null,
                null,
                invoice.ExtractionConfidence);
        }

        if (invoice.DestinationCountry is not null)
        {
            document.RecordExtractedField(
                "DestinationCountry",
                invoice.DestinationCountry,
                null,
                null,
                invoice.ExtractionConfidence);
        }

        foreach (var line in invoice.Lines)
        {
            document.RecordExtractedField(
                $"ProductLine[{line.LineIndex}]",
                $"{line.ProductDescription} x{line.Quantity}",
                line.SourcePage,
                line.RawTextSnippet,
                invoice.ExtractionConfidence);
        }
    }
}