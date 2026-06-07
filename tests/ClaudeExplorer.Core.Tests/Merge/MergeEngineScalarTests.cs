using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Merge;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Tests.Merge;

public class MergeEngineScalarTests
{
    private static ScopeSettings Scope(ScopeKind kind, string json)
        => new(kind, $"/{kind}.json", (JsonObject)JsonNode.Parse(json)!);

    [Fact]
    public void Higher_precedence_scope_wins_and_conflict_is_flagged()
    {
        var engine = new MergeEngine();
        var result = engine.Compute(new[]
        {
            Scope(ScopeKind.User, """{ "model": "opus" }"""),
            Scope(ScopeKind.Project, """{ "model": "sonnet" }"""),
        });

        var model = result.Find("model")!;
        Assert.Equal("sonnet", (string?)model.Value);              // project (1) beats user (0)
        Assert.Equal(ScopeKind.Project, model.Winner!.Scope);
        Assert.True(model.HasConflict);                            // two differing values
        Assert.Equal(2, model.Contributions.Count);
    }

    [Fact]
    public void Single_contribution_is_not_a_conflict()
    {
        var engine = new MergeEngine();
        var result = engine.Compute(new[] { Scope(ScopeKind.User, """{ "model": "opus" }""") });

        var model = result.Find("model")!;
        Assert.Equal("opus", (string?)model.Value);
        Assert.False(model.HasConflict);
    }

    [Fact]
    public void Absent_setting_is_omitted()
    {
        var engine = new MergeEngine();
        var result = engine.Compute(new[] { Scope(ScopeKind.User, "{ }") });
        Assert.Null(result.Find("model"));
    }
}
