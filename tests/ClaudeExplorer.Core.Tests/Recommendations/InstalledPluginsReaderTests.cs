using ClaudeExplorer.Core.Recommendations;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Recommendations;

public class InstalledPluginsReaderTests
{
    [Fact]
    public void Reads_plugin_names_from_the_cache_across_marketplaces()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/plugins/cache/claude-plugins-official/feature-dev/unknown/.claude-plugin/plugin.json", "{}")
            .AddFile("/home/.claude/plugins/cache/claude-plugins-official/superpowers/5.1.0/plugin.json", "{}")
            .AddFile("/home/.claude/plugins/cache/unifi-plugins/unifi-network/0.17.3/.mcp.json", "{}");

        var installed = new InstalledPluginsReader(fs).Read("/home");

        Assert.Contains("feature-dev", installed);
        Assert.Contains("superpowers", installed);
        Assert.Contains("unifi-network", installed);
        Assert.DoesNotContain("claude-plugins-official", installed); // marketplace dir, not a plugin
    }

    [Fact]
    public void No_cache_yields_empty_set()
    {
        Assert.Empty(new InstalledPluginsReader(new InMemoryFileSystem()).Read("/home"));
    }
}
