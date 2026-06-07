using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Merge;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Tests.Merge;

public class MergeEngineEnvHooksTests
{
    private static ScopeSettings Scope(ScopeKind kind, string json)
        => new(kind, $"/{kind}.json", (JsonObject)JsonNode.Parse(json)!);

    [Fact]
    public void Env_keys_are_expanded_to_scalar_settings()
    {
        var engine = new MergeEngine();
        var result = engine.Compute(new[]
        {
            Scope(ScopeKind.User, """{ "env": { "DISABLE_TELEMETRY": "1" } }"""),
            Scope(ScopeKind.Project, """{ "env": { "ANTHROPIC_LOG": "debug", "DISABLE_TELEMETRY": "0" } }"""),
        });

        Assert.Equal("debug", (string?)result.Find("env.ANTHROPIC_LOG")!.Value);
        var telem = result.Find("env.DISABLE_TELEMETRY")!;
        Assert.Equal("0", (string?)telem.Value);          // project beats user
        Assert.True(telem.HasConflict);                    // "1" vs "0"
    }

    [Fact]
    public void Hooks_are_concatenated_per_event()
    {
        var engine = new MergeEngine();
        var result = engine.Compute(new[]
        {
            Scope(ScopeKind.User, """{ "hooks": { "PreToolUse": [ { "matcher": "Bash" } ] } }"""),
            Scope(ScopeKind.Project, """{ "hooks": { "PreToolUse": [ { "matcher": "Read" }, { "matcher": "Edit" } ] } }"""),
        });

        var hooks = result.Find("hooks.PreToolUse")!;
        Assert.Equal(MergeStrategy.ArrayConcat, hooks.Strategy);
        Assert.Equal(3, ((JsonArray)hooks.Value!).Count);  // 1 + 2 combined
        Assert.Equal(2, hooks.Contributions.Count);
    }
}
