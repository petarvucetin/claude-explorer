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
}
