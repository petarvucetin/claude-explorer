using ClaudeExplorer.App.Services;
using ClaudeExplorer.Core;
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.App.Dashboard;

public sealed class EngineDashboardDataSource : IDashboardDataSource
{
    private readonly IWorkspaceContext _workspace;
    private readonly EffectiveConfigService _config;
    private readonly ArtifactCatalogService _artifacts;
    private readonly DependencyHealthService _health;
    private readonly McpServerReader _mcp;
    private readonly SafeMutationService _mutation;

    public EngineDashboardDataSource(
        IWorkspaceContext workspace,
        EffectiveConfigService config,
        ArtifactCatalogService artifacts,
        DependencyHealthService health,
        McpServerReader mcp,
        SafeMutationService mutation)
    {
        _workspace = workspace;
        _config = config;
        _artifacts = artifacts;
        _health = health;
        _mcp = mcp;
        _mutation = mutation;
    }

    public DashboardInputs GetInputs()
    {
        var user = _workspace.UserDir;
        var project = _workspace.ProjectDir;
        return new DashboardInputs(
            _config.Compute(user, project),
            _artifacts.Build(user, project),
            _health.Check(user, project),
            _mcp.Read(user, project),
            _mutation.ChangeLog.Entries,
            _workspace.ProjectLabel);
    }
}
