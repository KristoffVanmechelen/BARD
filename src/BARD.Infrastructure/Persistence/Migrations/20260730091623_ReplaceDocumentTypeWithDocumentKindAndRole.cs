using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BARD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceDocumentTypeWithDocumentKindAndRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentKind",
                schema: "dossier",
                table: "DossierDocuments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "DocumentRole",
                schema: "dossier",
                table: "DossierDocuments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<decimal>(
                name: "RoleConfidence",
                schema: "dossier",
                table: "DossierDocuments",
                type: "decimal(5,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "RoleConfirmedByUser",
                schema: "dossier",
                table: "DossierDocuments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RoleReasons",
                schema: "dossier",
                table: "DossierDocuments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [dossier].[DossierDocuments]
                SET [DocumentKind] =
                    CASE [DocumentType]
                        WHEN 'SalesInvoice' THEN 'Invoice'
                        WHEN 'Ac4Declaration' THEN 'Ac4Declaration'
                        WHEN 'EadEVadDocument' THEN 'EadEVadDocument'
                        WHEN 'CompanyExcelClaim' THEN 'CompanyExcelClaim'
                        WHEN 'SupportingEvidence' THEN 'SupportingEvidence'
                        ELSE 'Unknown'
                    END,
                    [DocumentRole] =
                    CASE [DocumentType]
                        WHEN 'SalesInvoice' THEN 'SalesInvoice'
                        WHEN 'SupportingEvidence' THEN 'SupportingEvidence'
                        ELSE 'Unknown'
                    END;
                """);

            migrationBuilder.DropColumn(
                name: "DocumentType",
                schema: "dossier",
                table: "DossierDocuments");

            migrationBuilder.CreateIndex(
                name: "IX_DossierDocuments_DocumentKind",
                schema: "dossier",
                table: "DossierDocuments",
                column: "DocumentKind");

            migrationBuilder.CreateIndex(
                name: "IX_DossierDocuments_DocumentRole",
                schema: "dossier",
                table: "DossierDocuments",
                column: "DocumentRole");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DossierDocuments_DocumentKind",
                schema: "dossier",
                table: "DossierDocuments");

            migrationBuilder.DropIndex(
                name: "IX_DossierDocuments_DocumentRole",
                schema: "dossier",
                table: "DossierDocuments");

            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                schema: "dossier",
                table: "DossierDocuments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.Sql(
                """
                UPDATE [dossier].[DossierDocuments]
                SET [DocumentType] =
                    CASE
                        WHEN [DocumentKind] = 'Invoice'
                             AND [DocumentRole] = 'SalesInvoice'
                            THEN 'SalesInvoice'
                        WHEN [DocumentKind] = 'Ac4Declaration'
                            THEN 'Ac4Declaration'
                        WHEN [DocumentKind] = 'EadEVadDocument'
                            THEN 'EadEVadDocument'
                        WHEN [DocumentKind] = 'CompanyExcelClaim'
                            THEN 'CompanyExcelClaim'
                        WHEN [DocumentKind] = 'SupportingEvidence'
                            THEN 'SupportingEvidence'
                        ELSE 'Unknown'
                    END;
                """);

            migrationBuilder.DropColumn(
                name: "DocumentKind",
                schema: "dossier",
                table: "DossierDocuments");

            migrationBuilder.DropColumn(
                name: "DocumentRole",
                schema: "dossier",
                table: "DossierDocuments");

            migrationBuilder.DropColumn(
                name: "RoleConfidence",
                schema: "dossier",
                table: "DossierDocuments");

            migrationBuilder.DropColumn(
                name: "RoleConfirmedByUser",
                schema: "dossier",
                table: "DossierDocuments");

            migrationBuilder.DropColumn(
                name: "RoleReasons",
                schema: "dossier",
                table: "DossierDocuments");
        }
    }
}