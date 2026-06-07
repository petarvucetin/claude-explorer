using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Tests.Dependencies;

public class DependencyExtractorTests
{
    private static EffectiveConfig HooksConfig(string @event, string hooksJson)
    {
        var setting = new EffectiveSetting(
            $"hooks.{@event}", MergeStrategy.ArrayConcat, JsonNode.Parse(hooksJson),
            Winner: null, Contributions: Array.Empty<SettingContribution>(), HasConflict: false);
        return new EffectiveConfig(new[] { setting });
    }

    [Fact]
    public void Extracts_runtimes_from_nested_hook_command_strings()
    {
        var config = HooksConfig("PreToolUse",
            """[ { "matcher": "Bash", "hooks": [ { "type": "command", "command": "npx -y eslint" } ] } ]""");

        var refs = new DependencyExtractor().Extract(config, Array.Empty<McpServer>());

        var npx = Assert.Single(refs);
        Assert.Equal("npx", npx.Name);
        Assert.Equal(new[] { "hook:PreToolUse" }, npx.ReferencedBy);
    }

    [Fact]
    public void Extracts_command_from_stdio_mcp_server_and_skips_url_servers()
    {
        var servers = new[]
        {
            new McpServer("pw", "uvx", new[] { "playwright-mcp" }, ScopeKind.Project),
            new McpServer("remote", null, Array.Empty<string>(), ScopeKind.Project),
        };

        var refs = new DependencyExtractor().Extract(new EffectiveConfig(Array.Empty<EffectiveSetting>()), servers);

        var uvx = Assert.Single(refs);
        Assert.Equal("uvx", uvx.Name);
        Assert.Equal("uvx playwright-mcp", uvx.Raw);
        Assert.Equal(new[] { "mcp:pw" }, uvx.ReferencedBy);
    }

    [Fact]
    public void Deduplicates_by_runtime_and_merges_sources_sorted()
    {
        var config = HooksConfig("PreToolUse",
            """[ { "hooks": [ { "command": "npx run-a" }, { "command": "npx run-b" } ] } ]""");
        var servers = new[] { new McpServer("srv", "npx", new[] { "@x/mcp" }, ScopeKind.Project) };

        var refs = new DependencyExtractor().Extract(config, servers);

        var npx = Assert.Single(refs);
        Assert.Equal("npx", npx.Name);
        Assert.Equal(new[] { "hook:PreToolUse", "mcp:srv" }, npx.ReferencedBy);
    }

    [Fact]
    public void Non_hook_settings_are_ignored()
    {
        var setting = new EffectiveSetting("model", MergeStrategy.ScalarLastWins,
            JsonValue.Create("opus"), null, Array.Empty<SettingContribution>(), false);
        var config = new EffectiveConfig(new[] { setting });

        Assert.Empty(new DependencyExtractor().Extract(config, Array.Empty<McpServer>()));
    }
}
