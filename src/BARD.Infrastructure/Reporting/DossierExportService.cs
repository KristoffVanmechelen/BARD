using BARD.Application.Common.Exceptions;
using BARD.Application.Common.Interfaces;
using BARD.Application.Reporting;
using BARD.Domain.Entities;
using BARD.Domain.Enums;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace BARD.Infrastructure.Reporting;

/// <summary>See IDossierExportService for the documented scope of this implementation.</summary>
public class DossierExportService : IDossierExportService
{
    private static readonly string[] AppendedColumnHeaders =
    {
        "BARD Match Status",
        "BARD Confidence %",
        "BARD Export Status",
        "BARD MRN Cumulative Status",
        "BARD AC4 Status",
        "BARD Calculated Refund",
        "BARD Calculation Notes",
        "BARD Officer Decision",
        "BARD Officer Remarks",
        "BARD Reviewed By",
        "BARD Reviewed At",
    };

    // Matches the Python prototype's excel_export.py colour scheme exactly.
    private const string ColourGreen = "C6EFCE";
    private const string ColourOrange = "FFEB9C";
    private const string ColourRed = "FFC7CE";

    private readonly IApplicationDbContext _db;
    private readonly IBlobStorageService _blobStorage;

    public DossierExportService(IApplicationDbContext db, IBlobStorageService blobStorage)
    {
        _db = db;
        _blobStorage = blobStorage;
    }

