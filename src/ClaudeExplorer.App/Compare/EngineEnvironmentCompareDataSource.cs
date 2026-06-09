using ClaudeExplorer.App.Environments;
using ClaudeExplorer.Core;
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Recommendations;

namespace ClaudeExplorer.App.Compare;

public sealed class EngineEnvironmentCompareDataSource : IEnvironmentCompareDataSource
{
    private readonly EffectiveConfigService _config;
    private readonly ArtifactCatalogService _artifacts;
    private readonly McpServerReader _mcp;
    private readonly InstalledPluginsReader _plugins;
    private readonly DependencyHealthService _health;
    private readonly IFileSystem _fs;

    public EngineEnvironmentCompareDataSource(
        EffectiveConfigService config, ArtifactCatalogService artifacts, McpServerReader mcp,
        InstalledPluginsReader plugins, DependencyHealthService health, IFileSystem fs)
    {
        _config = config;
        _artifacts = artifacts;
        _mcp = mcp;
        _plugins = plugins;
        _health = health;
        _fs = fs;
    }

    public EnvironmentSnapshot Snapshot(ClaudeEnvironment env)
        => Snapshot(CompareEndpoint.Base(env.Id, env.Name, env.UserDir));

    public EnvironmentSnapshot Snapshot(CompareEndpoint endpoint)
    {
        var u = endpoint.ReadUserDir;
        var p = endpoint.ReadProjectDir;
        return new EnvironmentSnapshot(
            _config.Compute(u, p).Settings,
            _artifacts.Build(u, p, plugins: Array.Empty<PluginLocation>()),
            _mcp.Read(u, p),
            endpoint.Kind == EndpointKind.Base
                ? _plugins.Read(u).ToList()
                : new List<string>(),
            _health.Check(u, p),
            ReadMemory(endpoint));
    }

    private IReadOnlyDictionary<string, string> ReadMemory(CompareEndpoint endpoint)
    {
        var mem = new Dictionary<string, string>(StringComparer.Ordinal);
        if (endpoint.Kind == EndpointKind.Base)
        {
            var u = endpoint.UserDir;
            AddMemory(mem, "CLAUDE.md", $"{u}/.claude/CLAUDE.md");
        }
        else
        {
            var e = endpoint;
            var projDir = e.ProjectDir ?? "";
            AddMemory(mem, "CLAUDE.md", $"{projDir}/CLAUDE.md");
            AddMemory(mem, "CLAUDE.local.md", $"{projDir}/CLAUDE.local.md");
        }
        return mem;
    }

    private void AddMemory(Dictionary<string, string> mem, string name, string path)
    {
        if (_fs.FileExists(path)) mem[name] = _fs.ReadAllText(path);
    }
}
