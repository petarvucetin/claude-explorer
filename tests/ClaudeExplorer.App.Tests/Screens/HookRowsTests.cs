using System.Text.Json.Nodes;
using ClaudeExplorer.App.Screens.Hooks;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.App.Tests.Screens;

public class HookRowsTests
{
    private static EffectiveSetting HookSetting(string evt, ScopeKind scope, string file, string json)
    {
        var arr = JsonNode.Parse(json)!;
        var contrib = new SettingContribution(new SettingOrigin(scope, file, $"hooks.{evt}"), arr);
        return new EffectiveSetting($"hooks.{evt}", MergeStrategy.ArrayConcat, arr.DeepClone(), null, new[] { contrib }, false);
    }

    private static DependencyReport Report(params (string name, DependencyStatusKind kind)[] entries) =>
        new(entries.Select(e => new DependencyResult(
            new DependencyRef(e.name, e.name, new[] { "hook:x" }), new DependencyStatus(e.kind))).ToList());

    [Fact]
    public void Flattens_event_matcher_command_with_source_and_health()
    {
        var config = new EffectiveConfig(new[]
        {
            HookSetting("PreToolUse", ScopeKind.Project, "/repo/.claude/settings.json",
                """[ { "matcher": "Bash", "hooks": [ { "type": "command", "command": "python3 guard.py" } ] } ]"""),
        });

        var view = HookRowsMapper.Map(config, Report(("python3", DependencyStatusKind.Found)));

        var group = Assert.Single(view.Groups);
        Assert.Equal("PreToolUse", group.Event);
        var row = Assert.Single(group.Rows);
        Assert.Equal("Bash", row.Matcher);
        Assert.Equal("python3 guard.py", row.Command);
        Assert.Equal(ScopeKind.Project, row.Source);
        Assert.Equal(HookHealth.Ok, row.Health);
        Assert.Equal(1, view.Total);
        Assert.Equal(0, view.Missing);
    }

    [Fact]
    public void Templated_plugin_command_is_na_not_missing()
    {
        // superpowers' SessionStart hook: command is a ${CLAUDE_PLUGIN_ROOT} path → run-hook resolves
        // nowhere on PATH, but it is not "missing" in any meaningful sense.
        var config = new EffectiveConfig(new[]
        {
            HookSetting("SessionStart", ScopeKind.Plugin, "/home/.claude/plugins/cache/o/superpowers/5.1.0/hooks/hooks.json",
                """[ { "matcher": "startup", "hooks": [ { "type": "command", "command": "\"${CLAUDE_PLUGIN_ROOT}/hooks/run-hook.cmd\" session-start" } ] } ]"""),
        });

        var view = HookRowsMapper.Map(config, Report(("run-hook", DependencyStatusKind.Missing)));

        var row = Assert.Single(view.Groups.Single().Rows);
        Assert.Equal(ScopeKind.Plugin, row.Source);
        Assert.Equal(HookHealth.Na, row.Health);
        Assert.Equal(0, view.Missing);
    }

    [Fact]
    public void Missing_runtime_for_a_plain_command_counts_as_missing()
    {
        var config = new EffectiveConfig(new[]
        {
            HookSetting("PostToolUse", ScopeKind.User, "/home/.claude/settings.json",
                """[ { "hooks": [ { "type": "command", "command": "deno run check.ts" } ] } ]"""),
        });

        var view = HookRowsMapper.Map(config, Report(("deno", DependencyStatusKind.Missing)));

        var row = Assert.Single(view.Groups.Single().Rows);
        Assert.Equal("*", row.Matcher);                 // no matcher → "*"
        Assert.Equal(HookHealth.Missing, row.Health);
        Assert.Equal(1, view.Missing);
    }

    [Fact]
    public void Non_hook_settings_are_ignored()
    {
        var config = new EffectiveConfig(new[]
        {
            new EffectiveSetting("model", MergeStrategy.ScalarLastWins, JsonValue.Create("opus"),
                null, Array.Empty<SettingContribution>(), false),
        });

        var view = HookRowsMapper.Map(config, Report());
        Assert.Empty(view.Groups);
        Assert.Equal(0, view.Total);
    }
}
