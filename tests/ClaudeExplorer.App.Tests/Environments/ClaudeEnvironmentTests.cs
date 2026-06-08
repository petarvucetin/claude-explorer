using ClaudeExplorer.App.Environments;

namespace ClaudeExplorer.App.Tests.Environments;

public class ClaudeEnvironmentTests
{
    [Fact]
    public void Windows_environment_carries_its_fields()
    {
        var env = new ClaudeEnvironment("windows", "Windows", EnvironmentKind.Windows, "C:/Users/p", null);
        Assert.Equal("windows", env.Id);
        Assert.Equal(EnvironmentKind.Windows, env.Kind);
        Assert.Null(env.ProjectDir);
    }

    [Fact]
    public void WithProject_returns_a_copy_with_the_project_set()
    {
        var env = new ClaudeEnvironment("wsl:Ubuntu", "WSL · Ubuntu", EnvironmentKind.Wsl, "//wsl.localhost/Ubuntu/home/p", null);
        var withProj = env with { ProjectDir = "/work/app" };
        Assert.Equal("/work/app", withProj.ProjectDir);
        Assert.Null(env.ProjectDir); // original unchanged (record immutability)
    }
}
