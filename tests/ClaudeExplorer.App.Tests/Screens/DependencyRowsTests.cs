using ClaudeExplorer.App.Screens.Dependencies;
using ClaudeExplorer.Core.Dependencies;

namespace ClaudeExplorer.App.Tests.Screens;

public class DependencyRowsTests
{
    private static DependencyResult Dep(string name, DependencyStatusKind kind,
        string? version = null, string? path = null, params string[] referencedBy)
        => new(new DependencyRef(name, name, referencedBy),
               new DependencyStatus(kind, version, path));

    [Fact]
    public void Tone_mapping_per_kind()
    {
        Assert.Equal(DepTone.Ok, DependencyRowsMapper.Tone(DependencyStatusKind.Found));
        Assert.Equal(DepTone.Bad, DependencyRowsMapper.Tone(DependencyStatusKind.Missing));
        Assert.Equal(DepTone.Warn, DependencyRowsMapper.Tone(DependencyStatusKind.Unverifiable));
    }

    [Fact]
    public void Counts_are_correct()
    {
        var report = new DependencyReport(new[]
        {
            Dep("node",   DependencyStatusKind.Found,         version: "20.0"),
            Dep("uvx",    DependencyStatusKind.Missing),
            Dep("docker", DependencyStatusKind.Unverifiable),
        });

        var view = DependencyRowsMapper.Map(report);

        Assert.Equal(1, view.Found);
        Assert.Equal(1, view.Missing);
        Assert.Equal(1, view.Unverifiable);
        Assert.Equal(3, view.Rows.Count);
    }

    [Fact]
    public void Referenced_by_joined_with_comma()
    {
        var report = new DependencyReport(new[]
        {
            Dep("uvx", DependencyStatusKind.Missing, null, null, "mcp:context7", "hook:PostToolUse"),
        });

        var view = DependencyRowsMapper.Map(report);
        var row = Assert.Single(view.Rows);

        Assert.Contains("mcp:context7", row.ReferencedBy);
        Assert.Contains("hook:PostToolUse", row.ReferencedBy);
        Assert.Contains(",", row.ReferencedBy);
    }

    [Fact]
    public void Empty_referenced_by_is_empty_string()
    {
        var report = new DependencyReport(new[]
        {
            Dep("node", DependencyStatusKind.Found),
        });

        var view = DependencyRowsMapper.Map(report);
        Assert.Equal("", view.Rows[0].ReferencedBy);
    }

    [Fact]
    public void Version_and_path_passed_through()
    {
        var report = new DependencyReport(new[]
        {
            Dep("node", DependencyStatusKind.Found, version: "20.11.1", path: "/usr/bin/node"),
        });

        var view = DependencyRowsMapper.Map(report);
        var row = Assert.Single(view.Rows);

        Assert.Equal("20.11.1", row.Version);
        Assert.Equal("/usr/bin/node", row.Path);
    }

    [Fact]
    public void Empty_report_returns_empty_view()
    {
        var view = DependencyRowsMapper.Map(new DependencyReport(Array.Empty<DependencyResult>()));
        Assert.Empty(view.Rows);
        Assert.Equal(0, view.Found);
        Assert.Equal(0, view.Missing);
        Assert.Equal(0, view.Unverifiable);
    }
}
