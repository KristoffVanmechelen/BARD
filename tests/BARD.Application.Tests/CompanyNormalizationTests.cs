using BARD.Application.Common.Services;
using BARD.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace BARD.Application.Tests;

public class CompanyNumberNormalizerTests
{
    [Theory]
    [InlineData("BE 0123.456.789", "BE0123456789")]
    [InlineData("be0123456789", "BE0123456789")]
    [InlineData("BE-0123-456-789", "BE0123456789")]
    [InlineData("BE0123456789", "BE0123456789")]
    public void Normalize_ProducesIdenticalResultForEquivalentInputs(string input, string expected)
    {
        CompanyNumberNormalizer.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Normalize_EmptyInput_Throws()
    {
        var act = () => CompanyNumberNormalizer.Normalize("");
        act.Should().Throw<ArgumentException>();
    }
}

public class CompanyDeduplicationTests
{
    [Fact]
    public void TwoDifferentlyFormattedNumbers_NormalizeToSameDedupKey()
    {
        var a = CompanyNumberNormalizer.Normalize("BE 0123.456.789");
        var b = CompanyNumberNormalizer.Normalize("BE-0123-456-789");

        a.Should().Be(b);
    }

    [Fact]
    public void Company_Create_PreservesOriginalDisplayNumber_ButStoresNormalizedSeparately()
    {
        var normalized = CompanyNumberNormalizer.Normalize("BE 0123.456.789");
        var company = Company.Create("ACME BVBA", "BE 0123.456.789", normalized, Guid.NewGuid());

        company.EnterpriseNumber.Should().Be("BE 0123.456.789"); // original, as entered
        company.NormalizedEnterpriseNumber.Should().Be("BE0123456789"); // dedup key
    }

    [Fact]
    public void UpdateContactDetails_NeverChangesIdentity()
    {
        var normalized = CompanyNumberNormalizer.Normalize("BE0123456789");
        var company = Company.Create("ACME BVBA", "BE0123456789", normalized, Guid.NewGuid());

        company.UpdateContactDetails("ACME NV (renamed)", "New Street 1", "1000", "Brussels", "BE", Guid.NewGuid());

        company.Name.Should().Be("ACME NV (renamed)");
        company.NormalizedEnterpriseNumber.Should().Be("BE0123456789"); // unchanged identity
    }
}
