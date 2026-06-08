using ClaudeExplorer.App.Environments;
using ClaudeExplorer.App.Tests.Fakes;

namespace ClaudeExplorer.App.Tests.Environments;

public class EnvironmentServiceTests
{
    private const string StorePath = "/home/.claude/.claude-explorer/environments.json";

    private static EnvironmentService Build(InMemoryFileSystem fs, FakeWslLocator wsl)
        => new(new EnvironmentDiscovery(fs, wsl, "C:/Users/p"), new EnvironmentStore(fs, fs, StorePath));

    [Fact]
    public void Loads_discovered_plus_custom_and_defaults_active_to_first()
    {
        var fs = new InMemoryFileSystem()
            .AddFile(StorePath, "{\"ActiveId\":null,\"Custom\":[{\"Id\":\"custom:x\",\"Name\":\"X\",\"Kind\":2,\"UserDir\":\"D:/cfg\",\"ProjectDir\":null}],\"Projects\":{}}");
        var svc = Build(fs, new FakeWslLocator());
        svc.Load();

        Assert.Contains(svc.Environments, e => e.Id == "windows");
        Assert.Contains(svc.Environments, e => e.Id == "custom:x");
        Assert.Equal("windows", svc.Active.Id); // first discovered, no persisted active
    }

    [Fact]
    public void SetActive_changes_active_and_raises_Changed()
    {
        var fs = new InMemoryFileSystem();
        var svc = Build(fs, new FakeWslLocator());
        svc.Load();
        svc.AddCustom("D:/cfg", "X"); // gives a second env
        var raised = 0; svc.Changed += () => raised++;

        svc.SetActive(svc.Environments.Last().Id);

        Assert.Equal(svc.Environments.Last().Id, svc.Active.Id);
        Assert.True(raised > 0);
    }

    [Fact]
    public void SetProject_attaches_a_project_to_the_active_env_and_persists()
    {
        var fs = new InMemoryFileSystem();
        var svc = Build(fs, new FakeWslLocator());
        svc.Load();

        svc.SetProject(svc.Active.Id, "/work/app");

        Assert.Equal("/work/app", svc.Active.ProjectDir);
        Assert.True(fs.FileExists(StorePath)); // persisted
    }

    [Fact]
    public void AddCustom_adds_a_custom_environment()
    {
        var fs = new InMemoryFileSystem();
        var svc = Build(fs, new FakeWslLocator());
        svc.Load();

        svc.AddCustom("D:/cfg", "My Root");

        var added = svc.Environments.Single(e => e.Kind == EnvironmentKind.Custom);
        Assert.Equal("My Root", added.Name);
        Assert.Equal("D:/cfg", added.UserDir);
    }

    [Fact]
    public void Refresh_rediscovers_newly_available_environments()
    {
        var fs = new InMemoryFileSystem();
        var wsl = new FakeWslLocator(); // no WSL distros yet
        var svc = Build(fs, wsl);
        svc.Load();
        Assert.Single(svc.Environments); // Windows only
        var raised = 0; svc.Changed += () => raised++;

        // A WSL distro now has a ~/.claude (e.g. the user just created it).
        wsl.AddDistro("Ubuntu", "//wsl.localhost/Ubuntu/home/p");
        fs.AddFile("//wsl.localhost/Ubuntu/home/p/.claude/settings.json", "{}");
        svc.Refresh();

        Assert.Contains(svc.Environments, e => e.Id == "wsl:Ubuntu");
        Assert.True(raised > 0); // Refresh raised Changed so the UI reloads
    }

    [Fact]
    public void Refresh_preserves_the_active_environment_and_custom_roots()
    {
        var fs = new InMemoryFileSystem();
        var svc = Build(fs, new FakeWslLocator());
        svc.Load();
        svc.AddCustom("D:/cfg", "My Root");
        svc.SetActive("custom:D:/cfg");

        svc.Refresh();

        Assert.Equal("custom:D:/cfg", svc.Active.Id);                        // active preserved
        Assert.Contains(svc.Environments, e => e.Id == "custom:D:/cfg");     // custom preserved
    }
}
