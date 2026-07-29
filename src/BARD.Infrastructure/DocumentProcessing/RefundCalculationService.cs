using BARD.Application.DocumentProcessing.Interfaces;
using BARD.Domain.Entities;
using BARD.Domain.Enums;

namespace BARD.Infrastructure.DocumentProcessing;

/// <summary>See IRefundCalculationService for the documented scope/limitation of this implementation.</summary>
public class RefundCalculationService : IRefundCalculationService
{
    public decimal? Calculate(ExciseRateVersion rateVersion, decimal quantity, out string? notes)
    {
        switch (rateVersion.CalculationUnit)
        {
            case ExciseCalculationUnit.PerHectolitre:
            case ExciseCalculationUnit.PerHectolitreOfPureAlcohol:
            case ExciseCalculationUnit.PerHectolitreAlcoholicStrength:
                // Straight per-unit application: refund = quantity x rate.
                // Correct ONLY if the claimed quantity is already expressed
                // in the same unit as the rate (e.g. hectolitres of pure
                // alcohol already computed upstream) — the pipeline does
                // not currently perform alcohol-percentage-based volume
                // conversion (see interface doc). Flagged in notes so the
                // officer sees this is a direct multiplication, not a
                // fully converted regulatory calculation.
                notes = "Calculated as quantity x rate. No alcohol-strength/volume conversion applied — verify the " +
                        "claimed quantity is already expressed in the rate's unit before relying on this figure.";
                return quantity * rateVersion.Rate;

            case ExciseCalculationUnit.PerDegreePlatoPerHectolitre:
                // Requires a Plato-degree value that does not exist
                // anywhere in the current domain model (no field on
                // Invoice/DossierLine/ExcelClaimRow carries it). Refusing
                // to compute rather than guessing a Plato value.
                notes = "Cannot calculate: this excise code's unit requires a Plato-degree value that is not " +
                        "captured anywhere in the current data model. Manual calculation required.";
                return null;

            case ExciseCalculationUnit.Other:
            default:
                notes = $"Cannot calculate: calculation unit '{rateVersion.CalculationUnit}' has no defined formula. " +
                        "Manual calculation required.";
                return null;
        }
    }
}
