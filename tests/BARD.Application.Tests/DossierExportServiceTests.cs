using BARD.Domain.Entities;
using BARD.Domain.Enums;
using BARD.Infrastructure.Reporting;
using FluentAssertions;
using Xunit;

namespace BARD.Application.Tests;

public class DossierExportServiceRowColourTests
{
    private static DossierLine Line(MatchStatus match, ExportConfirmationStatus export, OfficerDecision decision,
        MrnCumulativeStatus mrn = MrnCumulativeStatus.WithinLimit, Ac4Status ac4 = Ac4Status.Confirmed)
    {
        var line = DossierLine.Create(Guid.NewGuid(), 0, "INV-1", "Jupiler", "S101", 100m, "MRN1", "FR");
        line.SetMatchResult(match, match == MatchStatus.AutoMatch ? 100m : 50m, null, null, "");
        line.SetExportStatus(export, "");
        line.SetMrnCumulativeStatus(mrn, "");
        line.SetAc4Status(ac4, "");
        if (decision != OfficerDecision.PendingReview)
            line.RecordOfficerDecision(decision, null, Guid.NewGuid());
        return line;
    }

    [Fact]
    public void OfficerApproved_IsAlwaysGreen_RegardlessOfMatchStatus()
    {
        var line = Line(MatchStatus.ManualReviewRequired, ExportConfirmationStatus.Uncertain, OfficerDecision.Approved);
        DossierExportService.RowColour(line).Should().Be("C6EFCE");
    }

    [Fact]
    public void OfficerRejected_IsAlwaysRed()
    {
        var line = Line(MatchStatus.AutoMatch, ExportConfirmationStatus.Confirmed, OfficerDecision.Rejected);
        DossierExportService.RowColour(line).Should().Be("FFC7CE");
    }

    [Fact]
    public void NoMatch_PendingDecision_IsRed()
    {
        var line = Line(MatchStatus.NoMatch, ExportConfirmationStatus.Uncertain, OfficerDecision.PendingReview);
        DossierExportService.RowColour(line).Should().Be("FFC7CE");
    }

    [Fact]
    public void CleanAutoMatchConfirmedExport_PendingDecision_IsGreen()
    {
        var line = Line(MatchStatus.AutoMatch, ExportConfirmationStatus.Confirmed, OfficerDecision.PendingReview);
        DossierExportService.RowColour(line).Should().Be("C6EFCE");
    }

    [Fact]
    public void LikelyMatch_PendingDecision_IsOrange()
    {
        var line = Line(MatchStatus.LikelyMatch, ExportConfirmationStatus.Confirmed, OfficerDecision.PendingReview);
        DossierExportService.RowColour(line).Should().Be("FFEB9C");
    }
}
