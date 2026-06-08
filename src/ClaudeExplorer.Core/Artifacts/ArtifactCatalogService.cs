using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Artifacts;

/// <summary>Top-level façade: discover all file-based artifacts and resolve them into a catalog.</summary>
public sealed class ArtifactCatalogService
{
    private readonly ArtifactDiscoverer _discoverer;
    private readonly InstalledPluginLocator _pluginLocator;
    private readonly ArtifactResolver _resolver;

    public ArtifactCatalogService(IFileSystem fileSystem)
    {
        _discoverer = new ArtifactDiscoverer(fileSystem);
        _pluginLocator = new InstalledPluginLocator(fileSystem);
        _resolver = new ArtifactResolver();
    }

    /// <summary>
    /// Discovers user + project + plugin artifacts. When <paramref name="plugins"/> is null the
    /// installed plugins under <paramref name="userDir"/> are auto-discovered, so plugin-provided
    /// commands/skills/agents appear alongside user/project ones (matching <c>/skills</c>). Pass an
    /// explicit list (including empty) to override auto-discovery.
    /// </summary>
    public ArtifactCatalog Build(
        string userDir,
        string? projectDir = null,
        IReadOnlyList<PluginLocation>? plugins = null)
    {
        var pluginList = plugins ?? _pluginLocator.Locate(userDir);
        var discovered = _discoverer.Discover(userDir, projectDir, pluginList);
        return _resolver.Resolve(discovered);
    }
}
