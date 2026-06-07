using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClaudeExplorer.Core.Catalog;

/// <summary>
/// Parses a marketplace manifest (the <c>.claude-plugin/marketplace.json</c> shape) into normalized
/// <see cref="CatalogItem"/>s. Lenient: malformed/empty JSON yields an empty list; entries without a
/// name are skipped. Items inherit the source's trust. The <c>source</c> field is not resolved here
/// (that is install-time, Phase 6).
/// </summary>
public static class MarketplaceManifestParser
{
    private static readonly JsonDocumentOptions DocOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>The marketplace <c>name</c> and <c>owner.email</c> (for trust classification).</summary>
    public static (string? Name, string? OwnerEmail) ReadHeader(string? manifestText)
    {
        var root = TryParse(manifestText);
        if (root is null) return (null, null);
        var name = (string?)root["name"];
        var email = root["owner"] is JsonObject owner ? (string?)owner["email"] : null;
        return (name, email);
    }

    public static IReadOnlyList<CatalogItem> Parse(string? manifestText, CatalogSource source)
    {
        var root = TryParse(manifestText);
        if (root?["plugins"] is not JsonArray plugins) return Array.Empty<CatalogItem>();

        var items = new List<CatalogItem>();
        foreach (var node in plugins)
        {
            if (node is not JsonObject p) continue;
            var name = (string?)p["name"];
            if (string.IsNullOrWhiteSpace(name)) continue;

            items.Add(new CatalogItem(
                Name: name,
                Type: CatalogItemType.Plugin,
                Summary: (string?)p["description"],
                Author: p["author"] is JsonObject a ? (string?)a["name"] : null,
                Category: (string?)p["category"],
                Homepage: (string?)p["homepage"],
                Tags: ReadStringArray(p["tags"]),
                Source: source,
                Trust: source.Trust));
        }
        return items;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonNode? node)
        => node is JsonArray arr
            ? arr.Select(x => (string?)x).Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).ToList()
            : Array.Empty<string>();

    private static JsonObject? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try { return JsonNode.Parse(text, nodeOptions: null, documentOptions: DocOptions) as JsonObject; }
        catch (JsonException) { return null; }
    }
}
