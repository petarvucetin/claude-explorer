using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Merge;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Tests.Merge;

public class MergeEngineEdgeCaseTests
{
    private static ScopeSettings Scope(ScopeKind kind, string json)
        => new(kind, $"/{kind}.json", (JsonObject)JsonNode.Parse(json)!);

    // T1: env null value must not crash; null key omitted, non-null key resolved
    [Fact]
    public void Env_null_value_does_not_throw_and_null_key_is_omitted()
    {
        var engine = new MergeEngine();
        var result = engine.Compute(new[]
        {
            Scope(ScopeKind.User, """{ "env": { "FOO": null, "BAR": "1" } }"""),
        });

        Assert.Null(result.Find("env.FOO"));
        Assert.Equal("1", (string?)result.Find("env.BAR")!.Value);
    }

    // T3: three-scope scalar — Local wins, HasConflict true, 3 contributions
    [Fact]
    public void Three_scope_scalar_local_wins_with_conflict()
    {
        var engine = new MergeEngine();
        var result = engine.Compute(new[]
        {
            Scope(ScopeKind.User,    """{ "model": "opus" }"""),
            Scope(ScopeKind.Project, """{ "model": "sonnet" }"""),
            Scope(ScopeKind.Local,   """{ "model": "haiku" }"""),
        });

        var model = result.Find("model")!;
        Assert.Equal("haiku", (string?)model.Value);
        Assert.Equal(ScopeKind.Local, model.Winner!.Scope);
        Assert.True(model.HasConflict);
        Assert.Equal(3, model.Contributions.Count);
    }

    // T4a: hooks non-array in user, array in project → 1 contribution, 1 element
    [Fact]
    public void Hooks_non_array_in_one_scope_skipped_other_scope_counted()
    {
        var engine = new MergeEngine();
        var result = engine.Compute(new[]
        {
            Scope(ScopeKind.User,    """{ "hooks": { "PreToolUse": "nope" } }"""),
            Scope(ScopeKind.Project, """{ "hooks": { "PreToolUse": [ { "matcher": "Bash" } ] } }"""),
        });

        var hooks = result.Find("hooks.PreToolUse")!;
        Assert.NotNull(hooks);
        Assert.Single(hooks.Contributions);
        Assert.Single((JsonArray)hooks.Value!);
    }

    // T4b: hooks non-array in all scopes → phantom suppressed, result is null
    [Fact]
    public void Hooks_non_array_in_all_scopes_yields_null()
    {
        var engine = new MergeEngine();
        var result = engine.Compute(new[]
        {
            Scope(ScopeKind.User, """{ "hooks": { "PreToolUse": "nope" } }"""),
        });

        Assert.Null(result.Find("hooks.PreToolUse"));
    }

    // T2: wrong-type permissions (array instead of object) — path-children are null
    [Fact]
    public void Wrong_type_permissions_silently_omitted()
    {
        var engine = new MergeEngine();
        var result = engine.Compute(new[]
        {
            Scope(ScopeKind.User, """{ "permissions": [1, 2, 3] }"""),
        });

        Assert.Null(result.Find("permissions.allow"));
        Assert.Null(result.Find("permissions.defaultMode"));
    }
}
