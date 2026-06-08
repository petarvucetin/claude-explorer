using ClaudeExplorer.App.Environments;
using ClaudeExplorer.App.Tests.Fakes;

namespace ClaudeExplorer.App.Tests.Environments;

public class ProjectRegistryTests
{
    private const string Path = "/home/.claude/.claude-explorer/environments.json";

    private static (ProjectRegistry reg, InMemoryFileSystem fs) Build()
    {
        var fs = new InMemoryFileSystem();
        var store = new EnvironmentStore(fs, fs, Path);
        var reg = new ProjectRegistry(store);
        reg.Load();
        return (reg, fs);
    }

    [Fact]
    public void Add_then_load_round_trips()
    {
        var (reg, fs) = Build();
        reg.Add("Project A", "win", "D:/work/a");

        var reg2 = new ProjectRegistry(new EnvironmentStore(fs, fs, Path));
        reg2.Load();

        var p = Assert.Single(reg2.All);
        Assert.Equal("Project A", p.Name);
        Assert.Equal("D:/work/a", p.ProjectDir);
        Assert.Equal("win", p.EnvId);
    }

    [Fact]
    public void Remove_drops_the_project()
    {
        var (reg, _) = Build();
        reg.Add("A", "win", "D:/a");
        var id = reg.All.Single().Id;
        reg.Remove(id);
        Assert.Empty(reg.All);
    }

    [Fact]
    public void Add_is_idempotent_by_env_and_dir()
    {
        var (reg, _) = Build();
        reg.Add("A", "win", "D:/a");
        reg.Add("A again", "win", "D:/a");
        Assert.Single(reg.All);
    }
}
