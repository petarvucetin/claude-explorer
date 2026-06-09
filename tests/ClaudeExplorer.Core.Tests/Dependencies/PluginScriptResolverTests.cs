using ClaudeExplorer.Core.Dependencies;

namespace ClaudeExplorer.Core.Tests.Dependencies;

public class PluginScriptResolverTests
{
    private static readonly string[] Roots =
    {
        "/home/.claude/plugins/cache/claude-plugins-official/superpowers/5.1.0",
    };

    [Fact]
    public void Resolves_plugin_root_template_against_the_owning_plugin()
    {
        var resolved = PluginScriptResolver.Resolve(
            "\"${CLAUDE_PLUGIN_ROOT}/hooks/run-hook.cmd\" session-start",
            "/home/.claude/plugins/cache/claude-plugins-official/superpowers/5.1.0/hooks/hooks.json",
            Roots);

        Assert.Equal(
            "/home/.claude/plugins/cache/claude-plugins-official/superpowers/5.1.0/hooks/run-hook.cmd",
            resolved);
    }

    [Fact]
    public void Returns_null_for_a_plain_command()
        => Assert.Null(PluginScriptResolver.Resolve("npx -y eslint", "/home/.claude/settings.json", Roots));

    [Fact]
    public void Returns_null_when_no_root_owns_the_origin_file()
        => Assert.Null(PluginScriptResolver.Resolve(
            "\"${CLAUDE_PLUGIN_ROOT}/hooks/run-hook.cmd\" x",
            "/some/other/place/hooks.json",
            Roots));

    [Fact]
    public void Normalizes_backslashes_in_roots_and_origin()
    {
        var resolved = PluginScriptResolver.Resolve(
            "\"${CLAUDE_PLUGIN_ROOT}/hooks/run-hook.cmd\" x",
            @"C:\Users\p\.claude\plugins\cache\m\sp\1.0\hooks\hooks.json",
            new[] { @"C:\Users\p\.claude\plugins\cache\m\sp\1.0" });

        Assert.Equal("C:/Users/p/.claude/plugins/cache/m/sp/1.0/hooks/run-hook.cmd", resolved);
    }
}
