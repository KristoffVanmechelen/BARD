using System.Globalization;
using BARD.Application.Common.Exceptions;
using BARD.Application.DocumentProcessing.Interfaces;
using BARD.Application.DocumentProcessing.Models;
using ClosedXML.Excel;

namespace BARD.Infrastructure.DocumentProcessing;

/// <summary>Port of core/ingestion/excel_reader.py.</summary>
public class ExcelClaimReaderService : IExcelClaimReaderService
{
    private static readonly Dictionary<string, string[]> ColumnAliases = new()
    {
        ["invoice_number"] = new[]
        {
            "invoice number", "invoice no", "invoice nr", "facture", "factuur",
            "factuurnummer", "numero facture", "invoice", "inv no", "inv number",
        },
        ["product_description"] = new[]
        {
            "product description", "description", "product", "omschrijving",
            "designation", "article", "product name", "beschrijving",
        },
        ["excise_code"] = new[]
        {
            "excise code", "excise", "accijnscode", "code accise", "excise duty code", "e-code", "ecode",
        },
        ["quantity"] = new[] { "quantity", "qty", "hoeveelheid", "quantite", "qte", "aantal" },
        ["mrn"] = new[] { "mrn", "movement reference number", "referentienummer" },
        ["destination_country"] = new[]
        {
            "destination country", "destination", "country", "land",
            "pays de destination", "bestemmingsland",
        },
    };

    private static readonly string[] RequiredFields = { "invoice_number", "product_description", "excise_code", "quantity" };

    private static string Normalise(string header) =>
        string.Join(" ", header.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public IReadOnlyList<ParsedExcelClaimRow> Read(Stream excelStream, string fileName)
    {
        using var workbook = new XLWorkbook(excelStream);
        var worksheet = workbook.Worksheets.First();
        var headerRow = worksheet.FirstRowUsed()
            ?? throw new BusinessRuleViolationException($"Excel file '{fileName}' contains no data rows.");

        var columnHeaders = new Dictionary<int, string>();
        foreach (var cell in headerRow.CellsUsed())
            columnHeaders[cell.Address.ColumnNumber] = cell.GetString();

        var normalisedLookup = columnHeaders.ToDictionary(kv => Normalise(kv.Value), kv => kv.Key);

        var columnMap = new Dictionary<string, int>();
        foreach (var (canonicalField, aliases) in ColumnAliases)
        {
            foreach (var alias in aliases)
            {
                if (normalisedLookup.TryGetValue(alias, out var colIndex))
                {
                    columnMap[canonicalField] = colIndex;
                    break;
                }
            }
        }

        var missingRequired = RequiredFields.Where(f => !columnMap.ContainsKey(f)).ToList();
        if (missingRequired.Count > 0)
            throw new BusinessRuleViolationException(
                $"Could not identify the following required columns in the uploaded Excel file: " +
                $"{string.Join(", ", missingRequired)}. Columns found in file: {string.Join(", ", columnHeaders.Values)}. " +
                "Add the actual header name to the column alias configuration, or fix the source file.");

        var dataRows = worksheet.RowsUsed().Skip(1).ToList();
        if (dataRows.Count == 0)
            throw new BusinessRuleViolationException($"Excel file '{fileName}' contains no data rows.");

        var results = new List<ParsedExcelClaimRow>();
        var rowIndex = 0;

        foreach (var row in dataRows)
        {
            var rawRow = new Dictionary<string, string?>();
            foreach (var (col, header) in columnHeaders)
                rawRow[header] = row.Cell(col).IsEmpty() ? null : row.Cell(col).GetString();

            string? SafeStr(string field) =>
                columnMap.TryGetValue(field, out var col) && !row.Cell(col).IsEmpty() ? row.Cell(col).GetString().Trim() : null;

            decimal? SafeDecimal(string field)
            {
                if (!columnMap.TryGetValue(field, out var col) || row.Cell(col).IsEmpty()) return null;
                var cell = row.Cell(col);
                if (cell.TryGetValue(out double d)) return (decimal)d;
                return decimal.TryParse(cell.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
            }

            results.Add(new ParsedExcelClaimRow(
                rowIndex,
                SafeStr("invoice_number"),
                SafeStr("product_description"),
                SafeStr("excise_code"),
                SafeDecimal("quantity"),
                columnMap.ContainsKey("mrn") ? SafeStr("mrn") : null,
                columnMap.ContainsKey("destination_country") ? SafeStr("destination_country") : null,
                rawRow));

            rowIndex++;
        }

        return results;
    }
}
