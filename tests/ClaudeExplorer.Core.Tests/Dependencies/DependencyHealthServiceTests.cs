using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Dependencies;

public class DependencyHealthServiceTests
{
    [Fact]
    public void End_to_end_classifies_hook_and_mcp_dependencies()
    {
        var fs = new InMemoryFileSystem()
            // hooks: one npx command (present) and one python3 command (missing)
            .AddFile("/home/.claude/settings.json", """
                {
                  "hooks": {
                    "PreToolUse": [
                      { "matcher": "Bash", "hooks": [ { "type": "command", "command": "npx -y eslint" } ] },
                      { "matcher": "Edit", "hooks": [ { "type": "command", "command": "python3 -m guard" } ] }
                    ]
                  }
                }
                """)
            // MCP: a stdio uvx server (present) and an sse server (no command -> skipped)
            .AddFile("/repo/.mcp.json", """
                {
                  "mcpServers": {
                    "pw": { "command": "uvx", "args": ["playwright-mcp"] },
                    "remote": { "type": "sse", "url": "https://example.com/mcp" }
                  }
                }
                """);

        var resolver = new FakePathResolver()
            .Add("npx", "/usr/bin/npx")
            .Add("uvx", "/usr/bin/uvx"); // python3 intentionally absent
        var runner = new FakeProcessRunner()
            .AddVersion("/usr/bin/npx", "10.2.0")
            .AddVersion("/usr/bin/uvx", "uv 0.4.0");

        var report = new DependencyHealthService(fs, resolver, runner).Check("/home", "/repo");

        Assert.Equal(3, report.Results.Count); // npx, python3, uvx ("remote" skipped)
        Assert.Equal(2, report.Count(DependencyStatusKind.Found));
        Assert.Equal(1, report.Count(DependencyStatusKind.Missing));
        Assert.False(report.AllHealthy);

        var npx = report.Results.Single(r => r.Ref.Name == "npx");
        Assert.Equal(DependencyStatusKind.Found, npx.Status.Kind);
        Assert.Equal("10.2.0", npx.Status.Version);
        Assert.Equal(new[] { "hook:PreToolUse" }, npx.Ref.ReferencedBy);

        var python = report.Results.Single(r => r.Ref.Name == "python3");
        Assert.Equal(DependencyStatusKind.Missing, python.Status.Kind);

        var uvx = report.Results.Single(r => r.Ref.Name == "uvx");
        Assert.Equal(DependencyStatusKind.Found, uvx.Status.Kind);
        Assert.Equal(new[] { "mcp:pw" }, uvx.Ref.ReferencedBy);
    }

    [Fact]
    public void Plugin_root_templated_hook_resolves_via_registry_and_is_found_not_missing()
    {
        const string root = "/home/.claude/plugins/cache/claude-plugins-official/superpowers/5.1.0";
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/plugins/installed_plugins.json", """
                { "version": 2, "plugins": {
                    "superpowers@claude-plugins-official": [ { "version": "5.1.0", "scope": "user" } ] } }
                """)
            .AddFile($"{root}/hooks/hooks.json", """
                { "hooks": { "SessionStart": [
                    { "matcher": "startup", "hooks": [
                        { "type": "command", "command": "\"${CLAUDE_PLUGIN_ROOT}/hooks/run-hook.cmd\" session-start" } ] } ] } }
                """)
            .AddFile($"{root}/hooks/run-hook.cmd", "echo");

        var report = new DependencyHealthService(fs, new FakePathResolver(), new FakeProcessRunner())
            .Check("/home", "/repo");

        var runHook = Assert.Single(report.Results);
        Assert.Equal("run-hook", runHook.Ref.Name);
        Assert.Equal(DependencyStatusKind.Found, runHook.Status.Kind);
        Assert.Equal($"{root}/hooks/run-hook.cmd", runHook.Status.Path);
        Assert.Equal(new[] { "hook:SessionStart" }, runHook.Ref.ReferencedBy);
    }

    [Fact]
    public void Empty_workspace_yields_empty_report()
    {
        var report = new DependencyHealthService(
                new InMemoryFileSystem(), new FakePathResolver(), new FakeProcessRunner())
            .Check("/home", "/repo");

        Assert.Empty(report.Results);
    }
}
