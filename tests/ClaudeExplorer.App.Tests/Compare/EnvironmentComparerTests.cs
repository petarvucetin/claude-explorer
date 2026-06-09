using System.Text.Json.Nodes;
using ClaudeExplorer.App.Compare;
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.App.Tests.Compare;

public class EnvironmentComparerTests
{
    private static EffectiveSetting Setting(string key, JsonNode? value) =>
        new(key, MergeStrategy.ScalarLastWins, value, null, Array.Empty<SettingContribution>(), false);

    private static ResolvedArtifact Art(ArtifactKind kind, string name, string? summary, string path = "") =>
        new(new DiscoveredArtifact(kind, name, summary, new ArtifactSource(ArtifactSourceKind.User),
            string.IsNullOrEmpty(path) ? $"/{name}" : path),
            Array.Empty<DiscoveredArtifact>());

    private static EnvironmentSnapshot Snap(
        IReadOnlyList<EffectiveSetting>? settings = null,
        IReadOnlyList<ResolvedArtifact>? artifacts = null,
        IReadOnlyList<McpServer>? mcp = null,
        IReadOnlyList<string>? plugins = null,
        IReadOnlyList<DependencyResult>? deps = null,
        Dictionary<string, string>? memory = null) =>
        new(settings ?? Array.Empty<EffectiveSetting>(),
            new ArtifactCatalog(artifacts ?? Array.Empty<ResolvedArtifact>()),
            mcp ?? Array.Empty<McpServer>(),
            plugins ?? Array.Empty<string>(),
            new DependencyReport(deps ?? Array.Empty<DependencyResult>()),
            memory ?? new Dictionary<string, string>());

    private static CompareCategory Cat(EnvironmentComparison c, string name) => c.Categories.Single(x => x.Name == name);

    [Fact]
    public void Settings_classifies_same_differs_onlyA_onlyB()
    {
        var a = Snap(settings: new[]
        {
            Setting("model", JsonValue.Create("opus")),
            Setting("outputStyle", JsonValue.Create("concise")),
            Setting("statusLine", JsonValue.Create("ccusage")), // only A
        });
        var b = Snap(settings: new[]
        {
            Setting("model", JsonValue.Create("sonnet")),        // differs
            Setting("outputStyle", JsonValue.Create("concise")), // same
            Setting("env.DOCKER_HOST", JsonValue.Create("x")),   // only B
        });

        var cat = Cat(EnvironmentComparer.Compare(a, b), "Settings");

        Assert.Equal(DiffStatus.Differs, cat.Rows.Single(r => r.Key == "model").Status);
        Assert.Equal(DiffStatus.Same, cat.Rows.Single(r => r.Key == "outputStyle").Status);
        Assert.Equal(DiffStatus.OnlyA, cat.Rows.Single(r => r.Key == "statusLine").Status);
        Assert.Equal(DiffStatus.OnlyB, cat.Rows.Single(r => r.Key == "env.DOCKER_HOST").Status);
        Assert.Equal(1, cat.Same);
        Assert.Equal(1, cat.Differs);
        Assert.Equal(1, cat.OnlyA);
        Assert.Equal(1, cat.OnlyB);
    }

    [Fact]
    public void Settings_list_values_compare_as_sets_regardless_of_order()
    {
        var a = Snap(settings: new[] { Setting("permissions.allow", new JsonArray("git", "npm")) });
        var b = Snap(settings: new[] { Setting("permissions.allow", new JsonArray("npm", "git")) });

        Assert.Equal(DiffStatus.Same, Cat(EnvironmentComparer.Compare(a, b), "Settings")
            .Rows.Single(r => r.Key == "permissions.allow").Status);
    }

    [Fact]
    public void Commands_skills_agents_compare_by_name_and_summary()
    {
        var a = Snap(artifacts: new[]
        {
            Art(ArtifactKind.Command, "deploy", "v1"),
            Art(ArtifactKind.Skill, "lint", "same"),
        });
        var b = Snap(artifacts: new[]
        {
            Art(ArtifactKind.Command, "deploy", "v2"), // differs by summary
            Art(ArtifactKind.Skill, "lint", "same"),   // same
            Art(ArtifactKind.Subagent, "review", "x"), // only B (Agents)
        });

        var c = EnvironmentComparer.Compare(a, b);
        Assert.Equal(DiffStatus.Differs, Cat(c, "Commands").Rows.Single(r => r.Key == "deploy").Status);
        Assert.Equal(DiffStatus.Same, Cat(c, "Skills").Rows.Single(r => r.Key == "lint").Status);
        Assert.Equal(DiffStatus.OnlyB, Cat(c, "Subagents").Rows.Single(r => r.Key == "review").Status);
    }

