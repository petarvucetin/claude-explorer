using ClaudeExplorer.Core.Artifacts;

namespace ClaudeExplorer.App.Screens.Artifacts;

/// <summary>View-model record for a single artifact item in the master list.</summary>
public sealed record ArtifactItem(
    ArtifactKind Kind,
    string Name,
    string? Summary,
    bool IsShadowing,
    string FilePath,
    IReadOnlyList<DiscoveredArtifact> Shadowed);

/// <summary>A source group in the master list.</summary>
public sealed record ArtifactGroup(string Label, IReadOnlyList<ArtifactItem> Items);

/// <summary>Pure static mapper: groups a <see cref="ArtifactCatalog"/> into source groups and
/// applies kind filter + name search.</summary>
public static class ArtifactBrowserMapper
{
    public static IReadOnlyList<ArtifactGroup> Group(ArtifactCatalog catalog)
    {
        return catalog.Artifacts
            .GroupBy(a => a.Winner.Source.Label, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new ArtifactGroup(
                g.Key,
                g.Select(a => new ArtifactItem(
                        a.Winner.Kind,
                        a.Winner.Name,
                        a.Winner.Summary,
                        a.IsShadowing,
                        a.Winner.FilePath,
                        a.Shadowed))
                    .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .ToList();
    }

    /// <summary>Filter a flat list of items by optional kind and name search (ordinal, case-insensitive).</summary>
    public static IReadOnlyList<ArtifactGroup> Filter(
        IReadOnlyList<ArtifactGroup> groups,
        ArtifactKind? kind,
        string? search)
    {
        return groups
            .Select(g => g with
            {
                Items = g.Items
                    .Where(i => kind is null || i.Kind == kind)
                    .Where(i => string.IsNullOrEmpty(search) ||
                                i.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList()
            })
            .Where(g => g.Items.Count > 0)
            .ToList();
    }
}
