using ClaudeExplorer.App.Environments;
using ClaudeExplorer.Core;
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Recommendations;

namespace ClaudeExplorer.App.Compare;

public sealed class EngineEnvironmentCompareDataSource : IEnvironmentCompareDataSource
{
    private readonly EffectiveConfigService _config;
    private readonly ArtifactCatalogService _artifacts;
    private readonly McpServerReader _mcp;
    private readonly InstalledPluginsReader _plugins;
    private readonly DependencyHealthService _health;

    public EngineEnvironmentCompareDataSource(
        EffectiveConfigService config, ArtifactCatalogService artifacts, McpServerReader mcp,
        InstalledPluginsReader plugins, DependencyHealthService health)
    {
        _config = config;
        _artifacts = artifacts;
        _mcp = mcp;
        _plugins = plugins;
        _health = health;
    }

    public EnvironmentSnapshot Snapshot(ClaudeEnvironment env)
    {
        var user = env.UserDir;
        const string noProject = "";
        return new EnvironmentSnapshot(
            _config.Compute(user, noProject).Settings,
            _artifacts.Build(user, noProject),
            _mcp.Read(user, noProject),
            _plugins.Read(user).ToList(), // InstalledPluginsReader.Read returns IReadOnlySet<string>
            _health.Check(user, noProject));
    }
}
