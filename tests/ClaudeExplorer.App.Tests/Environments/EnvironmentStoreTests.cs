using ClaudeExplorer.App.Environments;
using ClaudeExplorer.App.Tests.Fakes;
using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.App.Tests.Environments;

public class EnvironmentStoreTests
{
    private const string Path = "/home/.claude/.claude-explorer/environments.json";

    /// <summary>An existing-but-unreadable file: FileExists is true, ReadAllText throws an IO error.</summary>
    private sealed class IoFailingFileSystem : IFileSystem
    {
        public bool FileExists(string path) => true;
        public string ReadAllText(string path) => throw new IOException("file is locked");
        public bool DirectoryExists(string path) => false;
        public IReadOnlyList<string> GetDirectories(string path) => Array.Empty<string>();
        public IReadOnlyList<string> GetFiles(string path, string searchPattern, bool recurse) => Array.Empty<string>();
    }

    [Fact]
    public void Round_trips_state()
    {
        var fs = new InMemoryFileSystem();
        var store = new EnvironmentStore(fs, fs, Path);

        store.Save(new EnvironmentState(
            ActiveId: "wsl:Ubuntu",
            Custom: new[] { new ClaudeEnvironment("custom:x", "My Root", EnvironmentKind.Custom, "D:/cfg", null) },
            Projects: new Dictionary<string, string> { ["windows"] = "/work/app" }));

        var loaded = store.Load();

        Assert.Equal("wsl:Ubuntu", loaded.ActiveId);
        Assert.Equal("custom:x", Assert.Single(loaded.Custom).Id);
        Assert.Equal("/work/app", loaded.Projects["windows"]);
    }

    [Fact]
    public void Load_returns_empty_state_when_file_missing()
    {
        var loaded = new EnvironmentStore(new InMemoryFileSystem(), new InMemoryFileSystem(), Path).Load();

        Assert.Null(loaded.ActiveId);
        Assert.Empty(loaded.Custom);
        Assert.Empty(loaded.Projects);
    }

    [Fact]
    public void Load_returns_empty_state_on_garbled_json()
    {
        var fs = new InMemoryFileSystem().AddFile(Path, "{ not json");

        Assert.Empty(new EnvironmentStore(fs, fs, Path).Load().Custom);
    }

    [Fact]
    public void Load_returns_empty_state_when_the_file_is_unreadable_does_not_throw()
    {
        // A locked/permission-denied file must degrade gracefully (this runs in the DI factory at startup).
        var store = new EnvironmentStore(new IoFailingFileSystem(), new InMemoryFileSystem(), Path);

        var loaded = store.Load();

        Assert.Null(loaded.ActiveId);
        Assert.Empty(loaded.Custom);
    }
}
