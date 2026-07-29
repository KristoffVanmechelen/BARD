namespace BARD.Application.Reporting;

public record DossierExportResult(byte[] Content, string FileName, string ContentType);

/// <summary>
/// Ports core/reporting/report_builder.py + excel_export.py, adapted for
/// authoritative decision #5: rather than reconstructing a workbook from
/// parsed domain entities, this opens the ORIGINAL uploaded Excel
/// workbook (preserved unchanged in Blob Storage as a DossierDocument
/// since Phase 2's implementation), appends the documented BARD
/// validation/status/confidence/calculation/officer-decision columns to
/// the original worksheet, and applies the documented conditional
/// formatting — preserving every original column, row, and source value.
/// </summary>
public interface IDossierExportService
{
    Task<DossierExportResult> GenerateReportAsync(Guid dossierId, CancellationToken ct = default);
}
