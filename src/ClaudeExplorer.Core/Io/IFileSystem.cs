namespace ClaudeExplorer.Core.Io;

public interface IFileSystem
{
    bool FileExists(string path);
    string ReadAllText(string path);
    bool DirectoryExists(string path);

    /// <summary>Immediate child directories of <paramref name="path"/> (full paths). Empty if the dir is missing.</summary>
    IReadOnlyList<string> GetDirectories(string path);

    /// <summary>
    /// Files under <paramref name="path"/> matching a simple pattern ("*", "*.md", or an exact name).
    /// Recurses into subdirectories when <paramref name="recurse"/> is true. Empty if the dir is missing.
    /// </summary>
    IReadOnlyList<string> GetFiles(string path, string searchPattern, bool recurse);
}

public sealed class PhysicalFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);
    public string ReadAllText(string path) => File.ReadAllText(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IReadOnlyList<string> GetDirectories(string path)
        => Directory.Exists(path)
            ? Directory.GetDirectories(path).Select(Normalize).ToList()
            : Array.Empty<string>();

    public IReadOnlyList<string> GetFiles(string path, string searchPattern, bool recurse)
        => Directory.Exists(path)
            ? Directory.GetFiles(path, searchPattern,
                    recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Select(Normalize).ToList()
            : Array.Empty<string>();

    private static string Normalize(string p) => p.Replace('\\', '/');
}
