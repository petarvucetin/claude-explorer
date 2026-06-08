using ClaudeExplorer.Core.Catalog;
using ClaudeExplorer.Core.Plugins;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Plugins;

public class PluginInventoryReaderTests
{
    private static InMemoryFileSystem Machine()
    {
        var cache = "/home/.claude/plugins/cache";
        return new InMemoryFileSystem()
            .AddFile("/home/.claude/plugins/installed_plugins.json", """
            {
              "version": 2,
              "plugins": {
                "superpowers@claude-plugins-official": [ { "scope": "user", "version": "5.1.0" } ],
                "unifi-network@unifi-plugins": [ { "scope": "user", "version": "0.17.3" } ]
              }
            }
            """)
            .AddFile("/home/.claude/plugins/known_marketplaces.json", """
            {
              "claude-plugins-official": { "source": { "source": "github", "repo": "anthropics/claude-plugins-official" } },
              "unifi-plugins": { "source": { "source": "github", "repo": "someone/unifi-plugins" } }
            }
            """)
            .AddFile("/home/.claude/settings.json", """
            { "enabledPlugins": { "superpowers@claude-plugins-official": true, "unifi-network@unifi-plugins": false } }
            """)
            // superpowers provides: 1 skill + a hooks.json (1 event)
            .AddFile($"{cache}/claude-plugins-official/superpowers/5.1.0/skills/brainstorming/SKILL.md",
                "---\nname: brainstorming\ndescription: x\n---\nb")
            .AddFile($"{cache}/claude-plugins-official/superpowers/5.1.0/hooks/hooks.json",
                """{ "hooks": { "SessionStart": [ { "matcher": "startup" } ] } }""")
            // unifi provides: 1 mcp server
            .AddFile($"{cache}/unifi-plugins/unifi-network/0.17.3/.mcp.json",
                """{ "unifi": { "command": "uvx", "args": ["unifi-mcp"] } }""");
    }

    [Fact]
    public void Reads_installed_plugins_with_marketplace_version_scope_and_enabled()
    {
        var inv = new PluginInventoryReader(Machine()).Read("/home");

        var sp = inv.Plugins.Single(p => p.Name == "superpowers");
        Assert.Equal("claude-plugins-official", sp.Marketplace);
        Assert.Equal("5.1.0", sp.Version);
        Assert.Equal("user", sp.Scope);
        Assert.True(sp.Enabled);
        Assert.Equal(TrustLevel.Verified, sp.Trust);

        var unifi = inv.Plugins.Single(p => p.Name == "unifi-network");
        Assert.False(unifi.Enabled);                 // explicitly disabled
        Assert.Equal(TrustLevel.Community, unifi.Trust);
    }

    [Fact]
    public void Counts_what_each_plugin_provides()
    {
        var inv = new PluginInventoryReader(Machine()).Read("/home");

        var sp = inv.Plugins.Single(p => p.Name == "superpowers");
        Assert.Equal(1, sp.Provides.Skills);
        Assert.Equal(1, sp.Provides.Hooks);
        Assert.Equal(0, sp.Provides.Mcp);

        var unifi = inv.Plugins.Single(p => p.Name == "unifi-network");
        Assert.Equal(1, unifi.Provides.Mcp);
        Assert.Equal(0, unifi.Provides.Skills);
    }

    [Fact]
    public void Reads_marketplaces_with_trust_and_installed_counts()
    {
        var inv = new PluginInventoryReader(Machine()).Read("/home");

        var official = inv.Marketplaces.Single(m => m.Name == "claude-plugins-official");
        Assert.Equal(TrustLevel.Verified, official.Trust);
        Assert.Equal("anthropics/claude-plugins-official", official.SourceRepo);
        Assert.Equal(1, official.InstalledCount);

        var unifi = inv.Marketplaces.Single(m => m.Name == "unifi-plugins");
        Assert.Equal(TrustLevel.Community, unifi.Trust);
        Assert.Equal(1, unifi.InstalledCount);
    }

    [Fact]
    public void Empty_when_nothing_installed()
    {
        var inv = new PluginInventoryReader(new InMemoryFileSystem()).Read("/home");
        Assert.Empty(inv.Plugins);
        Assert.Empty(inv.Marketplaces);
    }
}
