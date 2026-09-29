using BARD.Application.Dossiers.Commands;
using FluentAssertions;
using Xunit;

namespace BARD.Application.Tests;

public class ProcessDossierCommandValidatorTests
{
    private readonly ProcessDossierCommandValidator _sut = new();

    [Fact]
    public void PdfOnlyDossier_IsValid()
    {
        var command = Create(
            new[]
            {
                new UploadedFile(
                    "invoice.pdf",
                    new byte[] { 1 },
                    "application/pdf")
            });

        _sut.Validate(command).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ExcelOnlyDossier_IsValid()
    {
        var command = Create(
            new[]
            {
                new UploadedFile(
                    "operator-overview.xlsx",
                    new byte[] { 1 },
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            });

        _sut.Validate(command).IsValid.Should().BeTrue();
    }

    [Fact]
    public void MultipleExcelFiles_AreValid()
    {
        var command = Create(
            new[]
            {
                new UploadedFile(
                    "return-overview.xlsx",
                    new byte[] { 1 },
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
                new UploadedFile(
                    "other-overview.xlsx",
                    new byte[] { 2 },
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            });

        _sut.Validate(command).IsValid.Should().BeTrue();
    }

    [Fact]
    public void EmptyDossier_IsInvalid()
    {
        var command = Create(Array.Empty<UploadedFile>());

        var result = _sut.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should()
            .Contain(error => error.PropertyName == nameof(ProcessDossierCommand.Files));
    }

    private static ProcessDossierCommand Create(
        IReadOnlyList<UploadedFile> files)
        => new(
            "TEST/2026/001",
            "Test Company",
            "BE0123456789",
            null,
            null,
            null,
            null,
            new DateOnly(2026, 9, 29),
            files);
}
