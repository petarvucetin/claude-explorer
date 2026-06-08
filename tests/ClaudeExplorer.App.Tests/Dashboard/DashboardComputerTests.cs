using System.Text.Json.Nodes;
using ClaudeExplorer.App.Dashboard;
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.App.Tests.Dashboard;

public class DashboardComputerTests
{
    private static ResolvedArtifact Art(ArtifactKind kind, string name, ArtifactSourceKind src,
        string? plugin = null, bool shadowed = false)
    {
        var winner = new DiscoveredArtifact(kind, name, null, new ArtifactSource(src, plugin), $"/{name}");
        var shadow = shadowed
            ? new[] { new DiscoveredArtifact(kind, name, null, new ArtifactSource(ArtifactSourceKind.User), $"/u/{name}") }
            : Array.Empty<DiscoveredArtifact>();
        return new ResolvedArtifact(winner, shadow);
    }

    private static EffectiveSetting Setting(string key, bool conflict) =>
        new(key, MergeStrategy.ScalarLastWins, JsonValue.Create("x"), null,
            Array.Empty<SettingContribution>(), conflict);

    private static DependencyResult Dep(string name, DependencyStatusKind kind, params string[] referencedBy) =>
        new(new DependencyRef(name, name, referencedBy), new DependencyStatus(kind));

    private static DashboardInputs Inputs(
        IReadOnlyList<ResolvedArtifact>? artifacts = null,
        IReadOnlyList<EffectiveSetting>? settings = null,
        IReadOnlyList<DependencyResult>? deps = null,
        IReadOnlyList<McpServer>? mcp = null,
        IReadOnlyList<ChangeLogEntry>? changes = null) =>
        new(new EffectiveConfig(settings ?? Array.Empty<EffectiveSetting>()),
            new ArtifactCatalog(artifacts ?? Array.Empty<ResolvedArtifact>()),
            new DependencyReport(deps ?? Array.Empty<DependencyResult>()),
            mcp ?? Array.Empty<McpServer>(),
            changes ?? Array.Empty<ChangeLogEntry>(),
            "webapp");

    [Fact]
    public void Counts_commands_and_skills_with_source_breakdown()
    {
        var data = DashboardComputer.Compute(Inputs(artifacts: new[]
        {
            Art(ArtifactKind.Command, "a", ArtifactSourceKind.User),
            Art(ArtifactKind.Command, "b", ArtifactSourceKind.Project),
            Art(ArtifactKind.Skill, "s", ArtifactSourceKind.Plugin, "acme"),
            Art(ArtifactKind.Subagent, "g", ArtifactSourceKind.Plugin, "acme"),
        }));

        var commands = data.Stats.Single(s => s.Label == "Commands");
        Assert.Equal(2, commands.Value);
        Assert.Equal("01", commands.Index);
        Assert.Contains("1 user", commands.Sub);
        Assert.Contains("1 project", commands.Sub);

        var skills = data.Stats.Single(s => s.Label == "Skills+Agents");
        Assert.Equal(2, skills.Value);
        Assert.Contains("1 plugin", skills.Sub); // 1 distinct plugin (acme)
    }

    [Fact]
    public void Mcp_down_counts_servers_with_a_missing_dependency()
    {
        var data = DashboardComputer.Compute(Inputs(
            mcp: new[]
            {
                new McpServer("context7", "uvx", new[] { "context7" }, ScopeKind.Project),
                new McpServer("ok", "node", Array.Empty<string>(), ScopeKind.User),
            },
            deps: new[]
            {
                Dep("uvx", DependencyStatusKind.Missing, "mcp:context7"),
                Dep("node", DependencyStatusKind.Found, "mcp:ok"),
            }));

        var mcp = data.Stats.Single(s => s.Label == "MCP Servers");
        Assert.Equal(2, mcp.Value);
        Assert.Equal("1 down", mcp.Badge);
        Assert.Equal(BadgeTone.Bad, mcp.Tone);
        Assert.Contains("1 reachable", mcp.Sub);
    }

