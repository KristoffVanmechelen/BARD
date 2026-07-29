using BARD.Domain.Entities;
using BARD.Domain.Enums;
using BARD.Infrastructure.DocumentProcessing;
using FluentAssertions;
using Xunit;

namespace BARD.Application.Tests;

public class RefundCalculationServiceTests
{
    [Fact]
    public void PerHectolitre_ComputesQuantityTimesRate()
    {
        var version = ExciseRateVersion.Create(Guid.NewGuid(), 25.50m, ExciseCalculationUnit.PerHectolitre,
            DateOnly.FromDateTime(DateTime.Today), Guid.NewGuid());

        var result = new RefundCalculationService().Calculate(version, 10m, out var notes);

        result.Should().Be(255.0m);
        notes.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void PerDegreePlato_RefusesToCalculate_NeverGuesses()
    {
        var version = ExciseRateVersion.Create(Guid.NewGuid(), 1.2m, ExciseCalculationUnit.PerDegreePlatoPerHectolitre,
            DateOnly.FromDateTime(DateTime.Today), Guid.NewGuid());

        var result = new RefundCalculationService().Calculate(version, 10m, out var notes);

        result.Should().BeNull();
        notes.Should().Contain("Plato");
    }

    [Fact]
    public void OtherUnit_RefusesToCalculate()
    {
        var version = ExciseRateVersion.Create(Guid.NewGuid(), 1m, ExciseCalculationUnit.Other,
            DateOnly.FromDateTime(DateTime.Today), Guid.NewGuid());

        var result = new RefundCalculationService().Calculate(version, 10m, out var notes);

        result.Should().BeNull();
        notes.Should().NotBeNullOrEmpty();
    }
}

public class ExciseRateHistoricalReproducibilityTests
{
    [Fact]
    public void GetCurrentVersion_ReturnsVersionEffectiveAsOfGivenDate_NotLatest()
    {
        var rate = ExciseRate.Create("S101", "Beer", 20m, ExciseCalculationUnit.PerHectolitre,
            new DateOnly(2025, 1, 1), Guid.NewGuid());
        rate.PublishNewVersion(25m, ExciseCalculationUnit.PerHectolitre, new DateOnly(2026, 1, 1), Guid.NewGuid());

        // A calculation dated before the 2026 change must still see the old rate.
        var historical = rate.GetCurrentVersion(new DateOnly(2025, 6, 1));
        historical.Rate.Should().Be(20m);

        var current = rate.GetCurrentVersion(new DateOnly(2026, 6, 1));
        current.Rate.Should().Be(25m);
    }
}
