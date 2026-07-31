using BARD.Application.DocumentProcessing.Interfaces;
using BARD.Application.DocumentProcessing.Models;
using BARD.Domain.Enums;

namespace BARD.Infrastructure.DocumentProcessing;

/// <summary>
/// Determines the contextual role fulfilled by a document within a dossier.
///
/// Invoice roles are determined from the applicant's perspective:
/// an invoice addressed to the applicant is a purchase invoice, while an
/// invoice referenced by the refund claim or issued by the applicant to
/// another customer is a sales invoice.
/// </summary>
public sealed class DocumentRoleClassifierService : IDocumentRoleClassifierService
{
    public DocumentRoleClassificationResult ClassifyRole(
        DocumentClassificationResult classification,
        ParsedInvoice? invoice,
        ParsedAc4Declaration? ac4Declaration,
        DocumentRoleClassificationContext context)
    {
        ArgumentNullException.ThrowIfNull(classification);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.ExcelRows);

        return classification.DocumentKind switch
        {
            DocumentKind.CompanyExcelClaim =>
                CreateResult(
                    classification.FileName,
                    DocumentRole.RefundClaim,
                    1.00m,
                    "The document is the company Excel claim forming the basis of the refund dossier."),

            DocumentKind.Ac4Declaration =>
                ClassifyAc4(classification, ac4Declaration),

            DocumentKind.EadEVadDocument =>
                CreateResult(
                    classification.FileName,
                    DocumentRole.DispatchEvidence,
                    0.95m,
                    "An e-AD or e-VAD documents the dispatch or movement of excise goods."),

            DocumentKind.SupportingEvidence =>
                CreateResult(
                    classification.FileName,
                    DocumentRole.SupportingEvidence,
                    0.90m,
                    "The document was classified as general supporting evidence."),

            DocumentKind.Invoice =>
                ClassifyInvoice(classification, invoice, context),

            _ =>
                CreateResult(
                    classification.FileName,
                    DocumentRole.Unknown,
                    0.00m,
                    "The document kind is unknown, so no reliable dossier role can be assigned."),
        };
    }

    private static DocumentRoleClassificationResult ClassifyAc4(
        DocumentClassificationResult classification,
        ParsedAc4Declaration? ac4Declaration)
    {
        var reasons = new List<string>
        {
            "An AC4 declaration serves as evidence relating to the release for consumption or excise treatment of the goods.",
        };

        var confidence = 0.90m;

        if (ac4Declaration is not null)
        {
            if (!string.IsNullOrWhiteSpace(ac4Declaration.Mrn))
            {
                reasons.Add(
                    $"The parsed declaration contains MRN {ac4Declaration.Mrn}.");

                confidence += 0.03m;
            }

            if (ac4Declaration.Ac4Date.HasValue)
            {
                reasons.Add(
                    $"The parsed declaration contains AC4 date {ac4Declaration.Ac4Date:yyyy-MM-dd}.");

                confidence += 0.02m;
            }
        }
        else
        {
            reasons.Add(
                "No parsed AC4 data was available when the role was determined.");
        }

        return new DocumentRoleClassificationResult(
            classification.FileName,
            DocumentRole.DispatchEvidence,
            Math.Min(confidence, 1.00m),
            reasons);
    }

    private static DocumentRoleClassificationResult ClassifyInvoice(
        DocumentClassificationResult classification,
        ParsedInvoice? invoice,
        DocumentRoleClassificationContext context)
    {
        var reasons = new List<string>
        {
            "The document is an invoice. Its role is determined from the applicant's perspective and its relationship to the refund claim.",
        };

        if (invoice is null)
        {
            reasons.Add(
                "No parsed invoice data was available, so no invoice role can be assigned.");

            return new DocumentRoleClassificationResult(
                classification.FileName,
                DocumentRole.Unknown,
                0.20m,
                reasons);
        }

        var matchingClaimRows =
            CountMatchingClaimRows(
                invoice.InvoiceNumber,
                context.ExcelRows);

        if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
        {
            reasons.Add(
                "No invoice number could be extracted.");
        }
        else
        {
            reasons.Add(
                $"The parsed invoice number is {invoice.InvoiceNumber}.");

            if (matchingClaimRows > 0)
            {
                reasons.Add(
                    $"The invoice number occurs in {matchingClaimRows} row(s) of the Excel refund claim.");
            }
            else
            {
                reasons.Add(
                    "The invoice number was not found in the currently available Excel refund claim rows.");
            }
        }

        var customerContainsEnterpriseNumber =
            ContainsEnterpriseNumber(
                invoice.Customer,
                context.EnterpriseNumber);

        var customerContainsCompanyName =
            ContainsNormalizedValue(
                invoice.Customer,
                context.CompanyName,
                minimumLength: 4);

        var applicantIsCustomer =
            customerContainsEnterpriseNumber
            || customerContainsCompanyName;

        if (string.IsNullOrWhiteSpace(invoice.Customer))
        {
            reasons.Add(
                "No customer could be extracted from the invoice.");
        }
        else if (customerContainsEnterpriseNumber)
        {
            reasons.Add(
                "The invoice customer contains the applicant's enterprise/VAT number.");
        }
        else if (customerContainsCompanyName)
        {
            reasons.Add(
                "The invoice customer contains the applicant's company name.");
        }
        else
        {
            reasons.Add(
                "The extracted invoice customer does not identify the applicant.");
        }

        var rawTextContainsEnterpriseNumber =
            ContainsEnterpriseNumber(
                invoice.RawText,
                context.EnterpriseNumber);

        var rawTextContainsCompanyName =
            ContainsNormalizedValue(
                invoice.RawText,
                context.CompanyName,
                minimumLength: 4);

        var applicantAppearsInRawText =
            rawTextContainsEnterpriseNumber
            || rawTextContainsCompanyName;

        if (rawTextContainsEnterpriseNumber)
        {
            reasons.Add(
                "The invoice text contains the applicant's enterprise/VAT number.");
        }
        else if (rawTextContainsCompanyName)
        {
            reasons.Add(
                "The invoice text contains the applicant's company name.");
        }
        else
        {
            reasons.Add(
                "The applicant could not be identified elsewhere in the invoice text.");
        }

        if (applicantIsCustomer && matchingClaimRows > 0)
        {
            reasons.Add(
                "The applicant is identified as the customer, which indicates a purchase invoice, but the invoice number is also referenced by the refund claim, which indicates a sales invoice. The evidence is conflicting.");

            return new DocumentRoleClassificationResult(
                classification.FileName,
                DocumentRole.Unknown,
                0.25m,
                reasons);
        }

        if (applicantIsCustomer)
        {
            reasons.Add(
                "Because the applicant is the invoice customer, the document is classified as a purchase invoice.");

            return new DocumentRoleClassificationResult(
                classification.FileName,
                DocumentRole.PurchaseInvoice,
                customerContainsEnterpriseNumber
                    ? 0.98m
                    : 0.92m,
                reasons);
        }

        if (matchingClaimRows > 0)
        {
            reasons.Add(
                "Because the invoice is explicitly referenced by the refund claim and is not addressed to the applicant, it is classified as a sales invoice.");

            return new DocumentRoleClassificationResult(
                classification.FileName,
                DocumentRole.SalesInvoice,
                applicantAppearsInRawText
                    ? 0.97m
                    : 0.90m,
                reasons);
        }

        if (!string.IsNullOrWhiteSpace(invoice.Customer)
            && applicantAppearsInRawText)
        {
            reasons.Add(
                "The applicant appears in the invoice text while another party is identified as the customer, so the document is classified as a sales invoice.");

            return new DocumentRoleClassificationResult(
                classification.FileName,
                DocumentRole.SalesInvoice,
                0.85m,
                reasons);
        }

        reasons.Add(
            "The available party and claim evidence is insufficient to distinguish a purchase invoice from a sales invoice.");

        return new DocumentRoleClassificationResult(
            classification.FileName,
            DocumentRole.Unknown,
            0.40m,
            reasons);
    }

    private static int CountMatchingClaimRows(
        string? invoiceNumber,
        IReadOnlyList<ParsedExcelClaimRow> excelRows)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            return 0;
        }

        var normalizedInvoiceNumber =
            Normalize(invoiceNumber);

        return excelRows.Count(row =>
            !string.IsNullOrWhiteSpace(row.InvoiceNumber)
            && string.Equals(
                Normalize(row.InvoiceNumber),
                normalizedInvoiceNumber,
                StringComparison.Ordinal));
    }

    private static bool ContainsEnterpriseNumber(
        string? text,
        string? enterpriseNumber)
    {
        if (string.IsNullOrWhiteSpace(text)
            || string.IsNullOrWhiteSpace(enterpriseNumber))
        {
            return false;
        }

        var normalizedText =
            Normalize(text);

        var normalizedEnterpriseNumber =
            Normalize(enterpriseNumber);

        if (normalizedEnterpriseNumber.Length < 6)
        {
            return false;
        }

        if (normalizedText.Contains(
                normalizedEnterpriseNumber,
                StringComparison.Ordinal))
        {
            return true;
        }

        var hasCountryPrefix =
            normalizedEnterpriseNumber.Length > 8
            && char.IsLetter(normalizedEnterpriseNumber[0])
            && char.IsLetter(normalizedEnterpriseNumber[1]);

        if (!hasCountryPrefix)
        {
            return false;
        }

        var withoutCountryPrefix =
            normalizedEnterpriseNumber[2..];

        return normalizedText.Contains(
            withoutCountryPrefix,
            StringComparison.Ordinal);
    }

    private static bool ContainsNormalizedValue(
        string? text,
        string? value,
        int minimumLength)
    {
        if (string.IsNullOrWhiteSpace(text)
            || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalizedValue =
            Normalize(value);

        if (normalizedValue.Length < minimumLength)
        {
            return false;
        }

        return Normalize(text).Contains(
            normalizedValue,
            StringComparison.Ordinal);
    }

    private static DocumentRoleClassificationResult CreateResult(
        string fileName,
        DocumentRole documentRole,
        decimal confidence,
        params string[] reasons)
    {
        return new DocumentRoleClassificationResult(
            fileName,
            documentRole,
            confidence,
            reasons);
    }

    private static string Normalize(string value)
    {
        return new string(
            value
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());
    }
}