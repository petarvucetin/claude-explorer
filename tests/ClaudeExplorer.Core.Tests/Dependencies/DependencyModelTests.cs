using ClaudeExplorer.Core.Dependencies;

namespace ClaudeExplorer.Core.Tests.Dependencies;

public class DependencyModelTests
{
    private static DependencyResult R(string name, DependencyStatusKind kind)
        => new(new DependencyRef(name, name, new[] { "hook:PreToolUse" }), new DependencyStatus(kind));

    [Fact]
    public void Report_counts_results_by_kind()
    {
        var report = new DependencyReport(new[]
        {
            R("node", DependencyStatusKind.Found),
            R("npx", DependencyStatusKind.Found),
            R("python3", DependencyStatusKind.Missing),
            R("mytool", DependencyStatusKind.Unverifiable),
        });

        Assert.Equal(2, report.Count(DependencyStatusKind.Found));
        Assert.Equal(1, report.Count(DependencyStatusKind.Missing));
        Assert.Equal(1, report.Count(DependencyStatusKind.Unverifiable));
    }

    [Fact]
    public void AllHealthy_is_false_when_anything_is_missing()
    {
        var healthy = new DependencyReport(new[] { R("node", DependencyStatusKind.Found), R("x", DependencyStatusKind.Unverifiable) });
        var broken = new DependencyReport(new[] { R("node", DependencyStatusKind.Found), R("python3", DependencyStatusKind.Missing) });

        Assert.True(healthy.AllHealthy);
        Assert.False(broken.AllHealthy);
    }

    [Fact]
    public void Status_carries_version_and_path()
    {
        var status = new DependencyStatus(DependencyStatusKind.Found, "v20.10.0", "/usr/bin/node");
        Assert.Equal("v20.10.0", status.Version);
        Assert.Equal("/usr/bin/node", status.Path);
    }
}
