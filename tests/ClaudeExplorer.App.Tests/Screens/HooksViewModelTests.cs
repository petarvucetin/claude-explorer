using ClaudeExplorer.App.Screens.Hooks;
using ClaudeExplorer.App.Tests.Fakes;
using ClaudeExplorer.Core;
using ClaudeExplorer.Core.Dependencies;

namespace ClaudeExplorer.App.Tests.Screens;

public class HooksViewModelTests
{
    [Fact]
    public void Load_merges_settings_and_plugin_hooks_for_the_active_workspace()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/settings.json",
                """{ "hooks": { "PreToolUse": [ { "matcher": "Bash", "hooks": [ { "type": "command", "command": "node guard.js" } ] } ] } }""")
            .AddFile("/home/.claude/plugins/cache/o/superpowers/5.1.0/hooks/hooks.json",
                """{ "hooks": { "SessionStart": [ { "matcher": "startup", "hooks": [ { "type": "command", "command": "run-hook session-start" } ] } ] } }""");

        var config = new EffectiveConfigService(fs);
        var resolver = new FakePathResolver().Add("node", "/usr/bin/node");
        var runner = new FakeProcessRunner().AddVersion("/usr/bin/node", "20.0.0");
        var health = new DependencyHealthService(fs, resolver, runner);

        var vm = new HooksViewModel(config, health, new FakeWorkspaceContext("/home", ""));
        vm.Load();

        Assert.Null(vm.ErrorMessage);
        Assert.Equal(2, vm.View!.Total);
        Assert.Contains(vm.View.Groups, g => g.Event == "PreToolUse");
        Assert.Contains(vm.View.Groups, g => g.Event == "SessionStart");
        Assert.Equal(HookHealth.Ok, vm.View.Groups.Single(g => g.Event == "PreToolUse").Rows.Single().Health);
    }
}
