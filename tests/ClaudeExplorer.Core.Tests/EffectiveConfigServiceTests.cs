using System.Text.Json.Nodes;
using ClaudeExplorer.Core;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests;

public class EffectiveConfigServiceTests
{
    private static InMemoryFileSystem Workspace()
        => new InMemoryFileSystem()
            .AddFile("/home/me/.claude/settings.json", """
            {
              "model": "opus",
              "permissions": { "defaultMode": "acceptEdits", "allow": ["Bash(git*)"] },
              "env": { "DISABLE_TELEMETRY": "1" },
              "statusLine": { "command": "~/bin/ccline" }
            }
            """)
            .AddFile("/repo/.claude/settings.json", """
            {
              "model": "sonnet",
              "permissions": { "allow": ["Read(src/**)"], "deny": ["Bash(rm -rf*)"] },
              "env": { "ANTHROPIC_LOG": "debug" },
              "outputStyle": "concise",
              "hooks": { "PreToolUse": [ { "matcher": "Bash" } ] }
            }
            """)
            .AddFile("/repo/.claude/settings.local.json", """
            { "permissions": { "allow": ["Bash(npm*)"] } }
            """);

    [Fact]
    public void Computes_effective_config_with_correct_precedence_and_merges()
    {
        var service = new EffectiveConfigService(Workspace());

        var cfg = service.Compute(userDir: "/home/me", projectDir: "/repo");

        // scalar precedence: project overrides user, flagged as conflict
        var model = cfg.Find("model")!;
        Assert.Equal("sonnet", (string?)model.Value);
        Assert.Equal(ScopeKind.Project, model.Winner!.Scope);
        Assert.True(model.HasConflict);

        // user-only scalar
        Assert.Equal("acceptEdits", (string?)cfg.Find("permissions.defaultMode")!.Value);

        // project-only scalar
        Assert.Equal("concise", (string?)cfg.Find("outputStyle")!.Value);

        // list union across all three scopes
        var allow = ((JsonArray)cfg.Find("permissions.allow")!.Value!).Select(n => (string?)n).ToArray();
        Assert.Equal(new[] { "Bash(git*)", "Read(src/**)", "Bash(npm*)" }, allow);

        // deny present from project only
        Assert.Single((JsonArray)cfg.Find("permissions.deny")!.Value!);

        // env expanded
        Assert.Equal("1", (string?)cfg.Find("env.DISABLE_TELEMETRY")!.Value);
        Assert.Equal("debug", (string?)cfg.Find("env.ANTHROPIC_LOG")!.Value);

        // hooks concat (only project contributes here)
        Assert.Single((JsonArray)cfg.Find("hooks.PreToolUse")!.Value!);

        // provenance: every contribution points at a real file path
        Assert.All(cfg.Settings.SelectMany(s => s.Contributions),
            c => Assert.False(string.IsNullOrWhiteSpace(c.Origin.FilePath)));
    }

    [Fact]
    public void Empty_workspace_yields_empty_config()
    {
        var service = new EffectiveConfigService(new InMemoryFileSystem());
        var cfg = service.Compute("/home/me", "/repo");
        Assert.Empty(cfg.Settings);
    }

    [Fact]
    public void Folds_in_plugin_hooks_as_a_plugin_scoped_layer()
    {
        // A plugin ships a SessionStart hook; the user has no hooks at all.
        var fs = new InMemoryFileSystem()
            .AddFile("/home/me/.claude/settings.json", """{ "model": "opus" }""")
            .AddFile("/home/me/.claude/plugins/cache/official/superpowers/5.1.0/hooks/hooks.json", """
            {
              "hooks": {
                "SessionStart": [
                  { "matcher": "startup", "hooks": [ { "type": "command", "command": "run-hook session-start" } ] }
                ]
              }
            }
            """);

        var cfg = new EffectiveConfigService(fs).Compute(userDir: "/home/me", projectDir: "");

        var hook = cfg.Find("hooks.SessionStart");
        Assert.NotNull(hook);
        var contribution = Assert.Single(hook!.Contributions);
        Assert.Equal(ScopeKind.Plugin, contribution.Origin.Scope);
        Assert.Equal("/home/me/.claude/plugins/cache/official/superpowers/5.1.0/hooks/hooks.json",
            contribution.Origin.FilePath);
        Assert.Single((JsonArray)hook.Value!);
    }

    [Fact]
    public void Plugin_hooks_concat_after_settings_hooks_of_the_same_event()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/me/.claude/settings.json", """
            { "hooks": { "PreToolUse": [ { "matcher": "Bash" } ] } }
            """)
            .AddFile("/home/me/.claude/plugins/cache/official/p/1.0.0/hooks/hooks.json", """
            { "hooks": { "PreToolUse": [ { "matcher": "Edit" } ] } }
            """);

        var cfg = new EffectiveConfigService(fs).Compute(userDir: "/home/me", projectDir: "");

        var hook = cfg.Find("hooks.PreToolUse")!;
        Assert.Equal(2, ((JsonArray)hook.Value!).Count);            // both contributions concatenated
        Assert.Equal(2, hook.Contributions.Count);
        Assert.Contains(hook.Contributions, c => c.Origin.Scope == ScopeKind.Plugin);
        Assert.Contains(hook.Contributions, c => c.Origin.Scope == ScopeKind.User);
    }
}
