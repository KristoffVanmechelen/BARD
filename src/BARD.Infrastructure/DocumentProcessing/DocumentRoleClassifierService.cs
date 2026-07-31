using BARD.Application.DocumentProcessing.Interfaces;
using BARD.Application.DocumentProcessing.Models;
using BARD.Domain.Enums;

namespace BARD.Infrastructure.DocumentProcessing;

/// <summary>
/// Determines the contextual role fulfilled by a document within a dossier.
///
/// This first implementation deliberately classifies only roles that follow
/// unambiguously from the intrinsic document kind. Invoice roles require more
/// dossier context and therefore remain Unknown for now.
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
                reasons.Add($"The parsed declaration contains MRN {ac4Declaration.Mrn}.");
                confidence += 0.03m;
            }

            if (ac4Declaration.Ac4Date.HasValue)
            {
                reasons.Add($"The parsed declaration contains AC4 date {ac4Declaration.Ac4Date:yyyy-MM-dd}.");
                confidence += 0.02m;
            }
        }
        else
        {
            reasons.Add("No parsed AC4 data was available when the role was determined.");
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
            "The document is an invoice, but its role depends on the parties and its relationship to the refund claim.",
        };

        if (invoice is null)
        {
            reasons.Add("No parsed invoice data was available.");
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
            {
                reasons.Add($"The parsed invoice number is {invoice.InvoiceNumber}.");

                var matchingClaimRows = context.ExcelRows.Count(row =>
                    !string.IsNullOrWhiteSpace(row.InvoiceNumber) &&
                    string.Equals(
                        Normalize(row.InvoiceNumber),
                        Normalize(invoice.InvoiceNumber),
                        StringComparison.OrdinalIgnoreCase));

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
            else
            {
                reasons.Add("No invoice number could be extracted.");
            }
        }

        reasons.Add(
            "PurchaseInvoice and SalesInvoice are not assigned until reliable party-based dossier rules are implemented.");

        return new DocumentRoleClassificationResult(
            classification.FileName,
            DocumentRole.Unknown,
            0.40m,
            reasons);
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