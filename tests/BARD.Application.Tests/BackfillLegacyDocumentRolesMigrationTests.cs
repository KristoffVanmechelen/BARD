using BARD.Infrastructure.Persistence.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace BARD.Application.Tests;

public class BackfillLegacyDocumentRolesMigrationTests
{
    [Fact]
    public void Up_ContainsFourTargetedBackfillOperations()
    {
        var operations = BuildUpOperations();

        operations.Should().HaveCount(4);
        operations.Should().OnlyContain(
            operation => operation is SqlOperation);
    }

    [Fact]
    public void Up_LegacyInvoiceRole_IsResetToUnknownConservatively()
    {
        var sql = GetUpSql()[0];

        sql.Should().Contain(
            "[DocumentKind] = 'Invoice'");
        sql.Should().Contain(
            "[DocumentRole] = 'SalesInvoice'");
        sql.Should().Contain(
            "[DocumentRole] = 'Unknown'");

        sql.Should().Contain(
            "[RoleConfidence] = 0");
        sql.Should().Contain(
            "[RoleReasons] IS NULL");

        AssertRequiresNoUserConfirmation(sql);
    }

    [Fact]
    public void Up_UnambiguousKinds_AreBackfilledToExpectedRoles()
    {
        var sql = GetUpSql();

        sql[1].Should().Contain(
            "[DocumentKind] = 'CompanyExcelClaim'");
        sql[1].Should().Contain(
            "[DocumentRole] = 'RefundClaim'");

        sql[2].Should().Contain(
            "[DocumentKind] = 'Ac4Declaration'");
        sql[2].Should().Contain(
            "[DocumentRole] = 'DispatchEvidence'");

        sql[3].Should().Contain(
            "[DocumentKind] = 'EadEVadDocument'");
        sql[3].Should().Contain(
            "[DocumentRole] = 'DispatchEvidence'");

        foreach (var statement in sql.Skip(1))
        {
            statement.Should().Contain(
                "[DocumentRole] = 'Unknown'");
            statement.Should().Contain(
                "[RoleConfidence] = 0");
            statement.Should().Contain(
                "[RoleReasons] IS NULL");

            AssertRequiresNoUserConfirmation(statement);
        }
    }

    [Fact]
    public void Up_DoesNotCreateFalseUserConfirmationData()
    {
        foreach (var sql in GetUpSql())
        {
            sql.Should().NotContain(
                "SET [RoleConfirmedByUser]");
            sql.Should().NotContain(
                "SET [RoleConfirmedByUserId]");
            sql.Should().NotContain(
                "SET [RoleConfirmedAtUtc]");
        }
    }

    [Fact]
    public void Down_OnlyTargetsRowsTaggedByThisMigration()
    {
        var sql = GetDownSql();

        sql.Should().HaveCount(4);

        sql[0].Should().Contain(
            "Legacy role corrected:");
        sql[1].Should().Contain(
            "Legacy role backfilled from DocumentKind.CompanyExcelClaim.");
        sql[2].Should().Contain(
            "Legacy role backfilled from DocumentKind.Ac4Declaration.");
        sql[3].Should().Contain(
            "Legacy role backfilled from DocumentKind.EadEVadDocument.");

        foreach (var statement in sql)
        {
            AssertRequiresNoUserConfirmation(statement);
        }
    }

    private static void AssertRequiresNoUserConfirmation(
        string sql)
    {
        sql.Should().Contain(
            "[RoleConfirmedByUser] = 0");
        sql.Should().Contain(
            "[RoleConfirmedByUserId] IS NULL");
        sql.Should().Contain(
            "[RoleConfirmedAtUtc] IS NULL");
    }

    private static IReadOnlyList<MigrationOperation>
        BuildUpOperations()
    {
        var migration = new TestableMigration();

        return migration.BuildUpOperations();
    }

    private static IReadOnlyList<string> GetUpSql()
    {
        return BuildUpOperations()
            .Cast<SqlOperation>()
            .Select(operation => operation.Sql)
            .ToList();
    }

    private static IReadOnlyList<string> GetDownSql()
    {
        var migration = new TestableMigration();

        return migration.BuildDownOperations()
            .Cast<SqlOperation>()
            .Select(operation => operation.Sql)
            .ToList();
    }

    private sealed class TestableMigration
        : BackfillLegacyDocumentRoles
    {
        public IReadOnlyList<MigrationOperation>
            BuildUpOperations()
        {
            var builder =
                new MigrationBuilder(
                    "Microsoft.EntityFrameworkCore.SqlServer");

            base.Up(builder);

            return builder.Operations;
        }

        public IReadOnlyList<MigrationOperation>
            BuildDownOperations()
        {
            var builder =
                new MigrationBuilder(
                    "Microsoft.EntityFrameworkCore.SqlServer");

            base.Down(builder);

            return builder.Operations;
        }
    }
}
