using BARD.Application.DocumentProcessing.Interfaces;

namespace BARD.Application.Tests;

public class FakeAliasResolver : IAliasResolverService
{
    private readonly Dictionary<string, string> _canonicalByAlias;

    public FakeAliasResolver()
    {
        _canonicalByAlias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["JUP"] = "JUPILER",
            ["JUPILER"] = "JUPILER",
            ["JUPILER 24X33"] = "JUPILER",
            ["JUP VP"] = "JUPILER",
            ["DUVEL"] = "DUVEL",
            ["DUVEL 24X33"] = "DUVEL",
        };
    }

    public string? Resolve(string productDescription) =>
        _canonicalByAlias.TryGetValue(productDescription.Trim(), out var canonical) ? canonical : null;

    public bool SameProduct(string descriptionA, string descriptionB)
    {
        var a = Resolve(descriptionA);
        var b = Resolve(descriptionB);
        return a is not null && a == b;
    }
}