    [Fact]
    public void Dependency_card_flags_missing_count()
    {
        var data = DashboardComputer.Compute(Inputs(deps: new[]
        {
            Dep("node", DependencyStatusKind.Found),
            Dep("uvx", DependencyStatusKind.Missing, "mcp:x"),
        }));

        var dep = data.Stats.Single(s => s.Label == "Dependencies");
        Assert.Equal(2, dep.Value);
        Assert.Equal("1 miss", dep.Badge);
        Assert.Equal(BadgeTone.Warn, dep.Tone);
    }

    [Fact]
    public void Conflicts_and_warnings_are_counted()
    {
        var data = DashboardComputer.Compute(Inputs(
            settings: new[] { Setting("model", true), Setting("outputStyle", false) },
            artifacts: new[] { Art(ArtifactKind.Command, "dup", ArtifactSourceKind.Project, shadowed: true) },
            deps: new[] { Dep("docker", DependencyStatusKind.Unverifiable) }));

        Assert.Equal(1, data.Stats.Single(s => s.Label == "Conflicts").Value);
        // warnings = 1 shadowed artifact + 1 unverifiable dep
        Assert.Equal(2, data.Stats.Single(s => s.Label == "Warnings").Value);
    }

    [Fact]
    public void Health_subtracts_for_missing_conflicts_and_down_servers()
    {
        var data = DashboardComputer.Compute(Inputs(
            settings: new[] { Setting("model", true) },                       // -3
            mcp: new[] { new McpServer("c", "uvx", new[] { "c" }, ScopeKind.Project) },
            deps: new[] { Dep("uvx", DependencyStatusKind.Missing, "mcp:c") })); // -8 missing, -8 down

        Assert.Equal(100 - 8 - 8 - 3, data.Health);
    }

    [Fact]
    public void Health_is_clamped_to_zero()
    {
        var deps = Enumerable.Range(0, 20).Select(i => Dep($"d{i}", DependencyStatusKind.Missing)).ToArray();
        Assert.Equal(0, DashboardComputer.Compute(Inputs(deps: deps)).Health);
    }

    [Fact]
    public void Attention_lists_missing_then_conflict_then_unverifiable()
    {
        var data = DashboardComputer.Compute(Inputs(
            settings: new[] { Setting("model", true) },
            deps: new[]
            {
                Dep("uvx", DependencyStatusKind.Missing, "mcp:context7"),
                Dep("docker", DependencyStatusKind.Unverifiable),
            }));

        Assert.Collection(data.Attention,
            a => { Assert.Equal(AttentionTone.Bad, a.Tone); Assert.Contains("uvx", a.Title); },
            a => { Assert.Equal(AttentionTone.Warn, a.Tone); Assert.Contains("model", a.Title); },
            a => { Assert.Equal(AttentionTone.Info, a.Tone); Assert.Contains("docker", a.Title); });
    }

    [Fact]
    public void Recent_changes_are_newest_first_capped_at_five()
    {
        var changes = Enumerable.Range(1, 7).Select(i => new ChangeLogEntry(
            $"chg-{i}", "2026-06-07", ChangeKind.Edit, ScopeKind.Project, "/p", $"edit {i}",
            null, null, false)).ToList();

        var data = DashboardComputer.Compute(Inputs(changes: changes));

        Assert.Equal(5, data.RecentChanges.Count);
        Assert.Equal("edit 7", data.RecentChanges[0].Title);
        Assert.Equal("edit 3", data.RecentChanges[4].Title);
    }

    [Fact]
    public void Empty_inputs_give_full_health_and_no_rows()
    {
        var data = DashboardComputer.Compute(Inputs());
        Assert.Equal(100, data.Health);
        Assert.Empty(data.Attention);
        Assert.Empty(data.RecentChanges);
        Assert.Equal("webapp", data.EffectiveForLabel);
        Assert.Equal(6, data.Stats.Count);
    }
}
