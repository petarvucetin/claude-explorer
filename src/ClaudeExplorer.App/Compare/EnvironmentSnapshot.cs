using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.App.Compare;

/// <summary>The user-global data read for one environment (no project overlay).</summary>
public sealed record EnvironmentSnapshot(
    IReadOnlyList<EffectiveSetting> Settings,
    ArtifactCatalog Artifacts,
    IReadOnlyList<McpServer> Mcp,
    IReadOnlyList<string> Plugins,
    DependencyReport Dependencies);
