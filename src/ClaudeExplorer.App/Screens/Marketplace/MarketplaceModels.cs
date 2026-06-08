using ClaudeExplorer.Core.Catalog;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.App.Screens.Marketplace;

/// <summary>A view-ready row for a catalog item in the marketplace list.</summary>
public sealed record MarketplaceItemRow(
    string Name,
    CatalogItemType Type,
    string? Summary,
    string? Author,
    TrustLevel Trust,
    CatalogSourceKind SourceKind,
    string SourceName);

/// <summary>Maps <see cref="CatalogItem"/> lists to view rows.</summary>
public static class MarketplaceMapper
{
    public static IReadOnlyList<MarketplaceItemRow> Map(IReadOnlyList<CatalogItem> items)
        => items.Select(i => new MarketplaceItemRow(
            i.Name,
            i.Type,
            i.Summary,
            i.Author,
            i.Trust,
            i.Source.Kind,
            i.Source.Name)).ToList();

    public static string TypeLabel(CatalogItemType t) => t switch
    {
        CatalogItemType.Plugin => "Plugin",
        CatalogItemType.Skill  => "Skill",
        CatalogItemType.Agent  => "Agent",
        _                      => t.ToString(),
    };

    public static IReadOnlyList<string> InstallArgs(string name)
        => new[] { "plugin", "install", name };

    public static IReadOnlyList<string> UninstallArgs(string name)
        => new[] { "plugin", "uninstall", name };
}
