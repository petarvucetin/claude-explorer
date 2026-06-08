using ClaudeExplorer.App.Screens.Mcp;
using ClaudeExplorer.App.Tests.Fakes;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Mcp;

namespace ClaudeExplorer.App.Tests.Screens;

public class McpViewModelTests
{
    [Fact]
    public void Load_reads_plugin_mcp_servers_and_joins_health()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/plugins/cache/o/playwright/1.0.0/.mcp.json",
                """{ "playwright": { "command": "npx", "args": ["@playwright/mcp@latest"] } }""")
            .AddFile("/home/.claude/plugins/cache/o/linear/1.0.0/.mcp.json",
                """{ "linear": { "type": "http", "url": "https://mcp.linear.app/mcp" } }""");

        var reader = new McpInventoryReader(fs);
        var resolver = new FakePathResolver().Add("npx", "/usr/bin/npx");
        var runner = new FakeProcessRunner().AddVersion("/usr/bin/npx", "10.0.0");
        var health = new DependencyHealthService(fs, resolver, runner);

        var vm = new McpViewModel(reader, health, new FakeWorkspaceContext("/home", ""));
        vm.Load();

        Assert.Null(vm.ErrorMessage);
        Assert.Equal(2, vm.View!.Rows.Count);
        Assert.Equal(McpHealth.Ok, vm.View.Rows.Single(r => r.Name == "playwright").Health);
        Assert.Equal(McpHealth.Na, vm.View.Rows.Single(r => r.Name == "linear").Health);
    }
}
