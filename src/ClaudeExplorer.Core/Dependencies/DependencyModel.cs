namespace ClaudeExplorer.Core.Dependencies;

/// <summary>
/// A distinct executable the discovered config depends on. Deduped by <see cref="Name"/> across all
/// hooks/MCP servers; <see cref="ReferencedBy"/> lists each source (e.g. "hook:PreToolUse",
/// "mcp:playwright").
/// </summary>
/// <param name="ResolvedPath">
/// For a plugin-local script referenced via <c>${CLAUDE_PLUGIN_ROOT}</c>, the absolute path the
/// template expands to (so health is a file-existence check, not a PATH lookup). <c>null</c> for an
/// ordinary PATH runtime.
/// </param>
public sealed record DependencyRef(
    string Name,
    string Raw,
    IReadOnlyList<string> ReferencedBy,
    string? ResolvedPath = null);

public enum DependencyStatusKind
{
    /// <summary>Resolved on PATH and version-probed (an allowlisted runtime).</summary>
    Found,
    /// <summary>Not resolvable on PATH.</summary>
    Missing,
    /// <summary>Present on PATH but not in the probe allowlist, so intentionally not executed.</summary>
    Unverifiable,
}

public sealed record DependencyStatus(
    DependencyStatusKind Kind,
    string? Version = null,
    string? Path = null);

public sealed record DependencyResult(DependencyRef Ref, DependencyStatus Status);

public sealed record DependencyReport(IReadOnlyList<DependencyResult> Results)
{
    public int Count(DependencyStatusKind kind) => Results.Count(r => r.Status.Kind == kind);

    /// <summary>True when nothing is outright missing (Unverifiable is not a failure).</summary>
    public bool AllHealthy => Results.All(r => r.Status.Kind != DependencyStatusKind.Missing);
}
