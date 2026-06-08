using ClaudeExplorer.App.Environments;
using ClaudeExplorer.App.Tests.Fakes;

namespace ClaudeExplorer.App.Tests.Environments;

public class EnvironmentDiscoveryTests
{
    [Fact]
    public void Always_includes_a_windows_environment()
    {
        var disc = new EnvironmentDiscovery(new InMemoryFileSystem(), new FakeWslLocator(), "C:/Users/p");

        var envs = disc.Discover();

        var win = Assert.Single(envs);
        Assert.Equal(EnvironmentKind.Windows, win.Kind);
        Assert.Equal("C:/Users/p", win.UserDir);
        Assert.Equal("windows", win.Id);
    }

    [Fact]
    public void Includes_a_wsl_distro_only_when_it_has_a_dotclaude_folder()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("//wsl.localhost/Ubuntu/home/p/.claude/settings.json", "{}"); // Ubuntu has .claude
        var wsl = new FakeWslLocator()
            .AddDistro("Ubuntu", "//wsl.localhost/Ubuntu/home/p")
            .AddDistro("docker-desktop", "//wsl.localhost/docker-desktop/root"); // no .claude

        var envs = new EnvironmentDiscovery(fs, wsl, "C:/Users/p").Discover();

        Assert.Equal(2, envs.Count);
        var ubuntu = envs.Single(e => e.Kind == EnvironmentKind.Wsl);
        Assert.Equal("wsl:Ubuntu", ubuntu.Id);
        Assert.Equal("WSL · Ubuntu", ubuntu.Name);
        Assert.Equal("//wsl.localhost/Ubuntu/home/p", ubuntu.UserDir);
    }

    [Fact]
    public void Skips_distros_whose_home_cannot_be_resolved()
    {
        var wsl = new FakeWslLocator().AddDistro("Broken"); // no home registered → ResolveHome null

        var envs = new EnvironmentDiscovery(new InMemoryFileSystem(), wsl, "C:/Users/p").Discover();

        Assert.Single(envs); // just Windows
    }
}
