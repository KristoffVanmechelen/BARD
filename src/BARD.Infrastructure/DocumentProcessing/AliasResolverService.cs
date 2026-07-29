using System.Text.Json;
using System.Text.RegularExpressions;
using BARD.Application.DocumentProcessing.Interfaces;
using Microsoft.Extensions.Configuration;

namespace BARD.Infrastructure.DocumentProcessing;

/// <summary>
/// Port of core/matching/alias_resolver.py. Backed by a JSON file
/// (canonical name -> synonym list), same shape as the Python
/// prototype's aliases.json, editable by the officer without a
/// redeploy — matches the "administrators must be able to extend the
/// synonym dictionary" requirement from the functional spec.
/// </summary>
public class AliasResolverService : IAliasResolverService
{
    private readonly Dictionary<string, string> _canonicalByAlias = new();

    public AliasResolverService(IConfiguration configuration)
    {
        var path = configuration["DocumentProcessing:AliasesFilePath"] ?? "DocumentProcessing/Data/aliases.json";
        Load(path);
    }

    private static string Normalise(string text)
    {
        var lowered = text.ToLowerInvariant().Trim();
        var noPunctuation = Regex.Replace(lowered, @"[^\w\s]", " ");
        return Regex.Replace(noPunctuation, @"\s+", " ").Trim();
    }

    private void Load(string path)
    {
        if (!File.Exists(path)) return;

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);

        foreach (var property in doc.RootElement.EnumerateObject())
        {
            if (property.Name.StartsWith('_')) continue; // skip "_comment" metadata keys

            var canonical = property.Name;
            _canonicalByAlias[Normalise(canonical)] = canonical;

            foreach (var synonym in property.Value.EnumerateArray())
            {
                var value = synonym.GetString();
                if (value is not null)
                    _canonicalByAlias[Normalise(value)] = canonical;
            }
        }
    }

    public string? Resolve(string productDescription)
    {
        if (string.IsNullOrWhiteSpace(productDescription)) return null;
        return _canonicalByAlias.TryGetValue(Normalise(productDescription), out var canonical) ? canonical : null;
    }

    public bool SameProduct(string descriptionA, string descriptionB)
    {
        var a = Resolve(descriptionA);
        var b = Resolve(descriptionB);
        return a is not null && a == b;
    }
}
