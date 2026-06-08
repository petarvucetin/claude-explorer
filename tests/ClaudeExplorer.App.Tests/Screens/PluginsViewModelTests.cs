using ClaudeExplorer.App.Screens.Plugins;
using ClaudeExplorer.App.Tests.Fakes;
using ClaudeExplorer.Core.Plugins;

namespace ClaudeExplorer.App.Tests.Screens;

public class PluginsViewModelTests
{
    [Theory]
    [InlineData(1, 0, 0, 0, 0, "1 command")]
    [InlineData(0, 14, 0, 4, 0, "14 skills · 4 hooks")]
    [InlineData(0, 0, 3, 0, 0, "3 subagents")]
    [InlineData(0, 0, 0, 0, 1, "1 mcp")]
    public void ProvidesParts_pluralizes_and_omits_zeros(int c, int s, int a, int h, int m, string expected)
    {
        var parts = PluginCardMapper.ProvidesParts(new ProvidesCounts(c, s, a, h, m));
        Assert.Equal(expected, string.Join(" · ", parts));
    }

    [Fact]
    public void Load_reads_inventory_for_active_workspace()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/plugins/installed_plugins.json",
                """{ "version": 2, "plugins": { "feature-dev@claude-plugins-official": [ { "scope": "user", "version": "1.0" } ] } }""")
            .AddFile("/home/.claude/plugins/known_marketplaces.json",
                """{ "claude-plugins-official": { "source": { "repo": "anthropics/claude-plugins-official" } } }""")
            .AddFile("/home/.claude/plugins/cache/claude-plugins-official/feature-dev/1.0/agents/code-architect.md",
                "---\nname: code-architect\ndescription: x\n---\nb");

        var vm = new PluginsViewModel(new PluginInventoryReader(fs), new FakeWorkspaceContext("/home", ""));
        vm.Load();

        Assert.Null(vm.ErrorMessage);
        var p = Assert.Single(vm.Inventory!.Plugins);
        Assert.Equal("feature-dev", p.Name);
        Assert.Equal(1, p.Provides.Subagents);
        Assert.Single(vm.Inventory.Marketplaces);
    }
}
