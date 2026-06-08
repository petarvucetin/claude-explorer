namespace ClaudeExplorer.Core.Io;

/// <summary>
/// Write-side companion to <see cref="IFileSystem"/>. Kept separate so the read-only engines
/// (discovery, merge, catalog, recommendations) never take a dependency on mutation capability —
/// only the Phase-6 safe-mutation layer accepts an <see cref="IFileWriter"/>.
/// </summary>
public interface IFileWriter
{
    /// <summary>Write <paramref name="content"/> to <paramref name="path"/>, creating parent
    /// directories and overwriting any existing file.</summary>
    void WriteAllText(string path, string content);

    /// <summary>Delete <paramref name="path"/> if it exists; a no-op when it is absent.</summary>
    void Delete(string path);
}

public sealed class PhysicalFileWriter : IFileWriter
{
    public void WriteAllText(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, content);
    }

    public void Delete(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
