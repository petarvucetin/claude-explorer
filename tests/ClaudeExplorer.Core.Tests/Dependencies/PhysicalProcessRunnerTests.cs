using ClaudeExplorer.Core.Dependencies;

namespace ClaudeExplorer.Core.Tests.Dependencies;

public class PhysicalProcessRunnerTests
{
    [Fact]
    public void Run_returns_failure_instead_of_throwing_when_executable_cannot_start()
    {
        // A path that cannot be launched on any OS (deterministic). Before the fix this threw a
        // Win32Exception that propagated up and crashed the dashboard at startup.
        var runner = new PhysicalProcessRunner(timeoutMs: 1000);

        var ex = Record.Exception(() =>
        {
            var result = runner.Run("Z:/no/such/totally-bogus-executable-xyz", Array.Empty<string>());
            Assert.False(result.Success);
        });

        Assert.Null(ex);
    }
}
