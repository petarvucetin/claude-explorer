using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Dependencies;

/// <summary>
/// Top-level façade: compute the effective config + read MCP servers, extract executable
/// dependencies, and check each one safely. Answers "will this config actually work on this
/// machine, and what's broken?"
/// </summary>
public sealed class DependencyHealthService
{
    private readonly EffectiveConfigService _config;
    private readonly McpServerReader _mcp;
    private readonly DependencyExtractor _extractor;
    private readonly DependencyChecker _checker;

    public DependencyHealthService(IFileSystem fileSystem, IPathResolver resolver, IProcessRunner runner)
    {
        _config = new EffectiveConfigService(fileSystem);
        _mcp = new McpServerReader(fileSystem);
        _extractor = new DependencyExtractor();
        _checker = new DependencyChecker(resolver, runner);
    }

    public DependencyReport Check(string userDir, string projectDir, string? enterprisePath = null)
    {
        var config = _config.Compute(userDir, projectDir, enterprisePath);
        var servers = _mcp.Read(userDir, projectDir, enterprisePath);
        var refs = _extractor.Extract(config, servers);
        return _checker.Check(refs);
    }
}
