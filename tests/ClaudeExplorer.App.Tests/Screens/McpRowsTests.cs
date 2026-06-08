using ClaudeExplorer.App.Screens.Mcp;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Mcp;

namespace ClaudeExplorer.App.Tests.Screens;

public class McpRowsTests
{
    private static McpServerInfo Stdio(string name, string command, params string[] args) =>
        new(name, McpTransport.Stdio, command, args, null, new Dictionary<string, string>(), "plugin: x", "/p/.mcp.json");

    private static McpServerInfo Http(string name, string url) =>
        new(name, McpTransport.Http, null, Array.Empty<string>(), url, new Dictionary<string, string>(), "plugin: x", "/p/.mcp.json");

    private static DependencyReport Report(params (string name, DependencyStatusKind kind)[] entries) =>
        new(entries.Select(e => new DependencyResult(
            new DependencyRef(e.name, e.name, new[] { "mcp:x" }),
            new DependencyStatus(e.kind))).ToList());

    [Fact]
    public void Stdio_server_takes_health_from_its_runtime()
    {
        var servers = new[] { Stdio("playwright", "npx", "@playwright/mcp@latest") };
        var view = McpRowsMapper.Map(servers, Report(("npx", DependencyStatusKind.Found)));

        var row = Assert.Single(view.Rows);
        Assert.Equal(McpHealth.Ok, row.Health);
        Assert.Equal("npx", row.Runtime);
        Assert.Equal(1, view.Stdio);
        Assert.Equal(0, view.Missing);
    }

    [Fact]
    public void Stdio_with_missing_runtime_is_missing()
    {
        var view = McpRowsMapper.Map(new[] { Stdio("unifi", "uvx", "unifi-mcp") },
            Report(("uvx", DependencyStatusKind.Missing)));

        Assert.Equal(McpHealth.Missing, view.Rows[0].Health);
        Assert.Equal(1, view.Missing);
        Assert.Equal("bad", McpRowsMapper.Pill(view.Rows[0].Health));
    }

    [Fact]
    public void Http_server_is_not_applicable_for_health()
    {
        var view = McpRowsMapper.Map(new[] { Http("linear", "https://mcp.linear.app/mcp") },
            Report());

        Assert.Equal(McpHealth.Na, view.Rows[0].Health);
        Assert.Equal(1, view.Remote);
        Assert.Equal("https://mcp.linear.app/mcp", view.Rows[0].Endpoint);
    }
}
