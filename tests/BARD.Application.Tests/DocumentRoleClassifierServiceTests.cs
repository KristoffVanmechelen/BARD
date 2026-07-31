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
        string? invoiceNumber = "INV-001",
        string? customer = null,
        string rawText = "")
    {
        return new ParsedInvoice(
            invoiceNumber,
            null,
            customer,
            null,
            "FR",
            Array.Empty<ParsedInvoiceLine>(),
            "invoice.pdf",
            ExtractionMethod.ClassicalTextExtraction,
            1m,
            Array.Empty<string>(),
            rawText);
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
        string? invoiceNumber = "CLAIM-001",
        string companyName = "Test Company",
        string enterpriseNumber = "BE0123456789")
    {
        return new DocumentRoleClassificationContext(
            companyName,
            enterpriseNumber,
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

        result.DocumentRole.Should()
            .Be(DocumentRole.RefundClaim);

        result.Confidence.Should()
            .Be(1.00m);

        result.Reasons.Should()
            .NotBeEmpty();
    }

    [Fact]
    public void Ac4Declaration_IsDispatchEvidence()
    {
        var result = _sut.ClassifyRole(
            Classification(DocumentKind.Ac4Declaration),
            null,
            Ac4(),
            Context());

        result.DocumentRole.Should()
            .Be(DocumentRole.DispatchEvidence);

        result.Confidence.Should()
            .BeGreaterThan(0.90m);

        result.Reasons.Should()
            .Contain(reason =>
                reason.Contains("24BE123456789"));
    }

    [Fact]
    public void EadDocument_IsDispatchEvidence()
    {
        var result = _sut.ClassifyRole(
            Classification(DocumentKind.EadEVadDocument),
            null,
            null,
            Context());

        result.DocumentRole.Should()
            .Be(DocumentRole.DispatchEvidence);

        result.Confidence.Should()
            .Be(0.95m);
    }

    [Fact]
    public void SupportingEvidence_RemainsSupportingEvidence()
    {
        var result = _sut.ClassifyRole(
            Classification(DocumentKind.SupportingEvidence),
            null,
            null,
            Context());

        result.DocumentRole.Should()
            .Be(DocumentRole.SupportingEvidence);

        result.Confidence.Should()
            .Be(0.90m);
    }

    [Fact]
    public void InvoiceWithApplicantEnterpriseNumberInCustomer_IsPurchaseInvoice()
    {
        var result = _sut.ClassifyRole(
            Classification(DocumentKind.Invoice),
            Invoice(
                "PUR-001",
                "Test Company\nVAT 0123.456.789"),
            null,
            Context("SALE-OTHER"));

        result.DocumentRole.Should()
            .Be(DocumentRole.PurchaseInvoice);

        result.Confidence.Should()
            .Be(0.98m);

        result.Reasons.Should()
            .Contain(reason =>
                reason.Contains("enterprise/VAT number"));
    }

    [Fact]
    public void InvoiceWithApplicantNameInCustomer_IsPurchaseInvoice()
    {
        var result = _sut.ClassifyRole(
            Classification(DocumentKind.Invoice),
            Invoice(
                "PUR-001",
                "Test Company\nMain Street 1"),
            null,
            Context("SALE-OTHER"));

        result.DocumentRole.Should()
            .Be(DocumentRole.PurchaseInvoice);

        result.Confidence.Should()
            .Be(0.92m);

        result.Reasons.Should()
            .Contain(reason =>
                reason.Contains("company name"));
    }

    [Fact]
    public void InvoiceReferencedByClaimAndContainingApplicant_IsSalesInvoice()
    {
        var result = _sut.ClassifyRole(
            Classification(DocumentKind.Invoice),
            Invoice(
                "INV-001",
                "Foreign Customer",
                "Seller: Test Company\nVAT BE 0123.456.789"),
            null,
            Context("inv 001"));

        result.DocumentRole.Should()
            .Be(DocumentRole.SalesInvoice);

        result.Confidence.Should()
            .Be(0.97m);

        result.Reasons.Should()
            .Contain(reason =>
                reason.Contains("Excel refund claim"));
    }

    [Fact]
    public void InvoiceReferencedByClaimWithoutPartyEvidence_IsSalesInvoice()
    {
        var result = _sut.ClassifyRole(
            Classification(DocumentKind.Invoice),
            Invoice(
                "SALE-001",
                "Foreign Customer"),
            null,
            Context("SALE-001"));

        result.DocumentRole.Should()
            .Be(DocumentRole.SalesInvoice);

        result.Confidence.Should()
            .Be(0.90m);
    }

       [Fact]
    public void InvoiceWithApplicantOnlyInRawText_RemainsUnknown()
    {
        var result = _sut.ClassifyRole(
            Classification(DocumentKind.Invoice),
            Invoice(
                "SALE-001",
                "Foreign Customer",
                "Test Company\nInvoice for Foreign Customer"),
            null,
            Context("OTHER-001"));

        result.DocumentRole.Should()
            .Be(DocumentRole.Unknown);

        result.Confidence.Should()
            .Be(0.40m);

        result.Reasons.Should()
            .Contain(reason =>
                reason.Contains("insufficient"));
    }

    [Fact]
    public void InvoiceWithConflictingPurchaseAndSalesEvidence_RemainsUnknown()
    {
        var result = _sut.ClassifyRole(
            Classification(DocumentKind.Invoice),
            Invoice(
                "INV-001",
                "Test Company\nBE0123456789"),
            null,
            Context("INV-001"));

        result.DocumentRole.Should()
            .Be(DocumentRole.Unknown);

        result.Confidence.Should()
            .Be(0.25m);

        result.Reasons.Should()
            .Contain(reason =>
                reason.Contains("conflicting"));
    }

    [Fact]
    public void InvoiceWithoutReliablePartyOrClaimEvidence_RemainsUnknown()
    {
        var result = _sut.ClassifyRole(
            Classification(DocumentKind.Invoice),
            Invoice(
                null,
                "Unidentified Customer"),
            null,
            Context("CLAIM-001"));

        result.DocumentRole.Should()
            .Be(DocumentRole.Unknown);

        result.Confidence.Should()
            .Be(0.40m);

        result.Reasons.Should()
            .Contain(reason =>
                reason.Contains("No invoice number"));
    }

    [Fact]
    public void InvoiceWithoutParsedData_RemainsUnknown()
    {
        var result = _sut.ClassifyRole(
            Classification(DocumentKind.Invoice),
            null,
            null,
            Context());

        result.DocumentRole.Should()
            .Be(DocumentRole.Unknown);

        result.Confidence.Should()
            .Be(0.20m);

        result.Reasons.Should()
            .Contain(reason =>
                reason.Contains("No parsed invoice data"));
    }
}