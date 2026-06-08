using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Artifacts;

public class InstalledPluginLocatorTests
{
    [Fact]
    public void Locates_each_plugin_version_root_across_marketplaces()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/plugins/cache/official/superpowers/5.1.0/skills/tdd/SKILL.md", "x")
            .AddFile("/home/.claude/plugins/cache/official/frontend-design/unknown/skills/fd/SKILL.md", "x")
            .AddFile("/home/.claude/plugins/cache/community/foo/1.0.0/commands/bar.md", "x");

        var locations = new InstalledPluginLocator(fs).Locate("/home");

        Assert.Equal(3, locations.Count);
        Assert.Contains(locations, p => p.Name == "superpowers"
            && p.RootPath == "/home/.claude/plugins/cache/official/superpowers/5.1.0");
        Assert.Contains(locations, p => p.Name == "frontend-design"
            && p.RootPath == "/home/.claude/plugins/cache/official/frontend-design/unknown");
        Assert.Contains(locations, p => p.Name == "foo"
            && p.RootPath == "/home/.claude/plugins/cache/community/foo/1.0.0");
    }

    [Fact]
    public void Returns_empty_when_no_plugin_cache()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/settings.json", "{}");

        var locations = new InstalledPluginLocator(fs).Locate("/home");

        Assert.Empty(locations);
    }
}
