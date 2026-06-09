using ClaudeExplorer.Core.Plugins;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Plugins;

public class PluginRootReaderTests
{
    [Fact]
    public void Derives_a_cache_root_per_installed_plugin_version()
    {
        var fs = new InMemoryFileSystem().AddFile("/home/.claude/plugins/installed_plugins.json", """
            {
              "version": 2,
              "plugins": {
                "superpowers@claude-plugins-official": [ { "installPath": "C:\\ignored\\absolute", "version": "5.1.0", "scope": "user" } ],
                "unifi-network@unifi-plugins": [ { "version": "0.17.3", "scope": "user" } ]
              }
            }
            """);

        var roots = new PluginRootReader(fs).ReadRoots("/home");

        Assert.Equal(new[]
        {
            "/home/.claude/plugins/cache/claude-plugins-official/superpowers/5.1.0",
            "/home/.claude/plugins/cache/unifi-plugins/unifi-network/0.17.3",
        }, roots);
    }

    [Fact]
    public void Missing_registry_yields_no_roots()
        => Assert.Empty(new PluginRootReader(new InMemoryFileSystem()).ReadRoots("/home"));

    [Fact]
    public void Malformed_registry_yields_no_roots()
    {
        var fs = new InMemoryFileSystem().AddFile("/home/.claude/plugins/installed_plugins.json", "{ not json");
        Assert.Empty(new PluginRootReader(fs).ReadRoots("/home"));
    }
}
