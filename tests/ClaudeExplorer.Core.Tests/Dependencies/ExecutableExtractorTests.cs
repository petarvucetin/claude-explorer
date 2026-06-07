using ClaudeExplorer.Core.Dependencies;

namespace ClaudeExplorer.Core.Tests.Dependencies;

public class ExecutableExtractorTests
{
    [Theory]
    [InlineData("npx -y @playwright/mcp@latest", "npx")]
    [InlineData("uvx some-mcp-server", "uvx")]
    [InlineData("uv run mytool", "uv")]
    [InlineData("python -m http.server", "python")]
    [InlineData("docker run --rm img", "docker")]
    [InlineData("podman run img", "podman")]
    [InlineData("/usr/local/bin/node script.js", "node")]
    public void Extracts_first_token_as_runtime_name(string commandLine, string expected)
    {
        Assert.Equal(expected, ExecutableExtractor.Extract(commandLine));
    }

    [Fact]
    public void Honors_quoting_and_strips_windows_extension()
    {
        Assert.Equal("node",
            ExecutableExtractor.Extract("\"C:/Program Files/nodejs/node.exe\" app.js"));
    }

    [Fact]
    public void Leading_whitespace_is_ignored()
    {
        Assert.Equal("git", ExecutableExtractor.Extract("   git status"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Blank_input_yields_null(string? commandLine)
    {
        Assert.Null(ExecutableExtractor.Extract(commandLine));
    }
}
