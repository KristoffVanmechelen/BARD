using BARD.Application.Common.Options;
using BARD.Application.DocumentProcessing.Interfaces;
using BARD.Application.DocumentProcessing.Models;
using BARD.Domain.Enums;
using Microsoft.Extensions.Options;

namespace BARD.Infrastructure.DocumentProcessing;

/// <summary>
/// Port of core/validation/deadline_check.py. Validates the AC4 date
/// against the refund APPLICATION date — never against "today" — per
/// the confirmed business requirement, since a claim can be reviewed
/// long after it was submitted.
/// </summary>
public class RefundDeadlineValidationService : IRefundDeadlineValidationService
{
    private readonly BusinessRulesOptions _businessRules;

    public RefundDeadlineValidationService(IOptions<BusinessRulesOptions> businessRules) => _businessRules = businessRules.Value;

    private static double MonthsBetween(DateOnly start, DateOnly end)
    {
        var days = end.DayNumber - start.DayNumber;
        return days / 30.436875; // average month length, matches the Python prototype exactly
    }

    public (Ac4Status Status, string Notes) CheckDeadline(ParsedAc4Declaration ac4, DateOnly refundApplicationDate)
    {
        if (ac4.Ac4Date is null)
            return (Ac4Status.Uncertain, "AC4 date unavailable — deadline could not be checked.");

        var elapsedMonths = MonthsBetween(ac4.Ac4Date.Value, refundApplicationDate);

        if (elapsedMonths < 0)
            return (Ac4Status.Uncertain,
                $"AC4 date ({ac4.Ac4Date}) is AFTER the refund application date ({refundApplicationDate}) — " +
                "inconsistent, requires manual review.");

        if (elapsedMonths > _businessRules.RefundDeadlineMonths)
            return (Ac4Status.NotConfirmed,
                $"Refund deadline exceeded: AC4 dated {ac4.Ac4Date}, application dated {refundApplicationDate} — " +
                $"{elapsedMonths:F1} months elapsed (limit: {_businessRules.RefundDeadlineMonths} months).");

        return (Ac4Status.Confirmed,
            $"Within refund deadline: {elapsedMonths:F1} of {_businessRules.RefundDeadlineMonths} months elapsed.");
    }
}
