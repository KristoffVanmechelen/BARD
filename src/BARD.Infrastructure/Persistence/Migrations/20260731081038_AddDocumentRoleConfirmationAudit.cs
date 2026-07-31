using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BARD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentRoleConfirmationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RoleConfirmedAtUtc",
                schema: "dossier",
                table: "DossierDocuments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RoleConfirmedByUserId",
                schema: "dossier",
                table: "DossierDocuments",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RoleConfirmedAtUtc",
                schema: "dossier",
                table: "DossierDocuments");

            migrationBuilder.DropColumn(
                name: "RoleConfirmedByUserId",
                schema: "dossier",
                table: "DossierDocuments");
        }
    }
}
