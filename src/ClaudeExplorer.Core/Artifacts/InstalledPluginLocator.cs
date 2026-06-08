using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Artifacts;

/// <summary>
/// Enumerates installed plugin roots from the on-disk cache
/// <c>{userDir}/.claude/plugins/cache/&lt;marketplace&gt;/&lt;plugin&gt;/&lt;version&gt;/</c>. Each version
/// directory is a plugin root that may contain <c>commands/</c>, <c>skills/</c>, <c>agents/</c>, and
/// <c>hooks/</c>. Returns a <see cref="PluginLocation"/> per installed (plugin, version), named by the
/// plugin directory. Local only — reads directory names, never executes anything.
/// </summary>
public sealed class InstalledPluginLocator
{
    private readonly IFileSystem _fs;

    public InstalledPluginLocator(IFileSystem fs) => _fs = fs;

    public IReadOnlyList<PluginLocation> Locate(string userDir)
    {
        var result = new List<PluginLocation>();
        var cache = $"{userDir}/.claude/plugins/cache";

        foreach (var marketplaceDir in _fs.GetDirectories(cache))
            foreach (var pluginDir in _fs.GetDirectories(marketplaceDir))
            {
                var name = LastSegment(pluginDir);
                foreach (var versionDir in _fs.GetDirectories(pluginDir))
                    result.Add(new PluginLocation(name, versionDir));
            }

        return result;
    }

    private static string LastSegment(string path)
    {
        var trimmed = path.TrimEnd('/');
        var i = trimmed.LastIndexOf('/');
        return i >= 0 ? trimmed.Substring(i + 1) : trimmed;
    }
}
