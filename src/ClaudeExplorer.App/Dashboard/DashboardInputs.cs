using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.App.Dashboard;

public sealed record DashboardInputs(
    EffectiveConfig Config,
    ArtifactCatalog Artifacts,
    DependencyReport Dependencies,
    IReadOnlyList<McpServer> McpServers,
    IReadOnlyList<ChangeLogEntry> RecentChanges,
    string ProjectLabel);
