using BARD.Domain.Entities;
using BARD.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace BARD.Domain.Tests;

public class DossierLineTests
{
    [Fact]
    public void RecordOfficerDecision_Approved_SetsDecisionAndReviewer()
    {
        var line = DossierLine.Create(Guid.NewGuid(), 0, "INV-1", "Jupiler", "S101", 100m, "MRN1", "FR");
        var officerId = Guid.NewGuid();

        line.RecordOfficerDecision(OfficerDecision.Approved, "Looks fine", officerId);

        line.OfficerDecision.Should().Be(OfficerDecision.Approved);
        line.OfficerRemarks.Should().Be("Looks fine");
        line.ReviewedByUserId.Should().Be(officerId);
        line.ReviewedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void RecordOfficerDecision_PendingReview_Throws()
    {
        var line = DossierLine.Create(Guid.NewGuid(), 0, "INV-1", "Jupiler", "S101", 100m, "MRN1", "FR");

        var act = () => line.RecordOfficerDecision(OfficerDecision.PendingReview, null, Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RequiresManualReview_TrueWhenHardBlockPresent_EvenAtHighConfidence()
    {
        var line = DossierLine.Create(Guid.NewGuid(), 0, "INV-1", "Jupiler", null, 100m, "MRN1", "FR");
        line.SetMatchResult(MatchStatus.AutoMatch, 100m, null, "Excise code missing", "explanation");

        line.RequiresManualReview().Should().BeTrue();
    }

    [Fact]
    public void RequiresManualReview_FalseWhenEverythingClean()
    {
        var line = DossierLine.Create(Guid.NewGuid(), 0, "INV-1", "Jupiler", "S101", 100m, "MRN1", "FR");
        line.SetMatchResult(MatchStatus.AutoMatch, 100m, null, null, "explanation");
        line.SetExportStatus(ExportConfirmationStatus.Confirmed, "ok");
        line.SetMrnCumulativeStatus(MrnCumulativeStatus.WithinLimit, "ok");
        line.SetAc4Status(Ac4Status.Confirmed, "ok");

        line.RequiresManualReview().Should().BeFalse();
    }
}

public class DossierTests
{
    [Fact]
    public void RecomputeStatusFromLines_AllApproved_SetsApproved()
    {
        var systemId = Guid.NewGuid();
        var dossier = Dossier.Create("2026-001", Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today), systemId);
        var line = DossierLine.Create(dossier.Id, 0, "INV-1", "Jupiler", "S101", 100m, "MRN1", "FR");
        dossier.AddLine(line);

        line.RecordOfficerDecision(OfficerDecision.Approved, null, systemId);
        dossier.RecomputeStatusFromLines(systemId);

        dossier.Status.Should().Be(DossierStatus.Approved);
    }

    [Fact]
    public void RecomputeStatusFromLines_MixedApprovedRejected_SetsPartiallyApproved()
    {
        var systemId = Guid.NewGuid();
        var dossier = Dossier.Create("2026-002", Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today), systemId);
        var line1 = DossierLine.Create(dossier.Id, 0, "INV-1", "Jupiler", "S101", 100m, "MRN1", "FR");
        var line2 = DossierLine.Create(dossier.Id, 1, "INV-2", "Duvel", "S109", 50m, "MRN2", "FR");
        dossier.AddLine(line1);
        dossier.AddLine(line2);

        line1.RecordOfficerDecision(OfficerDecision.Approved, null, systemId);
        line2.RecordOfficerDecision(OfficerDecision.Rejected, "bad", systemId);
        dossier.RecomputeStatusFromLines(systemId);

        dossier.Status.Should().Be(DossierStatus.PartiallyApproved);
    }

    [Fact]
    public void RecomputeStatusFromLines_AnyPending_SetsPendingManualReview()
    {
        var systemId = Guid.NewGuid();
        var dossier = Dossier.Create("2026-003", Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today), systemId);
        var line = DossierLine.Create(dossier.Id, 0, "INV-1", "Jupiler", "S101", 100m, "MRN1", "FR");
        dossier.AddLine(line);

        dossier.RecomputeStatusFromLines(systemId);

        dossier.Status.Should().Be(DossierStatus.PendingManualReview);
    }
}
