using ClaudeExplorer.Core.Mutation;
using ClaudeExplorer.Core.Sync;

namespace ClaudeExplorer.Core.Tests.Sync;

public class SettingsKeyEditorTests
{
    [Fact]
    public void SetKey_adds_into_empty_and_preserves_siblings()
    {
        var outp = SettingsKeyEditor.SetKey("""{ "model": "opus" }""", "env", """{ "A": "1" }""");
        Assert.Contains("\"model\": \"opus\"", outp);
        Assert.Contains("\"env\"", outp);
        Assert.Contains("\"A\": \"1\"", outp);
    }

    [Fact]
    public void SetKey_into_missing_file_creates_object()
        => Assert.Contains("\"model\"", SettingsKeyEditor.SetKey("", "model", "\"opus\""));

    [Fact]
    public void SetKey_overwrites_existing_key()
        => Assert.Contains("\"sonnet\"", SettingsKeyEditor.SetKey("""{ "model": "opus" }""", "model", "\"sonnet\""));

    [Fact]
    public void RemoveKey_drops_only_that_key()
    {
        var outp = SettingsKeyEditor.RemoveKey("""{ "model": "opus", "env": { "A": "1" } }""", "model");
        Assert.DoesNotContain("model", outp);
        Assert.Contains("\"env\"", outp);
    }

    [Fact]
    public void SetKey_rejects_invalid_value_json()
        => Assert.Throws<MutationException>(() => SettingsKeyEditor.SetKey("{}", "x", "{ not json"));
}
