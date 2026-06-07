using ClaudeExplorer.Core.Recommendations;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Recommendations;

public class SignalDetectionServiceTests
{
    [Fact]
    public void Framework_detector_finds_nextjs()
    {
        var fs = new InMemoryFileSystem().AddFile("/proj/next.config.js", "x");
        var sig = new FrameworkSignalDetector(fs).Detect("/proj").Single();
        Assert.Equal(SignalKind.Framework, sig.Kind);
        Assert.Equal("nextjs", sig.Value);
    }

    [Fact]
    public void Service_aggregates_all_detectors_into_project_signals()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/proj/package.json", "{}")
            .AddFile("/proj/next.config.js", "x")
            .AddFile("/proj/playwright.config.ts", "x")
            .AddFile("/proj/migrations/0001.sql", "x");

        var ps = new SignalDetectionService(fs).Detect("/proj");

        Assert.Contains(ps.Signals, s => s.Kind == SignalKind.Language && s.Value == "javascript");
        Assert.Contains(ps.Signals, s => s.Kind == SignalKind.Framework && s.Value == "nextjs");
        Assert.Contains(ps.Signals, s => s.Kind == SignalKind.TestRunner && s.Value == "playwright");
        Assert.Contains(ps.Signals, s => s.Kind == SignalKind.Database && s.Value == "sql");
    }

    [Fact]
    public void Service_accepts_a_custom_detector_set()
    {
        var ps = new SignalDetectionService(Array.Empty<ISignalDetector>()).Detect("/proj");
        Assert.Empty(ps.Signals);
    }
}
