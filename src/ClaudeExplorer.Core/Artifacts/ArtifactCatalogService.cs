using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Artifacts;

/// <summary>Top-level façade: discover all file-based artifacts and resolve them into a catalog.</summary>
public sealed class ArtifactCatalogService
{
    private readonly ArtifactDiscoverer _discoverer;
    private readonly ArtifactResolver _resolver;

    public ArtifactCatalogService(IFileSystem fileSystem)
    {
        _discoverer = new ArtifactDiscoverer(fileSystem);
        _resolver = new ArtifactResolver();
    }

    public ArtifactCatalog Build(
        string userDir,
        string? projectDir = null,
        IReadOnlyList<PluginLocation>? plugins = null)
    {
        var discovered = _discoverer.Discover(userDir, projectDir, plugins ?? Array.Empty<PluginLocation>());
        return _resolver.Resolve(discovered);
    }
}
