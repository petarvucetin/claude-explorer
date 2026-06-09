using System.Text.Json.Nodes;
using ClaudeExplorer.App.Environments;
using ClaudeExplorer.App.Screens.EnvironmentSettings;
using ClaudeExplorer.Core.Model;
using CoreEffectiveConfig = ClaudeExplorer.Core.Model.EffectiveConfig;

namespace ClaudeExplorer.App.Tests.Screens;

public class EnvironmentSettingsRowsTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static EnvironmentIdentity DefaultId()
        => new("Windows", EnvironmentKind.Windows, "C:/Users/u/.claude", "my-project");

    private static SettingContribution Contrib(ScopeKind scope, string filePath, JsonNode? value)
        => new(new SettingOrigin(scope, filePath, ""), value);

    private static EffectiveSetting Scalar(string key, ScopeKind scope, string value)
        => new(key,
               MergeStrategy.ScalarLastWins,
               JsonValue.Create(value),
               new SettingOrigin(scope, "/u/settings.json", key),
               new[] { Contrib(scope, "/u/settings.json", JsonValue.Create(value)) },
               HasConflict: false);

    private static EffectiveSetting ListSetting(string key, ScopeKind scope, params string[] items)
    {
        var arr = new JsonArray(items.Select(i => (JsonNode)JsonValue.Create(i)!).ToArray());
        return new(key,
                   MergeStrategy.ListUnion,
                   arr,
                   new SettingOrigin(scope, "/u/settings.json", key),
                   new[] { Contrib(scope, "/u/settings.json", arr) },
                   HasConflict: false);
    }

    // ── scalar extraction ────────────────────────────────────────────────────

    [Fact]
    public void Model_extracted_with_display_and_scope()
    {
        var cfg = new CoreEffectiveConfig(new[]
        {
            Scalar("model", ScopeKind.User, "claude-opus-4-5"),
        });

        var view = EnvironmentSettingsMapper.Map(cfg, DefaultId());

        Assert.NotNull(view.Model);
        Assert.Equal("claude-opus-4-5", view.Model!.Display);
        Assert.Equal("user", view.Model.ScopeCss);
    }

    [Fact]
    public void OutputStyle_extracted_with_project_scope()
    {
        var cfg = new CoreEffectiveConfig(new[]
        {
            Scalar("outputStyle", ScopeKind.Project, "json"),
        });

        var view = EnvironmentSettingsMapper.Map(cfg, DefaultId());

        Assert.NotNull(view.OutputStyle);
        Assert.Equal("json", view.OutputStyle!.Display);
        Assert.Equal("project", view.OutputStyle.ScopeCss);
    }

    [Fact]
    public void DefaultMode_extracted_with_local_scope()
    {
        var cfg = new CoreEffectiveConfig(new[]
        {
            Scalar("permissions.defaultMode", ScopeKind.Local, "acceptEdits"),
        });

        var view = EnvironmentSettingsMapper.Map(cfg, DefaultId());

        Assert.NotNull(view.DefaultMode);
        Assert.Equal("acceptEdits", view.DefaultMode!.Display);
        Assert.Equal("local", view.DefaultMode.ScopeCss);
    }

    [Fact]
    public void Missing_model_yields_null_Model()
    {
        var cfg = new CoreEffectiveConfig(Array.Empty<EffectiveSetting>());

        var view = EnvironmentSettingsMapper.Map(cfg, DefaultId());

        Assert.Null(view.Model);
    }

    [Fact]
    public void Enterprise_scope_css_is_enterprise()
    {
        var cfg = new CoreEffectiveConfig(new[]
        {
            Scalar("model", ScopeKind.Enterprise, "claude-sonnet-4-5"),
        });

        var view = EnvironmentSettingsMapper.Map(cfg, DefaultId());

        Assert.Equal("enterprise", view.Model!.ScopeCss);
    }

    [Fact]
    public void Plugin_scope_css_is_plugin()
    {
        var cfg = new CoreEffectiveConfig(new[]
        {
            Scalar("outputStyle", ScopeKind.Plugin, "raw"),
        });

        var view = EnvironmentSettingsMapper.Map(cfg, DefaultId());

        Assert.Equal("plugin", view.OutputStyle!.ScopeCss);
    }

    // ── no winner → empty ScopeCss ───────────────────────────────────────────

    [Fact]
    public void No_winner_gives_empty_scope_css()
    {
        var setting = new EffectiveSetting(
            "model",
            MergeStrategy.ScalarLastWins,
            JsonValue.Create("some-model"),
            null,   // no winner
            Array.Empty<SettingContribution>(),
            HasConflict: false);

        var cfg = new CoreEffectiveConfig(new[] { setting });
        var view = EnvironmentSettingsMapper.Map(cfg, DefaultId());

        Assert.Equal("", view.Model!.ScopeCss);
    }

    // ── permissions lists ────────────────────────────────────────────────────

    [Fact]
    public void Allow_list_extracted_correctly()
    {
        var cfg = new CoreEffectiveConfig(new[]
        {
            ListSetting("permissions.allow", ScopeKind.User, "Bash(*)", "Read(*)", "Edit(*)"),
        });

        var view = EnvironmentSettingsMapper.Map(cfg, DefaultId());

        Assert.Equal(3, view.Allow.Count);
        Assert.Contains("Bash(*)", view.Allow);
        Assert.Contains("Read(*)", view.Allow);
        Assert.Contains("Edit(*)", view.Allow);
    }

    [Fact]
    public void Deny_list_extracted_correctly()
    {
        var cfg = new CoreEffectiveConfig(new[]
        {
            ListSetting("permissions.deny", ScopeKind.Project, "WebFetch(*)", "WebSearch(*)"),
        });

        var view = EnvironmentSettingsMapper.Map(cfg, DefaultId());

        Assert.Equal(2, view.Deny.Count);
        Assert.Contains("WebFetch(*)", view.Deny);
    }

    [Fact]
    public void Ask_list_extracted_correctly()
    {
        var cfg = new CoreEffectiveConfig(new[]
        {
            ListSetting("permissions.ask", ScopeKind.User, "Bash(git push:*)"),
        });

        var view = EnvironmentSettingsMapper.Map(cfg, DefaultId());

        Assert.Single(view.Ask);
        Assert.Equal("Bash(git push:*)", view.Ask[0]);
    }

    [Fact]
    public void Missing_permissions_yield_empty_lists()
    {
        var cfg = new CoreEffectiveConfig(Array.Empty<EffectiveSetting>());

        var view = EnvironmentSettingsMapper.Map(cfg, DefaultId());

        Assert.Empty(view.Allow);
        Assert.Empty(view.Deny);
        Assert.Empty(view.Ask);
    }

    // ── env vars ─────────────────────────────────────────────────────────────

    [Fact]
    public void EnvVars_prefix_stripped_and_sorted_by_name()
    {
        var cfg = new CoreEffectiveConfig(new[]
        {
            Scalar("env.ZEBRA",       ScopeKind.User,  "1"),
            Scalar("env.ANTHROPIC_LOG", ScopeKind.User, "info"),
            Scalar("env.DISABLE_TELEMETRY", ScopeKind.Local, "1"),
        });

        var view = EnvironmentSettingsMapper.Map(cfg, DefaultId());

        Assert.Equal(3, view.EnvVars.Count);
        // ordinal sort
        Assert.Equal("ANTHROPIC_LOG",     view.EnvVars[0].Name);
        Assert.Equal("DISABLE_TELEMETRY", view.EnvVars[1].Name);
        Assert.Equal("ZEBRA",             view.EnvVars[2].Name);
        Assert.Equal("info", view.EnvVars[0].Value);
        Assert.Equal("user", view.EnvVars[0].ScopeCss);
        Assert.Equal("local", view.EnvVars[1].ScopeCss);
    }

    [Fact]
    public void EnvVars_empty_when_no_env_settings()
    {
        var cfg = new CoreEffectiveConfig(Array.Empty<EffectiveSetting>());
        var view = EnvironmentSettingsMapper.Map(cfg, DefaultId());
        Assert.Empty(view.EnvVars);
    }

    // ── status line ──────────────────────────────────────────────────────────

    [Fact]
    public void StatusLine_prefix_stripped_and_sorted()
    {
        var cfg = new CoreEffectiveConfig(new[]
        {
            Scalar("statusLine.type",    ScopeKind.User, "command"),
            Scalar("statusLine.command", ScopeKind.User, "~/.claude/statusline.sh"),
        });

        var view = EnvironmentSettingsMapper.Map(cfg, DefaultId());

        Assert.Equal(2, view.StatusLine.Count);
        Assert.Equal("command", view.StatusLine[0].Key);  // "command" < "type" ordinal
        Assert.Equal("~/.claude/statusline.sh", view.StatusLine[0].Value);
        Assert.Equal("type", view.StatusLine[1].Key);
        Assert.Equal("user", view.StatusLine[0].ScopeCss);
    }

    // ── hooks ────────────────────────────────────────────────────────────────

    [Fact]
    public void Hooks_grouped_by_event_with_matcher_counts()
    {
        var preArr = new JsonArray(
            JsonNode.Parse("""{"matcher":"Bash","hooks":[]}"""),
            JsonNode.Parse("""{"matcher":"Edit","hooks":[]}"""));
        var postArr = new JsonArray(
            JsonNode.Parse("""{"matcher":"*","hooks":[]}"""));

        var cfg = new CoreEffectiveConfig(new[]
        {
            new EffectiveSetting("hooks.PreToolUse",  MergeStrategy.ArrayConcat, preArr,
                new SettingOrigin(ScopeKind.User, "/u/settings.json", "hooks.PreToolUse"),
                Array.Empty<SettingContribution>(), HasConflict: false),
            new EffectiveSetting("hooks.PostToolUse", MergeStrategy.ArrayConcat, postArr,
                new SettingOrigin(ScopeKind.User, "/u/settings.json", "hooks.PostToolUse"),
                Array.Empty<SettingContribution>(), HasConflict: false),
        });

        var view = EnvironmentSettingsMapper.Map(cfg, DefaultId());

        Assert.Equal(2, view.Hooks.Count);
        // ordinal sort: PostToolUse < PreToolUse
        Assert.Equal("PostToolUse", view.Hooks[0].Event);
        Assert.Equal(1, view.Hooks[0].MatcherCount);
        Assert.Equal("PreToolUse", view.Hooks[1].Event);
        Assert.Equal(2, view.Hooks[1].MatcherCount);
    }

    [Fact]
    public void Hook_with_non_array_value_counts_zero_matchers()
    {
        var setting = new EffectiveSetting(
            "hooks.SessionStart",
            MergeStrategy.ScalarLastWins,
            JsonValue.Create("unexpected"),
            new SettingOrigin(ScopeKind.User, "/u/settings.json", "hooks.SessionStart"),
            Array.Empty<SettingContribution>(),
            HasConflict: false);

        var cfg = new CoreEffectiveConfig(new[] { setting });
        var view = EnvironmentSettingsMapper.Map(cfg, DefaultId());

        Assert.Single(view.Hooks);
        Assert.Equal(0, view.Hooks[0].MatcherCount);
    }

    // ── empty config ─────────────────────────────────────────────────────────

    [Fact]
    public void Empty_config_yields_all_empty_view_with_null_scalars()
    {
        var cfg = new CoreEffectiveConfig(Array.Empty<EffectiveSetting>());

        var view = EnvironmentSettingsMapper.Map(cfg, DefaultId());

        Assert.Null(view.Model);
        Assert.Null(view.OutputStyle);
        Assert.Null(view.DefaultMode);
        Assert.Empty(view.Allow);
        Assert.Empty(view.Deny);
        Assert.Empty(view.Ask);
        Assert.Empty(view.EnvVars);
        Assert.Empty(view.StatusLine);
        Assert.Empty(view.Hooks);
    }

    // ── identity pass-through ─────────────────────────────────────────────────

    [Fact]
    public void Identity_is_passed_through_unchanged()
    {
        var id = new EnvironmentIdentity("My Env", EnvironmentKind.Custom, "/custom/dir", "my-label");
        var cfg = new CoreEffectiveConfig(Array.Empty<EffectiveSetting>());

        var view = EnvironmentSettingsMapper.Map(cfg, id);

        Assert.Equal("My Env",       view.Identity.Name);
        Assert.Equal(EnvironmentKind.Custom, view.Identity.Kind);
        Assert.Equal("/custom/dir",  view.Identity.UserDir);
        Assert.Equal("my-label",     view.Identity.ProjectLabel);
    }
}
