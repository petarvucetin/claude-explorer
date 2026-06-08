namespace ClaudeExplorer.Core.Artifacts;

public enum ArtifactKind { Command, Skill, Subagent }

public enum ArtifactSourceKind { User, Project, Plugin }

public sealed record ArtifactSource(ArtifactSourceKind Kind, string? PluginName = null)
{
    public string Label => Kind == ArtifactSourceKind.Plugin ? $"Plugin: {PluginName}" : Kind.ToString();

    /// <summary>Higher wins when the same (Kind, Name) appears in multiple sources.</summary>
    public int Precedence => Kind switch
    {
        ArtifactSourceKind.Project => 2,
        ArtifactSourceKind.User => 1,
        _ => 0,
    };
}

public sealed record DiscoveredArtifact(
    ArtifactKind Kind,
    string Name,
    string? Summary,
    ArtifactSource Source,
    string FilePath,
    IReadOnlyDictionary<string, string>? Frontmatter = null,
    int ExtraFileCount = 0)
{
    private static readonly IReadOnlyDictionary<string, string> EmptyFm =
        new Dictionary<string, string>();

    /// <summary>Parsed frontmatter fields (case-insensitive keys); never null. Bespoke detail panes
    /// read type-specific fields from here (commands: <c>argument-hint</c>; subagents: <c>tools</c>,
    /// <c>model</c>; etc.).</summary>
    public IReadOnlyDictionary<string, string> Fm => Frontmatter ?? EmptyFm;
}

public sealed record ResolvedArtifact(DiscoveredArtifact Winner, IReadOnlyList<DiscoveredArtifact> Shadowed)
{
    public bool IsShadowing => Shadowed.Count > 0;
}

public sealed record ArtifactCatalog(IReadOnlyList<ResolvedArtifact> Artifacts)
{
    public IEnumerable<ResolvedArtifact> OfKind(ArtifactKind kind)
        => Artifacts.Where(a => a.Winner.Kind == kind);
}
