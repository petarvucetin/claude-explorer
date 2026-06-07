using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Dependencies;

public class FakeProcessRunnerTests
{
    [Fact]
    public void Returns_canned_result_and_records_the_invocation()
    {
        var runner = new FakeProcessRunner().AddVersion("node", "v20.10.0");

        var result = runner.Run("node", new[] { "--version" });

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("v20.10.0", result.StdOut);
        var call = Assert.Single(runner.Invocations);
        Assert.Equal("node", call.Executable);
        Assert.Equal(new[] { "--version" }, call.Arguments);
    }

    [Fact]
    public void Unknown_executable_returns_a_failure_result()
    {
        var result = new FakeProcessRunner().Run("ghost", new[] { "--version" });
        Assert.False(result.Success);
        Assert.NotEqual(0, result.ExitCode);
    }
}
