using BARD.Infrastructure.DocumentProcessing;
using FluentAssertions;
using Xunit;

namespace BARD.Application.Tests;

public class ExciseCodeMappingServiceTests
{
    private readonly ExciseCodeMappingService _sut = new();

    [Theory]
    [InlineData("W200", "S101")]
    [InlineData("W300", "S109")]
    public void DirectWineMappings_AreDeterministic(
        string emcsCode,
        string expected)
    {
        var result = _sut.Map(emcsCode, null, null, null);

        result.BelgianExciseCode.Should().Be(expected);
        result.Confidence.Should().Be(1m);
    }

    [Fact]
    public void BeerCode_IsNotGuessed()
    {
        var result =
            _sut.Map(
                "B000",
                "Bier als omschreven in artikel 2",
                6.2m,
                11.7m);

        result.BelgianExciseCode.Should().BeNull();
    }
}
