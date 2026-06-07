using ClaudeExplorer.Core.Recommendations;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Recommendations;

public class LanguageSignalDetectorTests
{
    [Fact]
    public void Detects_js_and_ts_from_marker_files_with_evidence()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/proj/package.json", "{}")
            .AddFile("/proj/tsconfig.json", "{}");

        var signals = new LanguageSignalDetector(fs).Detect("/proj");

        var ts = signals.Single(s => s.Value == "typescript");
        Assert.Equal(SignalKind.Language, ts.Kind);
        Assert.Equal("/proj/tsconfig.json", ts.Evidence[0].FilePath);
        Assert.Contains(signals, s => s.Value == "javascript");
    }

    [Fact]
    public void Detects_csharp_from_csproj_with_count()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/proj/src/App.csproj", "<Project/>")
            .AddFile("/proj/tests/Tests.csproj", "<Project/>");

        var cs = new LanguageSignalDetector(fs).Detect("/proj").Single();
        Assert.Equal("csharp", cs.Value);
        Assert.Equal(2, cs.Evidence[0].Count);
    }

    [Fact]
    public void Empty_project_yields_no_signals()
    {
        Assert.Empty(new LanguageSignalDetector(new InMemoryFileSystem()).Detect("/proj"));
    }
}