    public async Task<DossierExportResult> GenerateReportAsync(Guid dossierId, CancellationToken ct = default)
    {
        var dossier = await _db.Dossiers
            .Include(d => d.Lines)
            .Include(d => d.Documents)
            .FirstOrDefaultAsync(d => d.Id == dossierId, ct)
            ?? throw new NotFoundException(nameof(Dossier), dossierId);

        var excelDocument = dossier.Documents.FirstOrDefault(d => d.DocumentType == DocumentType.CompanyExcelClaim)
            ?? throw new BusinessRuleViolationException(
                $"Dossier '{dossier.DossierReference}' has no preserved original Excel claim document — cannot export.",
                "errors.dossier.no_original_excel",
                new Dictionary<string, string> { ["reference"] = dossier.DossierReference });

        var reviewerIds = dossier.Lines.Where(l => l.ReviewedByUserId != null).Select(l => l.ReviewedByUserId!.Value).Distinct().ToList();
        var reviewers = await _db.Users.Where(u => reviewerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);

        using var originalStream = await _blobStorage.DownloadAsync(excelDocument.BlobStoragePath, ct);
        using var memoryStream = new MemoryStream();
        await originalStream.CopyToAsync(memoryStream, ct);
        memoryStream.Position = 0;

        using var workbook = new XLWorkbook(memoryStream);
        var worksheet = workbook.Worksheets.First();

        var headerRow = worksheet.FirstRowUsed()
            ?? throw new BusinessRuleViolationException("Original Excel workbook has no header row — cannot export.");

        var lastOriginalColumn = headerRow.LastCellUsed()?.Address.ColumnNumber
            ?? throw new BusinessRuleViolationException("Original Excel workbook header row is empty — cannot export.");

        for (var i = 0; i < AppendedColumnHeaders.Length; i++)
        {
            var cell = headerRow.Cell(lastOriginalColumn + 1 + i);
            cell.Value = AppendedColumnHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#404040");
            cell.Style.Font.FontColor = XLColor.White;
        }

        var dataRows = worksheet.RowsUsed().Skip(1).ToList();
        var maxLineRowIndex = dossier.Lines.Count > 0 ? dossier.Lines.Max(l => l.RowIndex) : -1;

        // Defensive integrity check (audit finding M4): the export
        // relies on the original workbook's row enumeration order still
        // matching what ExcelClaimReaderService saw at ingestion time.
        // If the blob was modified in a way that changed the used-range
        // row count, refuse to silently misalign data rather than
        // producing a report with values on the wrong rows.
        if (dataRows.Count <= maxLineRowIndex)
            throw new BusinessRuleViolationException(
                $"Dossier '{dossier.DossierReference}': the preserved original workbook has {dataRows.Count} data row(s), " +
                $"but validation results reference row index {maxLineRowIndex}. The original file may have been modified " +
                "after upload — cannot safely export.",
                "errors.dossier.export_row_mismatch",
                new Dictionary<string, string>
                {
                    ["reference"] = dossier.DossierReference,
                    ["rowCount"] = dataRows.Count.ToString(),
                    ["maxIndex"] = maxLineRowIndex.ToString(),
                });
        var linesByRowIndex = dossier.Lines.ToDictionary(l => l.RowIndex);

        for (var rowIndex = 0; rowIndex < dataRows.Count; rowIndex++)
        {
            if (!linesByRowIndex.TryGetValue(rowIndex, out var line))
                continue;

            var row = dataRows[rowIndex];
            var colour = RowColour(line);

            var reviewerName = line.ReviewedByUserId != null && reviewers.TryGetValue(line.ReviewedByUserId.Value, out var reviewer)
                ? reviewer.DisplayName
                : "";

            var values = new object?[]
            {
                line.MatchStatus.ToString(),
                line.ConfidenceScore,
                line.ExportStatus.ToString(),
                line.MrnCumulativeStatus.ToString(),
                line.Ac4Status.ToString(),
                line.CalculatedRefundAmount,
                line.CalculationNotes,
                line.OfficerDecision.ToString(),
                line.OfficerRemarks,
                reviewerName,
                line.ReviewedAtUtc?.ToString("yyyy-MM-dd HH:mm"),
            };

            for (var i = 0; i < values.Length; i++)
            {
                var cell = row.Cell(lastOriginalColumn + 1 + i);
                SetCellValue(cell, values[i]);
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml($"#{colour}");
            }

            for (var c = 1; c <= lastOriginalColumn; c++)
                row.Cell(c).Style.Fill.BackgroundColor = XLColor.FromHtml($"#{colour}");
        }

        worksheet.Columns(lastOriginalColumn + 1, lastOriginalColumn + AppendedColumnHeaders.Length).AdjustToContents();

        using var outputStream = new MemoryStream();
        workbook.SaveAs(outputStream);

        var fileName = $"{dossier.DossierReference}_validation_report.xlsx";
        return new DossierExportResult(outputStream.ToArray(), fileName,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    /// <summary>
    /// Explicit, version-safe typed cell assignment (audit finding M3 —
    /// replaces a static factory call whose existence in the pinned
    /// ClosedXML version could not be verified without a real build).
    /// Uses only IXLCell.Value's long-established typed setter overloads.
    /// </summary>
    private static void SetCellValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                cell.Value = "";
                break;
            case string s:
                cell.Value = s;
                break;
            case decimal d:
                cell.Value = d;
                break;
            case double db:
                cell.Value = db;
                break;
            case int i:
                cell.Value = i;
                break;
            case DateTime dt:
                cell.Value = dt;
                break;
            case bool b:
                cell.Value = b;
                break;
            default:
                cell.Value = value.ToString() ?? "";
                break;
        }
    }

    internal static string RowColour(DossierLine line)
    {
        if (line.OfficerDecision == OfficerDecision.Rejected) return ColourRed;
        if (line.OfficerDecision == OfficerDecision.Approved) return ColourGreen;

        if (line.MatchStatus == MatchStatus.NoMatch) return ColourRed;
        if (line.ExportStatus == ExportConfirmationStatus.NotConfirmed) return ColourRed;

        if (line.MatchStatus == MatchStatus.AutoMatch
            && line.ExportStatus == ExportConfirmationStatus.Confirmed
            && !line.RequiresManualReview())
            return ColourGreen;

        return ColourOrange;
    }
}
