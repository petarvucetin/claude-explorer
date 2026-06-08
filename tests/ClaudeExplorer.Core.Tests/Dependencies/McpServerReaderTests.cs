using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Dependencies;

public class McpServerReaderTests
{
    [Fact]
    public void Reads_stdio_server_from_project_mcp_json_with_command_and_args()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/repo/.mcp.json",
                """{ "mcpServers": { "pw": { "command": "uvx", "args": ["playwright-mcp", "--headless"] } } }""");

        var servers = new McpServerReader(fs).Read("/home", "/repo");

        var pw = Assert.Single(servers);
        Assert.Equal("pw", pw.Name);
        Assert.Equal("uvx", pw.Command);
        Assert.Equal(new[] { "playwright-mcp", "--headless" }, pw.Args);
        Assert.Equal(ScopeKind.Project, pw.Scope);
    }

    [Fact]
    public void Url_server_has_null_command()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/repo/.mcp.json",
                """{ "mcpServers": { "remote": { "type": "sse", "url": "https://example.com/mcp" } } }""");

        var remote = Assert.Single(new McpServerReader(fs).Read("/home", "/repo"));
        Assert.Equal("remote", remote.Name);
        Assert.Null(remote.Command);
        Assert.Empty(remote.Args);
    }

    [Fact]
    public void Reads_mcpServers_block_from_settings_files_with_their_scope()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/settings.json",
                """{ "mcpServers": { "usersrv": { "command": "node", "args": ["server.js"] } } }""")
            .AddFile("/repo/.claude/settings.json",
                """{ "mcpServers": { "projsrv": { "command": "npx", "args": ["@x/mcp"] } } }""");

        var servers = new McpServerReader(fs).Read("/home", "/repo");

        Assert.Contains(servers, s => s.Name == "usersrv" && s.Command == "node" && s.Scope == ScopeKind.User);
        Assert.Contains(servers, s => s.Name == "projsrv" && s.Command == "npx" && s.Scope == ScopeKind.Project);
    }

    [Fact]
    public void Missing_and_malformed_sources_are_skipped()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/repo/.mcp.json", "{ not valid json ");

        Assert.Empty(new McpServerReader(fs).Read("/home", "/repo"));
    }

    [Fact]
    public void Reads_plugin_mcp_json_at_root_so_dependency_health_covers_plugin_servers()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/plugins/cache/official/playwright/unknown/.mcp.json",
                """{ "playwright": { "command": "npx", "args": ["@playwright/mcp@latest"] } }""");

        var pw = Assert.Single(new McpServerReader(fs).Read("/home", "/repo"));
        Assert.Equal("playwright", pw.Name);
        Assert.Equal("npx", pw.Command);
        Assert.Equal(ScopeKind.Plugin, pw.Scope);
    }

    [Fact]
    public void Reads_mcpServers_from_claude_json()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude.json",
                """{ "mcpServers": { "u": { "command": "node", "args": ["s.js"] } } }""");

        var u = Assert.Single(new McpServerReader(fs).Read("/home", "/repo"));
        Assert.Equal("u", u.Name);
        Assert.Equal(ScopeKind.User, u.Scope);
    }
}
