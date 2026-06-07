namespace ClaudeExplorer.Core.Artifacts;

public sealed class ArtifactResolver
{
    public ArtifactCatalog Resolve(IReadOnlyList<DiscoveredArtifact> discovered)
    {
        var resolved = new List<ResolvedArtifact>();

        foreach (var group in discovered.GroupBy(a => (a.Kind, a.Name)))
        {
            var ordered = group.OrderByDescending(a => a.Source.Precedence).ToList();
            resolved.Add(new ResolvedArtifact(ordered[0], ordered.Skip(1).ToList()));
        }

        var sorted = resolved
            .OrderBy(r => r.Winner.Kind)
            .ThenBy(r => r.Winner.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ArtifactCatalog(sorted);
    }
}
