using BARD.Application.DocumentProcessing.Models;
using BARD.Domain.Enums;
using BARD.Infrastructure.DocumentProcessing;
using FluentAssertions;
using Xunit;

namespace BARD.Application.Tests;

public class DocumentReferenceResolverServiceTests
{
    private readonly DocumentReferenceResolverService _sut = new();

    [Fact]
    public void ExplicitDrn_IsAuthoritative()
    {
        var refs =
            _sut.ResolveAll(
                "DRN26BEAC4C5G2YU6XJ7B2S9",
                DocumentKind.Ac4Declaration);

        var drn =
            refs.First(
                r => r.Type == DocumentReferenceType.Drn);

        drn.Value.Should()
            .Be("26BEAC4C5G2YU6XJ7B2S9");

        drn.Method.Should()
            .Be(ReferenceResolutionMethod.ExplicitLabel);

        drn.Confidence.Should()
            .Be(1m);
    }

    [Fact]
    public void UnlabelledAc4Pattern_IsInferred_NotProven()
    {
        var refs =
            _sut.ResolveAll(
                "random 26BEAC4C5G2YU6XJ7B2S9 value");

        var drn =
            refs.First(
                r => r.Type == DocumentReferenceType.Drn);

        drn.Method.Should()
            .Be(ReferenceResolutionMethod.PatternInference);

        drn.Confidence.Should()
            .BeLessThan(1m);

        drn.PatternFamily.Should()
            .Be("BE_AC4_CURRENT");
    }

    [Fact]
    public void UnlabelledEmcsPattern_IsArcCandidate()
    {
        var refs =
            _sut.ResolveAll(
                "26BEMN8XSD5E004DFC5S3");

        var arc =
            refs.First(
                r => r.Type == DocumentReferenceType.Arc);

        arc.Method.Should()
            .Be(ReferenceResolutionMethod.PatternInference);

        arc.Confidence.Should()
            .BeLessThan(1m);

        arc.PatternFamily.Should()
            .Be("BE_EMCS_CURRENT");
    }

    [Fact]
    public void ExplicitMrn_IsKeptAsMrn()
    {
        var refs =
            _sut.ResolveAll(
                "MRN 26BEH4000004RZISR0");

        refs.Should()
            .Contain(
                r => r.Type == DocumentReferenceType.Mrn
                     && r.Value == "26BEH4000004RZISR0"
                     && r.Confidence == 1m);
    }

    [Fact]
    public void Lrn_IsSeparateReferenceType()
    {
        var refs =
            _sut.ResolveAll(
                "LRNWeekaccijns2026-34");

        refs.Should()
            .Contain(
                r => r.Type == DocumentReferenceType.Lrn
                     && r.Value == "Weekaccijns2026-34");
    }
}
