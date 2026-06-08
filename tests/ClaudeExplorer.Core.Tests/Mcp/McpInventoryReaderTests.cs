using ClaudeExplorer.Core.Mcp;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Mcp;

public class McpInventoryReaderTests
{
    [Fact]
    public void Reads_plugin_mcp_json_with_servers_at_root()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/plugins/cache/official/linear/unknown/.mcp.json",
                """{ "linear": { "type": "http", "url": "https://mcp.linear.app/mcp" } }""")
            .AddFile("/home/.claude/plugins/cache/official/playwright/unknown/.mcp.json",
                """{ "playwright": { "command": "npx", "args": ["@playwright/mcp@latest"] } }""");

        var servers = new McpInventoryReader(fs).Read("/home", "");

        var linear = servers.Single(s => s.Name == "linear");
        Assert.Equal(McpTransport.Http, linear.Transport);
        Assert.Equal("https://mcp.linear.app/mcp", linear.Url);
        Assert.Equal("plugin: linear", linear.SourceLabel);

        var pw = servers.Single(s => s.Name == "playwright");
        Assert.Equal(McpTransport.Stdio, pw.Transport);
        Assert.Equal("npx", pw.Command);
        Assert.Equal(new[] { "@playwright/mcp@latest" }, pw.Args);
        Assert.Equal("npx @playwright/mcp@latest", pw.Endpoint);
    }

    [Fact]
    public void Reads_wrapped_mcp_servers_from_settings_and_claude_json_with_env()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/settings.json",
                """{ "mcpServers": { "fs": { "command": "node", "args": ["server.js"], "env": { "TOKEN": "x" } } } }""")
            .AddFile("/home/.claude.json",
                """{ "mcpServers": { "remote": { "type": "sse", "url": "https://e.example/sse" } } } """);

        var servers = new McpInventoryReader(fs).Read("/home", "");

        var local = servers.Single(s => s.Name == "fs");
        Assert.Equal(McpTransport.Stdio, local.Transport);
        Assert.Equal("user", local.SourceLabel);
        Assert.Equal("x", local.Env["TOKEN"]);

        var remote = servers.Single(s => s.Name == "remote");
        Assert.Equal(McpTransport.Sse, remote.Transport);
        Assert.Equal("https://e.example/sse", remote.Url);
    }

    [Fact]
    public void Empty_when_nothing_configured()
    {
        var servers = new McpInventoryReader(new InMemoryFileSystem()).Read("/home", "/repo");
        Assert.Empty(servers);
    }
}
