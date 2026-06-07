namespace ClaudeExplorer.Core.Artifacts;

/// <summary>
/// Resolves discovered artifacts into a deduplicated, sorted catalog.
/// Artifact names are matched case-sensitively (ordinal); same-precedence ties
/// (e.g. two plugins) are broken deterministically by plugin name then file path.
/// </summary>
public sealed class ArtifactResolver
{
    public ArtifactCatalog Resolve(IReadOnlyList<DiscoveredArtifact> discovered)
    {
        var resolved = new List<ResolvedArtifact>();

        foreach (var group in discovered.GroupBy(a => (a.Kind, a.Name)))
        {
            var ordered = group
                .OrderByDescending(a => a.Source.Precedence)
                .ThenBy(a => a.Source.PluginName ?? "", StringComparer.Ordinal)
                .ThenBy(a => a.FilePath, StringComparer.Ordinal)
                .ToList();
            resolved.Add(new ResolvedArtifact(ordered[0], ordered.Skip(1).ToList()));
        }

        var sorted = resolved
            .OrderBy(r => r.Winner.Kind)
            .ThenBy(r => r.Winner.Name, StringComparer.Ordinal)
            .ToList();

        return new ArtifactCatalog(sorted);
    }
}
