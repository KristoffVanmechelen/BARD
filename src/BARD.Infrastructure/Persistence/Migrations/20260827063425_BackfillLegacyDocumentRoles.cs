using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BARD.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Corrects document roles created by the legacy DocumentType migration.
    ///
    /// The former DocumentType.SalesInvoice value did not reliably mean that
    /// the applicant was the seller. The legacy classifier used that value for
    /// every recognised invoice. Such records must therefore return to
    /// DocumentRole.Unknown until their contextual role can be determined
    /// reliably or confirmed by a user.
    ///
    /// Other roles are backfilled only where the intrinsic DocumentKind
    /// determines the dossier role unambiguously.
    ///
    /// Records with evidence of later automatic classification or manual
    /// confirmation are deliberately left untouched.
    /// </summary>
    public partial class BackfillLegacyDocumentRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE [dossier].[DossierDocuments]
                SET
                    [DocumentRole] = 'Unknown',
                    [RoleReasons] = N'Legacy role corrected: the former SalesInvoice document type identified an invoice but did not reliably establish whether it was a purchase or sales invoice. Manual or contextual review is required.'
                WHERE
                    [DocumentKind] = 'Invoice'
                    AND [DocumentRole] = 'SalesInvoice'
                    AND [RoleConfidence] = 0
                    AND [RoleReasons] IS NULL
                    AND [RoleConfirmedByUser] = 0
                    AND [RoleConfirmedByUserId] IS NULL
                    AND [RoleConfirmedAtUtc] IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE [dossier].[DossierDocuments]
                SET
                    [DocumentRole] = 'RefundClaim',
                    [RoleConfidence] = 1.0000,
                    [RoleReasons] = N'Legacy role backfilled from DocumentKind.CompanyExcelClaim.'
                WHERE
                    [DocumentKind] = 'CompanyExcelClaim'
                    AND [DocumentRole] = 'Unknown'
                    AND [RoleConfidence] = 0
                    AND [RoleReasons] IS NULL
                    AND [RoleConfirmedByUser] = 0
                    AND [RoleConfirmedByUserId] IS NULL
                    AND [RoleConfirmedAtUtc] IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE [dossier].[DossierDocuments]
                SET
                    [DocumentRole] = 'DispatchEvidence',
                    [RoleConfidence] = 0.9000,
                    [RoleReasons] = N'Legacy role backfilled from DocumentKind.Ac4Declaration.'
                WHERE
                    [DocumentKind] = 'Ac4Declaration'
                    AND [DocumentRole] = 'Unknown'
                    AND [RoleConfidence] = 0
                    AND [RoleReasons] IS NULL
                    AND [RoleConfirmedByUser] = 0
                    AND [RoleConfirmedByUserId] IS NULL
                    AND [RoleConfirmedAtUtc] IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE [dossier].[DossierDocuments]
                SET
                    [DocumentRole] = 'DispatchEvidence',
                    [RoleConfidence] = 0.9500,
                    [RoleReasons] = N'Legacy role backfilled from DocumentKind.EadEVadDocument.'
                WHERE
                    [DocumentKind] = 'EadEVadDocument'
                    AND [DocumentRole] = 'Unknown'
                    AND [RoleConfidence] = 0
                    AND [RoleReasons] IS NULL
                    AND [RoleConfirmedByUser] = 0
                    AND [RoleConfirmedByUserId] IS NULL
                    AND [RoleConfirmedAtUtc] IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE [dossier].[DossierDocuments]
                SET
                    [DocumentRole] = 'SalesInvoice',
                    [RoleReasons] = NULL
                WHERE
                    [DocumentKind] = 'Invoice'
                    AND [DocumentRole] = 'Unknown'
                    AND [RoleConfidence] = 0
                    AND [RoleReasons] = N'Legacy role corrected: the former SalesInvoice document type identified an invoice but did not reliably establish whether it was a purchase or sales invoice. Manual or contextual review is required.'
                    AND [RoleConfirmedByUser] = 0
                    AND [RoleConfirmedByUserId] IS NULL
                    AND [RoleConfirmedAtUtc] IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE [dossier].[DossierDocuments]
                SET
                    [DocumentRole] = 'Unknown',
                    [RoleConfidence] = 0,
                    [RoleReasons] = NULL
                WHERE
                    [DocumentKind] = 'CompanyExcelClaim'
                    AND [DocumentRole] = 'RefundClaim'
                    AND [RoleConfidence] = 1.0000
                    AND [RoleReasons] = N'Legacy role backfilled from DocumentKind.CompanyExcelClaim.'
                    AND [RoleConfirmedByUser] = 0
                    AND [RoleConfirmedByUserId] IS NULL
                    AND [RoleConfirmedAtUtc] IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE [dossier].[DossierDocuments]
                SET
                    [DocumentRole] = 'Unknown',
                    [RoleConfidence] = 0,
                    [RoleReasons] = NULL
                WHERE
                    [DocumentKind] = 'Ac4Declaration'
                    AND [DocumentRole] = 'DispatchEvidence'
                    AND [RoleConfidence] = 0.9000
                    AND [RoleReasons] = N'Legacy role backfilled from DocumentKind.Ac4Declaration.'
                    AND [RoleConfirmedByUser] = 0
                    AND [RoleConfirmedByUserId] IS NULL
                    AND [RoleConfirmedAtUtc] IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE [dossier].[DossierDocuments]
                SET
                    [DocumentRole] = 'Unknown',
                    [RoleConfidence] = 0,
                    [RoleReasons] = NULL
                WHERE
                    [DocumentKind] = 'EadEVadDocument'
                    AND [DocumentRole] = 'DispatchEvidence'
                    AND [RoleConfidence] = 0.9500
                    AND [RoleReasons] = N'Legacy role backfilled from DocumentKind.EadEVadDocument.'
                    AND [RoleConfirmedByUser] = 0
                    AND [RoleConfirmedByUserId] IS NULL
                    AND [RoleConfirmedAtUtc] IS NULL;
                """);
        }
    }
}
