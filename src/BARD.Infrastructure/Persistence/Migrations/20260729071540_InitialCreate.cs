using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BARD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dossier");

            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.EnsureSchema(
                name: "reference");

            migrationBuilder.EnsureSchema(
                name: "i18n");

            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "AuditLogEntries",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                schema: "dossier",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EnterpriseNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NormalizedEnterpriseNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AddressLine = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    City = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfigurationOperationAuditEntries",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfigurationFormatVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ImportedOrExportedSections = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    ValidationErrors = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationOperationAuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfigurationSnapshots",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationFormatVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ApplicationVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Dossiers",
                schema: "dossier",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DossierReference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RefundApplicationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dossiers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExciseRateAuditEntries",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExciseRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExciseCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PreviousRate = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    NewRate = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    PreviousUnit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NewUnit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PreviousActiveStatus = table.Column<bool>(type: "bit", nullable: true),
                    NewActiveStatus = table.Column<bool>(type: "bit", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExciseRateAuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExciseRates",
                schema: "reference",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExciseCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AdministrativeComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExciseRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocalizationEntries",
                schema: "i18n",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Screen = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DefaultNl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DefaultFr = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DefaultDe = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DefaultEn = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsProtected = table.Column<bool>(type: "bit", nullable: false),
                    IsAdministratorConfigurable = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalizationEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TerminologyAuditEntries",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocalizationKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PreviousValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerminologyAuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalIdentityId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    PreferredLanguage = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DossierDocuments",
                schema: "dossier",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DossierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BlobStoragePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ClassificationConfidence = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    ClassificationReasons = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExtractionMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ExtractionConfidence = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    ExtractionWarnings = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OcrWasRequired = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DossierDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DossierDocuments_Dossiers_DossierId",
                        column: x => x.DossierId,
                        principalSchema: "dossier",
                        principalTable: "Dossiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DossierLines",
                schema: "dossier",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DossierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowIndex = table.Column<int>(type: "int", nullable: false),
                    ClaimedInvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClaimedProductDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExciseCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ClaimedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Mrn = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ClaimedDestinationCountry = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    MatchStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ConfidenceScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    MatchedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HardBlockReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MatchExplanation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExportStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ExportCheckNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MrnCumulativeStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MrnCumulativeNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ac4Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Ac4Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OfficerDecision = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OfficerRemarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppliedExciseCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AppliedRate = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    AppliedCalculationUnit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CalculatedRefundAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CalculationTimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppliedExciseRateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CalculationNotes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DossierLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DossierLines_Dossiers_DossierId",
                        column: x => x.DossierId,
                        principalSchema: "dossier",
                        principalTable: "Dossiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DossierStatusHistory",
                schema: "dossier",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DossierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DossierStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DossierStatusHistory_Dossiers_DossierId",
                        column: x => x.DossierId,
                        principalSchema: "dossier",
                        principalTable: "Dossiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExciseRateVersions",
                schema: "reference",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExciseRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    CalculationUnit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExciseRateVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExciseRateVersions_ExciseRates_ExciseRateId",
                        column: x => x.ExciseRateId,
                        principalSchema: "reference",
                        principalTable: "ExciseRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TerminologyOverrides",
                schema: "i18n",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocalizationEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    LastModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerminologyOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TerminologyOverrides_LocalizationEntries_LocalizationEntryId",
                        column: x => x.LocalizationEntryId,
                        principalSchema: "i18n",
                        principalTable: "LocalizationEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "identity",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoleAssignments",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoleAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRoleAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ac4Declarations",
                schema: "dossier",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DossierDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mrn = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Ac4Date = table.Column<DateOnly>(type: "date", nullable: true),
                    Consignee = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProductDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    ExciseCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ExtractionMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ExtractionConfidence = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    ExtractionWarnings = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ac4Declarations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ac4Declarations_DossierDocuments_DossierDocumentId",
                        column: x => x.DossierDocumentId,
                        principalSchema: "dossier",
                        principalTable: "DossierDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExtractedFields",
                schema: "dossier",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DossierDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PageNumber = table.Column<int>(type: "int", nullable: true),
                    RawSnippet = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Confidence = table.Column<decimal>(type: "decimal(5,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtractedFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExtractedFields_DossierDocuments_DossierDocumentId",
                        column: x => x.DossierDocumentId,
                        principalSchema: "dossier",
                        principalTable: "DossierDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ac4Declarations_DossierDocumentId",
                schema: "dossier",
                table: "Ac4Declarations",
                column: "DossierDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Ac4Declarations_Mrn",
                schema: "dossier",
                table: "Ac4Declarations",
                column: "Mrn");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_EntityType_EntityId",
                schema: "audit",
                table: "AuditLogEntries",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_TimestampUtc",
                schema: "audit",
                table: "AuditLogEntries",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_NormalizedEnterpriseNumber",
                schema: "dossier",
                table: "Companies",
                column: "NormalizedEnterpriseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DossierDocuments_ContentHash",
                schema: "dossier",
                table: "DossierDocuments",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_DossierDocuments_DossierId",
                schema: "dossier",
                table: "DossierDocuments",
                column: "DossierId");

            migrationBuilder.CreateIndex(
                name: "IX_DossierLines_ClaimedInvoiceNumber",
                schema: "dossier",
                table: "DossierLines",
                column: "ClaimedInvoiceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_DossierLines_DossierId",
                schema: "dossier",
                table: "DossierLines",
                column: "DossierId");

            migrationBuilder.CreateIndex(
                name: "IX_DossierLines_Mrn",
                schema: "dossier",
                table: "DossierLines",
                column: "Mrn");

            migrationBuilder.CreateIndex(
                name: "IX_Dossiers_CompanyId",
                schema: "dossier",
                table: "Dossiers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Dossiers_DossierReference",
                schema: "dossier",
                table: "Dossiers",
                column: "DossierReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DossierStatusHistory_DossierId",
                schema: "dossier",
                table: "DossierStatusHistory",
                column: "DossierId");

            migrationBuilder.CreateIndex(
                name: "IX_ExciseRateAuditEntries_ExciseRateId",
                schema: "audit",
                table: "ExciseRateAuditEntries",
                column: "ExciseRateId");

            migrationBuilder.CreateIndex(
                name: "IX_ExciseRates_ExciseCode",
                schema: "reference",
                table: "ExciseRates",
                column: "ExciseCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExciseRateVersions_ExciseRateId_EffectiveFrom",
                schema: "reference",
                table: "ExciseRateVersions",
                columns: new[] { "ExciseRateId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_ExtractedFields_DossierDocumentId",
                schema: "dossier",
                table: "ExtractedFields",
                column: "DossierDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_LocalizationEntries_Key",
                schema: "i18n",
                table: "LocalizationEntries",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocalizationEntries_Module_Screen",
                schema: "i18n",
                table: "LocalizationEntries",
                columns: new[] { "Module", "Screen" });

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Code",
                schema: "identity",
                table: "Permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_PermissionId",
                schema: "identity",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Code",
                schema: "identity",
                table: "Roles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TerminologyAuditEntries_LocalizationKey",
                schema: "audit",
                table: "TerminologyAuditEntries",
                column: "LocalizationKey");

            migrationBuilder.CreateIndex(
                name: "IX_TerminologyOverrides_LocalizationEntryId_Language",
                schema: "i18n",
                table: "TerminologyOverrides",
                columns: new[] { "LocalizationEntryId", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoleAssignments_UserId_RoleId",
                schema: "identity",
                table: "UserRoleAssignments",
                columns: new[] { "UserId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ExternalIdentityId",
                schema: "identity",
                table: "Users",
                column: "ExternalIdentityId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ac4Declarations",
                schema: "dossier");

            migrationBuilder.DropTable(
                name: "AuditLogEntries",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "Companies",
                schema: "dossier");

            migrationBuilder.DropTable(
                name: "ConfigurationOperationAuditEntries",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "ConfigurationSnapshots",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "DossierLines",
                schema: "dossier");

            migrationBuilder.DropTable(
                name: "DossierStatusHistory",
                schema: "dossier");

            migrationBuilder.DropTable(
                name: "ExciseRateAuditEntries",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "ExciseRateVersions",
                schema: "reference");

            migrationBuilder.DropTable(
                name: "ExtractedFields",
                schema: "dossier");

            migrationBuilder.DropTable(
                name: "Permissions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "RolePermissions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "TerminologyAuditEntries",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "TerminologyOverrides",
                schema: "i18n");

            migrationBuilder.DropTable(
                name: "UserRoleAssignments",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "ExciseRates",
                schema: "reference");

            migrationBuilder.DropTable(
                name: "DossierDocuments",
                schema: "dossier");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "LocalizationEntries",
                schema: "i18n");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Dossiers",
                schema: "dossier");
        }
    }
}
