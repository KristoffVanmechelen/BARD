using BARD.Application.DocumentProcessing.Interfaces;
using BARD.Application.DocumentProcessing.Models;
using BARD.Domain.Enums;

namespace BARD.Infrastructure.DocumentProcessing;

/// <summary>
/// Port of core/validation/mrn_validation.py. Batch operation (needs to
/// see every row sharing an MRN at once); no tolerance is applied when
/// comparing cumulative claimed quantity against the AC4-declared
/// quantity, per the confirmed "no rounding" regulatory requirement.
/// </summary>
public class MrnValidationService : IMrnValidationService
{
    private static string NormaliseMrn(string? value) => (value ?? "").Trim().ToUpperInvariant();

    public IReadOnlyDictionary<Guid, (MrnCumulativeStatus Status, string Notes, Ac4Status Ac4Status, string Ac4Notes, ParsedAc4Declaration? MatchedAc4)>
        Validate(IReadOnlyList<(Guid LineId, ParsedExcelClaimRow Row)> lines, IReadOnlyList<ParsedAc4Declaration> ac4Declarations)
    {
        var ac4ByMrn = ac4Declarations
            .Where(a => !string.IsNullOrEmpty(a.Mrn))
            .GroupBy(a => NormaliseMrn(a.Mrn))
            .ToDictionary(g => g.Key, g => g.First());

        var rowsByMrn = lines
            .Where(l => !string.IsNullOrEmpty(l.Row.Mrn))
            .GroupBy(l => NormaliseMrn(l.Row.Mrn))
            .ToDictionary(g => g.Key, g => g.ToList());

        var results = new Dictionary<Guid, (MrnCumulativeStatus, string, Ac4Status, string, ParsedAc4Declaration?)>();

        foreach (var (lineId, row) in lines)
        {
            var mrn = NormaliseMrn(row.Mrn);

            if (mrn == "")
            {
                results[lineId] = (MrnCumulativeStatus.Uncertain, "No MRN present on this row — cannot validate against AC4.",
                    Ac4Status.NotChecked, "", null);
                continue;
            }

            if (!ac4ByMrn.TryGetValue(mrn, out var ac4))
            {
                results[lineId] = (MrnCumulativeStatus.Uncertain, $"No AC4 declaration found for MRN '{row.Mrn}'.",
                    Ac4Status.NotConfirmed, "No matching AC4 uploaded for this MRN.", null);
                continue;
            }

            if (ac4.Quantity is null)
            {
                results[lineId] = (MrnCumulativeStatus.Uncertain, "AC4 quantity could not be extracted — cannot validate cumulative total.",
                    Ac4Status.Confirmed, $"AC4 declaration found for MRN '{ac4.Mrn}'.", ac4);
                continue;
            }

            var siblingRows = rowsByMrn[mrn];
            var cumulative = siblingRows.Sum(r => r.Row.Quantity ?? 0m);

            if (cumulative > ac4.Quantity)
            {
                results[lineId] = (MrnCumulativeStatus.Exceeded,
                    $"Cumulative claimed quantity for MRN '{ac4.Mrn}' is {cumulative}, exceeding the AC4-declared quantity " +
                    $"of {ac4.Quantity}. ({siblingRows.Count} row(s) share this MRN.)",
                    Ac4Status.Confirmed, $"AC4 declaration found for MRN '{ac4.Mrn}'.", ac4);
            }
            else
            {
                results[lineId] = (MrnCumulativeStatus.WithinLimit,
                    $"Cumulative claimed quantity for MRN '{ac4.Mrn}' is {cumulative}, within the AC4-declared quantity of {ac4.Quantity}.",
                    Ac4Status.Confirmed, $"AC4 declaration found for MRN '{ac4.Mrn}'.", ac4);
            }
        }

        return results;
    }
}
