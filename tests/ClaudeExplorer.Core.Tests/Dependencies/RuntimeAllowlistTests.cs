using ClaudeExplorer.Core.Dependencies;

namespace ClaudeExplorer.Core.Tests.Dependencies;

public class RuntimeAllowlistTests
{
    [Theory]
    [InlineData("node")]
    [InlineData("npx")]
    [InlineData("uvx")]
    [InlineData("python3")]
    [InlineData("docker")]
    [InlineData("git")]
    [InlineData("claude")]
    public void Known_runtimes_are_allowed(string name)
    {
        Assert.True(RuntimeAllowlist.IsAllowed(name));
    }

    [Theory]
    [InlineData("rm")]
    [InlineData("curl")]
    [InlineData("my-custom-tool")]
    [InlineData("")]
    public void Unknown_executables_are_not_allowed(string name)
    {
        Assert.False(RuntimeAllowlist.IsAllowed(name));
    }

    [Fact]
    public void Membership_is_case_insensitive()
    {
        Assert.True(RuntimeAllowlist.IsAllowed("NODE"));
        Assert.True(RuntimeAllowlist.IsAllowed("Python3"));
    }

    [Fact]
    public void Probe_arguments_are_version_only()
    {
        Assert.Equal(new[] { "--version" }, RuntimeAllowlist.ProbeArguments);
    }
}
