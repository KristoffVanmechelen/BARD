using BARD.Domain.Entities;
using BARD.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace BARD.Domain.Tests;

public class DossierDocumentTests
{
    [Fact]
    public void ConfirmDocumentRole_RecordsUserAndTimestamp()
    {
        var document = CreateDocument();
        var officerId = Guid.NewGuid();
        var beforeConfirmation = DateTime.UtcNow;

        document.SetDocumentRole(
            DocumentRole.SalesInvoice,
            0.90m,
            "Automatically classified.");

        document.ConfirmDocumentRole(
            DocumentRole.PurchaseInvoice,
            "Corrected after manual review.",
            officerId);

        document.DocumentRole.Should()
            .Be(DocumentRole.PurchaseInvoice);

        document.RoleConfidence.Should()
            .Be(1m);

        document.RoleReasons.Should()
            .Be("Corrected after manual review.");

        document.RoleConfirmedByUser.Should()
            .BeTrue();

        document.RoleConfirmedByUserId.Should()
            .Be(officerId);

        document.RoleConfirmedAtUtc.Should()
            .NotBeNull()
            .And.BeOnOrAfter(beforeConfirmation)
            .And.BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public void SetDocumentRole_AfterUserConfirmation_DoesNotOverwriteDecision()
    {
        var document = CreateDocument();
        var officerId = Guid.NewGuid();

        document.ConfirmDocumentRole(
            DocumentRole.PurchaseInvoice,
            "Confirmed by officer.",
            officerId);

        document.SetDocumentRole(
            DocumentRole.SalesInvoice,
            0.99m,
            "Later automatic classification.");

        document.DocumentRole.Should()
            .Be(DocumentRole.PurchaseInvoice);

        document.RoleConfidence.Should()
            .Be(1m);

        document.RoleReasons.Should()
            .Be("Confirmed by officer.");

        document.RoleConfirmedByUser.Should()
            .BeTrue();

        document.RoleConfirmedByUserId.Should()
            .Be(officerId);

        document.RoleConfirmedAtUtc.Should()
            .NotBeNull();
    }

    [Fact]
    public void ConfirmDocumentRole_WithoutUserId_Throws()
    {
        var document = CreateDocument();

        var act = () => document.ConfirmDocumentRole(
            DocumentRole.PurchaseInvoice,
            "Confirmed by officer.",
            Guid.Empty);

        act.Should()
            .Throw<ArgumentException>();
    }

    private static DossierDocument CreateDocument()
    {
        return DossierDocument.Create(
            Guid.NewGuid(),
            "invoice.pdf",
            "dossier-invoices/invoice.pdf",
            new string('a', 64),
            1024,
            Guid.NewGuid());
    }
}