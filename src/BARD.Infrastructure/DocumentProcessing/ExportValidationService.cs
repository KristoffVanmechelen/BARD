using BARD.Application.DocumentProcessing.Interfaces;
using BARD.Application.DocumentProcessing.Models;
using BARD.Domain.Enums;

namespace BARD.Infrastructure.DocumentProcessing;

/// <summary>Port of core/validation/export_check.py.</summary>
public class ExportValidationService : IExportValidationService
{
    private static readonly HashSet<string> BelgiumCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "BE", "BELGIUM", "BELGIE", "BELGIQUE",
    };

    private static string Normalise(string? value) => (value ?? "").Trim().ToUpperInvariant();

    public (ExportConfirmationStatus Status, string Notes) CheckExport(MatchResult matchResult)
    {
        var excelCountry = Normalise(matchResult.ExcelRow.DestinationCountry);
        var invoiceCountry = matchResult.MatchedInvoice is not null ? Normalise(matchResult.MatchedInvoice.DestinationCountry) : "";

        if (invoiceCountry != "")
        {
            if (BelgiumCodes.Contains(invoiceCountry))
                return (ExportConfirmationStatus.NotConfirmed,
                    $"Invoice delivery address indicates Belgium ('{matchResult.MatchedInvoice!.DestinationCountry}') — " +
                    "not an export, refund not applicable on this basis.");

            return (ExportConfirmationStatus.Confirmed,
                $"Invoice delivery address confirms export to '{matchResult.MatchedInvoice!.DestinationCountry}'.");
        }

        if (excelCountry != "")
        {
            if (BelgiumCodes.Contains(excelCountry))
                return (ExportConfirmationStatus.NotConfirmed,
                    $"Excel row indicates Belgium ('{matchResult.ExcelRow.DestinationCountry}') as destination — " +
                    "not an export. Invoice delivery address unavailable to cross-check.");

            return (ExportConfirmationStatus.Uncertain,
                $"Excel row states destination '{matchResult.ExcelRow.DestinationCountry}', but the matched invoice's " +
                "delivery address could not confirm this — verify manually.");
        }

        return (ExportConfirmationStatus.Uncertain,
            "No destination country available from either the Excel row or the matched invoice — " +
            "export cannot be confirmed automatically.");
    }
}
