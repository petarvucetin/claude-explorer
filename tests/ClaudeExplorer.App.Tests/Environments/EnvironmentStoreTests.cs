using ClaudeExplorer.App.Environments;
using ClaudeExplorer.App.Tests.Fakes;

namespace ClaudeExplorer.App.Tests.Environments;

public class EnvironmentStoreTests
{
    private const string Path = "/home/.claude/.claude-explorer/environments.json";

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
}
