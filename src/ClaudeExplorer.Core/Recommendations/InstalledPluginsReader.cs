using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Recommendations;

/// <summary>
/// The set of installed plugin names, read from the on-disk plugin cache
/// <c>{userDir}/.claude/plugins/cache/&lt;marketplace&gt;/&lt;plugin&gt;/&lt;version&gt;/</c>. Local only.
/// </summary>
public sealed class InstalledPluginsReader
{
    private readonly IFileSystem _fs;

    public InstalledPluginsReader(IFileSystem fs) => _fs = fs;

    public IReadOnlySet<string> Read(string userDir)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        var cache = $"{userDir}/.claude/plugins/cache";

        foreach (var marketplaceDir in _fs.GetDirectories(cache))
            foreach (var pluginDir in _fs.GetDirectories(marketplaceDir))
                set.Add(LastSegment(pluginDir));

        return set;
    }

    private static string LastSegment(string path)
    {
        var trimmed = path.TrimEnd('/');
        var i = trimmed.LastIndexOf('/');
        return i >= 0 ? trimmed.Substring(i + 1) : trimmed;
    }
}
