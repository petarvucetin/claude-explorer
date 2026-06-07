using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Dependencies;

public class DependencyCheckerTests
{
    private static DependencyRef Ref(string name) => new(name, name, new[] { "hook:PreToolUse" });

    [Fact]
    public void Missing_when_not_on_path_and_runner_is_never_called()
    {
        var resolver = new FakePathResolver(); // nothing on PATH
        var runner = new FakeProcessRunner();

        var report = new DependencyChecker(resolver, runner).Check(new[] { Ref("node") });

        var result = Assert.Single(report.Results);
        Assert.Equal(DependencyStatusKind.Missing, result.Status.Kind);
        Assert.Null(result.Status.Path);
        Assert.Empty(runner.Invocations);
    }

    [Fact]
    public void Found_when_allowlisted_and_on_path_with_version_from_probe()
    {
        var resolver = new FakePathResolver().Add("node", "/usr/bin/node");
        var runner = new FakeProcessRunner().AddVersion("/usr/bin/node", "v20.10.0");

        var report = new DependencyChecker(resolver, runner).Check(new[] { Ref("node") });

        var result = Assert.Single(report.Results);
        Assert.Equal(DependencyStatusKind.Found, result.Status.Kind);
        Assert.Equal("v20.10.0", result.Status.Version);
        Assert.Equal("/usr/bin/node", result.Status.Path);
        var call = Assert.Single(runner.Invocations);
        Assert.Equal("/usr/bin/node", call.Executable); // probed by resolved path, not bare name
        Assert.Equal(new[] { "--version" }, call.Arguments);
    }

    [Fact]
    public void Unverifiable_when_present_but_not_allowlisted_and_runner_is_never_called()
    {
        var resolver = new FakePathResolver().Add("my-tool", "/opt/bin/my-tool");
        var runner = new FakeProcessRunner();

        var report = new DependencyChecker(resolver, runner).Check(new[] { Ref("my-tool") });

        var result = Assert.Single(report.Results);
        Assert.Equal(DependencyStatusKind.Unverifiable, result.Status.Kind);
        Assert.Equal("/opt/bin/my-tool", result.Status.Path);
        Assert.Empty(runner.Invocations); // SAFETY: arbitrary binaries are never executed
    }

    [Fact]
    public void Version_falls_back_to_stderr_and_uses_first_nonempty_line()
    {
        var resolver = new FakePathResolver().Add("python3", "/usr/bin/python3");
        var runner = new FakeProcessRunner().AddResult("/usr/bin/python3", new ProcessResult(0, "", "\nPython 3.11.5\n"));

        var report = new DependencyChecker(resolver, runner).Check(new[] { Ref("python3") });

        Assert.Equal("Python 3.11.5", Assert.Single(report.Results).Status.Version);
    }
}
