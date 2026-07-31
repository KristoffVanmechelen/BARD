using BARD.Application.Dossiers.Commands;
using BARD.Contracts.Dossiers;
using FluentAssertions;
using Xunit;

namespace BARD.Application.Tests;

public class CorrectInvoiceRoleCommandValidatorTests
{
    private readonly CorrectInvoiceRoleCommandValidator _sut = new();

    [Theory]
    [InlineData("PurchaseInvoice")]
    [InlineData("purchaseinvoice")]
    [InlineData("SalesInvoice")]
    [InlineData("salesinvoice")]
    public void SupportedInvoiceRole_IsValid(string role)
    {
        var command = CreateCommand(
            role,
            "Confirmed after manual review.");

        var result = _sut.Validate(command);

        result.IsValid.Should()
            .BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Unknown")]
    [InlineData("DispatchEvidence")]
    [InlineData("NotARole")]
    public void UnsupportedInvoiceRole_IsInvalid(string role)
    {
        var command = CreateCommand(
            role,
            "Confirmed after manual review.");

        var result = _sut.Validate(command);

        result.IsValid.Should()
            .BeFalse();

        result.Errors.Should()
            .Contain(error =>
                error.PropertyName
                == "Request.DocumentRole");
    }

    [Fact]
    public void MissingReason_IsInvalid()
    {
        var command = CreateCommand(
            "PurchaseInvoice",
            "");

        var result = _sut.Validate(command);

        result.IsValid.Should()
            .BeFalse();

        result.Errors.Should()
            .Contain(error =>
                error.PropertyName
                == "Request.Reasons");
    }

    [Fact]
    public void MissingDocumentId_IsInvalid()
    {
        var command = new CorrectInvoiceRoleCommand(
            Guid.Empty,
            new CorrectInvoiceRoleRequest(
                "PurchaseInvoice",
                "Confirmed after manual review."));

        var result = _sut.Validate(command);

        result.IsValid.Should()
            .BeFalse();

        result.Errors.Should()
            .Contain(error =>
                error.PropertyName
                == nameof(
                    CorrectInvoiceRoleCommand
                        .DossierDocumentId));
    }

    private static CorrectInvoiceRoleCommand CreateCommand(
        string role,
        string reasons)
    {
        return new CorrectInvoiceRoleCommand(
            Guid.NewGuid(),
            new CorrectInvoiceRoleRequest(
                role,
                reasons));
    }
}