    [Fact]
    public void Mcp_plugins_dependencies_categories_present_and_diffed()
    {
        var a = Snap(
            mcp: new[] { new McpServer("ctx7", "uvx", new[] { "ctx7" }, ScopeKind.User) },
            plugins: new[] { "linear" },
            deps: new[] { new DependencyResult(new DependencyRef("node", "node", Array.Empty<string>()), new DependencyStatus(DependencyStatusKind.Found)) });
        var b = Snap(
            mcp: new[] { new McpServer("ctx7", "npx", new[] { "ctx7" }, ScopeKind.User) }, // differs (command)
            plugins: new[] { "linear", "playwright" },                                      // playwright only B
            deps: new[] { new DependencyResult(new DependencyRef("node", "node", Array.Empty<string>()), new DependencyStatus(DependencyStatusKind.Missing)) }); // differs (status)

        var c = EnvironmentComparer.Compare(a, b);
        Assert.Equal(DiffStatus.Differs, Cat(c, "MCP").Rows.Single(r => r.Key == "ctx7").Status);
        Assert.Equal(DiffStatus.OnlyB, Cat(c, "Plugins").Rows.Single(r => r.Key == "playwright").Status);
        Assert.Equal(DiffStatus.Differs, Cat(c, "Dependencies").Rows.Single(r => r.Key == "node").Status);
    }

    [Fact]
    public void Produces_seven_categories()
    {
        var c = EnvironmentComparer.Compare(Snap(), Snap());
        Assert.Equal(new[] { "Settings", "Commands", "Skills", "Subagents", "MCP", "Hooks", "Memory", "Plugins", "Dependencies" },
            c.Categories.Select(x => x.Name).ToArray());
    }

    [Fact]
    public void Hooks_category_keys_groups_by_event_and_index()
    {
        var hooks = System.Text.Json.Nodes.JsonNode.Parse(
            """[ { "matcher":"Bash", "hooks":[ {"type":"command","command":"echo a"} ] } ]""");
        var a = Snap(settings: new[] { new EffectiveSetting("hooks.PreToolUse", MergeStrategy.ArrayConcat, hooks, null, Array.Empty<SettingContribution>(), false) });
        var b = Snap();

        var cat = EnvironmentComparer.Compare(a, b).Find("Hooks")!;
        Assert.Equal(DiffStatus.OnlyA, cat.Rows.Single(r => r.Key == "PreToolUse#0").Status);
    }

    [Fact]
    public void Memory_category_diffs_claude_md_by_content()
    {
        var a = Snap(memory: new() { ["CLAUDE.md"] = "rules v1", ["CLAUDE.local.md"] = "x" });
        var b = Snap(memory: new() { ["CLAUDE.md"] = "rules v2" });

        var cat = EnvironmentComparer.Compare(a, b).Find("Memory")!;

        Assert.Equal(DiffStatus.Differs, cat.Rows.Single(r => r.Key == "CLAUDE.md").Status);
        Assert.Equal(DiffStatus.OnlyA, cat.Rows.Single(r => r.Key == "CLAUDE.local.md").Status);
    }

    [Fact]
    public void Plugins_and_dependencies_categories_are_view_only()
    {
        var cmp = EnvironmentComparer.Compare(Snap(), Snap());
        Assert.True(cmp.Find("Plugins")!.ViewOnly);
        Assert.True(cmp.Find("Dependencies")!.ViewOnly);
        Assert.False(cmp.Find("Settings")!.ViewOnly);
    }

    [Fact]
    public void Command_row_carries_each_sides_resolved_file_path_and_content()
    {
        var a = Snap(artifacts: new[] { Art(ArtifactKind.Command, "deploy", "v1", "/a/.claude/commands/deploy.md") });
        var b = Snap(artifacts: new[] { Art(ArtifactKind.Command, "deploy", "v2", "/b/.claude/commands/deploy.md") });

        var row = EnvironmentComparer.Compare(a, b).Find("Commands")!.Rows.Single(r => r.Key == "deploy");

        Assert.Equal("/a/.claude/commands/deploy.md", row.PathA);
        Assert.Equal("/b/.claude/commands/deploy.md", row.PathB);
    }

    [Fact]
    public void OnlyA_command_row_has_null_path_on_the_B_side()
    {
        var a = Snap(artifacts: new[] { Art(ArtifactKind.Command, "deploy", "v1", "/a/.claude/commands/deploy.md") });
        var b = Snap();

        var row = EnvironmentComparer.Compare(a, b).Find("Commands")!.Rows.Single(r => r.Key == "deploy");

        Assert.Equal(DiffStatus.OnlyA, row.Status);
        Assert.Equal("/a/.claude/commands/deploy.md", row.PathA);
        Assert.Null(row.PathB);
    }
}
