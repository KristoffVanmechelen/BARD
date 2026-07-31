using BARD.Application.DocumentProcessing.Models;
using BARD.Domain.Enums;
using BARD.Infrastructure.DocumentProcessing;
using FluentAssertions;
using Xunit;

namespace BARD.Application.Tests;

public class DocumentRoleClassifierServiceTests
{
    private readonly DocumentRoleClassifierService _sut = new();

    private static DocumentClassificationResult Classification(
        DocumentKind kind,
        string fileName = "document.pdf")
    {
        return new DocumentClassificationResult(
            fileName,
            kind,
            0.99m,
            new[] { "classifier" });
    }

    private static ParsedInvoice Invoice(
        string? invoiceNumber = "INV-001")
    {
        return new ParsedInvoice(
            invoiceNumber,
            null,
            null,
            null,
            "FR",
            Array.Empty<ParsedInvoiceLine>(),
            "invoice.pdf",
            ExtractionMethod.ClassicalTextExtraction,
            1m,
            Array.Empty<string>(),
            "");
    }

    private static ParsedAc4Declaration Ac4()
    {
        return new ParsedAc4Declaration(
            "24BE123456789",
            new DateOnly(2026, 1, 15),
            "Consignee",
            "Beer",
            100m,
            "S101",
            "ac4.pdf",
            ExtractionMethod.ClassicalTextExtraction,
            1m,
            Array.Empty<string>(),
            "");
    }

    private static DocumentRoleClassificationContext Context(
        string invoiceNumber = "INV-001")
    {
        return new DocumentRoleClassificationContext(
            "Test Company",
            "BE0123456789",
            new[]
            {
                new ParsedExcelClaimRow(
                    1,
                    invoiceNumber,
                    "Beer",
                    "S101",
                    100m,
                    "MRN1",
                    "FR",
                    new Dictionary<string, string?>())
            });
    }

    [Fact]
    public void CompanyExcelClaim_IsRefundClaim()
    {
        var result = _sut.ClassifyRole(
            Classification(DocumentKind.CompanyExcelClaim),
            null,
            null,
            Context());

        result.DocumentRole.Should().Be(DocumentRole.RefundClaim);
        result.Confidence.Should().Be(1.00m);
        result.Reasons.Should().NotBeEmpty();
    }

    [Fact]
    public void Ac4Declaration_IsDispatchEvidence()
    {
        var result = _sut.ClassifyRole(
            Classification(DocumentKind.Ac4Declaration),
            null,
            Ac4(),
            Context());

        result.DocumentRole.Should().Be(DocumentRole.DispatchEvidence);
        result.Confidence.Should().BeGreaterThan(0.90m);
        result.Reasons.Should().Contain(r => r.Contains("24BE123456789"));
    }

    [Fact]
    public void EadDocument_IsDispatchEvidence()
    {
        var result = _sut.ClassifyRole(
            Classification(DocumentKind.EadEVadDocument),
            null,
            null,
            Context());

        result.DocumentRole.Should().Be(DocumentRole.DispatchEvidence);
        result.Confidence.Should().Be(0.95m);
    }

    [Fact]
    public void SupportingEvidence_RemainsSupportingEvidence()
    {
        var result = _sut.ClassifyRole(
            Classification(DocumentKind.SupportingEvidence),
            null,
            null,
            Context());

        result.DocumentRole.Should().Be(DocumentRole.SupportingEvidence);
        result.Confidence.Should().Be(0.90m);
    }

    [Fact]
    public void Invoice_CurrentlyRemainsUnknown()
    {
        var result = _sut.ClassifyRole(
            Classification(DocumentKind.Invoice),
            Invoice(),
            null,
            Context());

        result.DocumentRole.Should().Be(DocumentRole.Unknown);
        result.Confidence.Should().Be(0.40m);
    }

    [Fact]
    public void InvoiceWithoutInvoiceNumber_RemainsUnknown()
    {
        var result = _sut.ClassifyRole(
            Classification(DocumentKind.Invoice),
            Invoice(null),
            null,
            Context());

        result.DocumentRole.Should().Be(DocumentRole.Unknown);
        result.Reasons.Should().Contain(r =>
            r.Contains("No invoice number"));
    }

    [Fact]
    public void InvoiceFoundInExcel_StillRemainsUnknown()
    {
        var result = _sut.ClassifyRole(
            Classification(DocumentKind.Invoice),
            Invoice("INV-001"),
            null,
            Context("INV-001"));

        result.DocumentRole.Should().Be(DocumentRole.Unknown);

        result.Reasons.Should().Contain(r =>
            r.Contains("occurs"));
    }
}