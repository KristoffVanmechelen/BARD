using BARD.Domain.Entities;
using BARD.Domain.Enums;

namespace BARD.Application.DocumentProcessing.Interfaces;

/// <summary>
/// Computes the refund amount for one dossier line against a specific,
/// resolved ExciseRateVersion.
///
/// IMPORTANT — scope of this implementation: this computes the baseline
/// "quantity x rate" calculation only. The BERDS Functional Specification
/// (SFS Ch.09 "Calculation Engine") describes a richer methodology —
/// volume conversions, alcohol-percentage-based adjustments, and Plato
/// calculations for beer — none of which were implemented in the Python
/// prototype either (core/validation/future/refund_calculation.py was an
/// explicit NotImplementedError stub there). Implementing those
/// conversions correctly requires domain-expert-verified formulas that
/// are not present anywhere in the available documentation with concrete
/// coefficients — inventing them would violate "never guess / never
/// invent business rules". This service is therefore the direct,
/// undiminished port of what the prototype actually computed (nothing),
/// PLUS the one calculation that IS unambiguous from the calculation
/// unit alone (a straight per-unit rate application) so the pipeline is
/// not left entirely without a number where the unit is simple.
///
/// PerDegreePlatoPerHectolitre specifically requires a Plato-degree
/// input that does not exist anywhere in the current domain model
/// (Invoice/DossierLine has no alcohol/Plato field) — computation for
/// that unit is deliberately refused (returns null) rather than
/// silently computing a wrong number.
/// </summary>
public interface IRefundCalculationService
{
    /// <summary>Returns the calculated amount, or null if the calculation cannot be performed without guessing.</summary>
    decimal? Calculate(ExciseRateVersion rateVersion, decimal quantity, out string? notes);
}
