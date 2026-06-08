using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.Core.Tests.Mutation;

public class SettingsValidatorTests
{
    private static readonly SettingsValidator Validator = new();

    [Fact]
    public void Valid_settings_pass()
    {
        var json = """
        {
          "model": "claude-opus-4-8",
          "outputStyle": "concise",
          "env": { "FOO": "bar" },
          "permissions": { "allow": ["Bash(ls)"], "deny": [], "defaultMode": "ask" },
          "hooks": { "PreToolUse": [] }
        }
        """;

        var result = Validator.Validate(json);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Empty_object_is_valid()
    {
        Assert.True(Validator.Validate("{}").IsValid);
    }

    [Fact]
    public void Comments_and_trailing_commas_are_tolerated()
    {
        var json = """
        {
          // a comment
          "model": "x",
        }
        """;

        Assert.True(Validator.Validate(json).IsValid);
    }

    [Fact]
    public void Malformed_json_is_invalid()
    {
        var result = Validator.Validate("{ not json ");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Invalid JSON"));
    }

    [Fact]
    public void Non_object_root_is_invalid()
    {
        var result = Validator.Validate("[1, 2, 3]");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("must be a JSON object"));
    }

    [Fact]
    public void Model_must_be_a_string()
    {
        var result = Validator.Validate("""{ "model": 123 }""");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("\"model\""));
    }

    [Fact]
    public void Env_values_must_be_strings()
    {
        var result = Validator.Validate("""{ "env": { "FOO": 5 } }""");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("env.FOO"));
    }

    [Fact]
    public void Env_must_be_an_object()
    {
        var result = Validator.Validate("""{ "env": "nope" }""");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("\"env\""));
    }

    [Fact]
    public void Permission_lists_must_contain_only_strings()
    {
        var result = Validator.Validate("""{ "permissions": { "allow": ["ok", 7] } }""");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("permissions.allow"));
    }

    [Fact]
    public void Permissions_must_be_an_object()
    {
        var result = Validator.Validate("""{ "permissions": [] }""");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("\"permissions\""));
    }

    [Fact]
    public void Hooks_must_be_an_object()
    {
        var result = Validator.Validate("""{ "hooks": [] }""");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("\"hooks\""));
    }

    [Fact]
    public void Multiple_errors_are_all_reported()
    {
        var result = Validator.Validate("""{ "model": 1, "outputStyle": 2 }""");

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
    }
}
