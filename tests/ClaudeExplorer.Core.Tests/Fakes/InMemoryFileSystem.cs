using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Tests.Fakes;

/// <summary>Deterministic in-memory file system. Paths use forward slashes.</summary>
public sealed class InMemoryFileSystem : IFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    public InMemoryFileSystem AddFile(string path, string content)
    {
        _files[Normalize(path)] = content;
        return this;
    }

    public bool FileExists(string path) => _files.ContainsKey(Normalize(path));

    public string ReadAllText(string path)
        => _files.TryGetValue(Normalize(path), out var c)
            ? c
            : throw new FileNotFoundException(path);

    private static string Normalize(string path) => path.Replace('\\', '/');
}
