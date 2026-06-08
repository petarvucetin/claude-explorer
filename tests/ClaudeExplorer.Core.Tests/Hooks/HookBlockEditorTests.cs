using ClaudeExplorer.Core.Hooks;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.Core.Tests.Hooks;

public class HookBlockEditorTests
{
    private const string Source = """
        {
          "model": "opus",
          "hooks": {
            "PostToolUse": [
              { "matcher": "Bash", "hooks": [ { "type": "command", "command": "a.js" } ] },
              { "matcher": "Edit", "hooks": [ { "type": "command", "command": "b.js" } ] }
            ]
          }
        }
        """;

    [Fact]
    public void Extract_returns_the_indexed_block_pretty_printed()
    {
        var block = HookBlockEditor.ExtractBlock(Source, "PostToolUse", 1);
        Assert.Contains("\"matcher\": \"Edit\"", block);
        Assert.Contains("\n", block); // pretty-printed
        Assert.DoesNotContain("Bash", block);
    }

    [Fact]
    public void Extract_throws_on_index_out_of_range()
        => Assert.Throws<MutationException>(() => HookBlockEditor.ExtractBlock(Source, "PostToolUse", 5));

    [Fact]
    public void Splice_replaces_only_the_target_block_and_preserves_the_rest()
    {
        var edited = """{ "matcher": "Edit|Write", "hooks": [ { "type": "command", "command": "b2.js" } ] }""";

        var result = HookBlockEditor.SpliceBlock(Source, "PostToolUse", 1, edited);

        Assert.Contains("b2.js", result);
        Assert.Contains("Edit|Write", result);
        Assert.Contains("\"model\": \"opus\"", result); // sibling preserved
        Assert.Contains("a.js", result);                // other block preserved
        Assert.DoesNotContain("\"b.js\"", result);      // old value gone
    }

    [Fact]
    public void Splice_rejects_invalid_json()
        => Assert.Throws<MutationException>(() => HookBlockEditor.SpliceBlock(Source, "PostToolUse", 0, "{ not json"));

    [Fact]
    public void Splice_rejects_block_without_hooks_array()
        => Assert.Throws<MutationException>(() => HookBlockEditor.SpliceBlock(Source, "PostToolUse", 0, """{ "matcher": "Bash" }"""));

    [Fact]
    public void Splice_throws_on_index_out_of_range()
        => Assert.Throws<MutationException>(() => HookBlockEditor.SpliceBlock(Source, "PostToolUse", 9, """{ "hooks": [] }"""));
}
