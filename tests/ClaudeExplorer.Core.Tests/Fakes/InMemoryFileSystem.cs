using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Tests.Fakes;

/// <summary>Deterministic in-memory file system. Paths use forward slashes.</summary>
public sealed class InMemoryFileSystem : IFileSystem, IFileWriter
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    public InMemoryFileSystem AddFile(string path, string content)
    {
        _files[Normalize(path)] = content;
        return this;
    }

    public void WriteAllText(string path, string content) => _files[Normalize(path)] = content;

    public void Delete(string path) => _files.Remove(Normalize(path));

    public bool FileExists(string path) => _files.ContainsKey(Normalize(path));

    public string ReadAllText(string path)
        => _files.TryGetValue(Normalize(path), out var c)
            ? c
            : throw new FileNotFoundException(path);

    public bool DirectoryExists(string path)
    {
        var prefix = Normalize(path).TrimEnd('/') + "/";
        return _files.Keys.Any(k => k.StartsWith(prefix, StringComparison.Ordinal));
    }

    public IReadOnlyList<string> GetDirectories(string path)
    {
        var prefix = Normalize(path).TrimEnd('/') + "/";
        var dirs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var k in _files.Keys)
        {
            if (!k.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var rest = k.Substring(prefix.Length);
            var slash = rest.IndexOf('/');
            if (slash >= 0) dirs.Add(prefix + rest.Substring(0, slash));
        }
        return dirs.OrderBy(d => d, StringComparer.Ordinal).ToList();
    }

    public IReadOnlyList<string> GetFiles(string path, string searchPattern, bool recurse)
    {
        var prefix = Normalize(path).TrimEnd('/') + "/";
        var results = new List<string>();
        foreach (var k in _files.Keys)
        {
            if (!k.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var rest = k.Substring(prefix.Length);
            if (!recurse && rest.Contains('/')) continue;
            var name = rest.Contains('/') ? rest.Substring(rest.LastIndexOf('/') + 1) : rest;
            if (MatchesPattern(name, searchPattern)) results.Add(k);
        }
        results.Sort(StringComparer.Ordinal);
        return results;
    }

    private static bool MatchesPattern(string name, string pattern)
    {
        if (pattern is "*" or "*.*") return true;
        if (pattern.StartsWith("*.", StringComparison.Ordinal))
            return name.EndsWith(pattern.Substring(1), StringComparison.OrdinalIgnoreCase);
        return string.Equals(name, pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
}
