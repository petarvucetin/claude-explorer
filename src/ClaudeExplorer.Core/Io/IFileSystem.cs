namespace ClaudeExplorer.Core.Io;

public interface IFileSystem
{
    bool FileExists(string path);
    string ReadAllText(string path);
}

public sealed class PhysicalFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);
    public string ReadAllText(string path) => File.ReadAllText(path);
}
