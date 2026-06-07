using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Merge;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Tests.Merge;

public class MergeEngineListTests
{
    private static ScopeSettings Scope(ScopeKind kind, string json)
        => new(kind, $"/{kind}.json", (JsonObject)JsonNode.Parse(json)!);

    [Fact]
    public void Permission_allow_is_unioned_across_scopes_with_dedup()
    {
        var engine = new MergeEngine();
        var result = engine.Compute(new[]
        {
            Scope(ScopeKind.User, """{ "permissions": { "allow": ["Bash(git*)"] } }"""),
            Scope(ScopeKind.Project, """{ "permissions": { "allow": ["Read(src/**)", "Bash(git*)"] } }"""),
            Scope(ScopeKind.Local, """{ "permissions": { "allow": ["Bash(npm*)"] } }"""),
        });

        var allow = result.Find("permissions.allow")!;
        var values = ((JsonArray)allow.Value!).Select(n => (string?)n).ToArray();

        Assert.Equal(new[] { "Bash(git*)", "Read(src/**)", "Bash(npm*)" }, values);  // dedup, precedence order
        Assert.Equal(MergeStrategy.ListUnion, allow.Strategy);
        Assert.Null(allow.Winner);          // merges have no single winner
        Assert.False(allow.HasConflict);    // union is not a conflict
        Assert.Equal(3, allow.Contributions.Count);
    }
}
