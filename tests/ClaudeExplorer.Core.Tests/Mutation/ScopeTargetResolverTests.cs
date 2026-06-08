using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.Core.Tests.Mutation;

public class ScopeTargetResolverTests
{
    private static readonly ScopeTargetResolver Resolver = new();

    [Fact]
    public void EditWinner_follows_the_current_winning_origin()
    {
        var winner = new SettingOrigin(ScopeKind.User, "/home/u/.claude/settings.json", "model");

        var target = Resolver.Resolve(EditMode.EditWinner, "/work/proj", winner);

        Assert.Equal(ScopeKind.User, target.Scope);
        Assert.Equal("/home/u/.claude/settings.json", target.FilePath);
    }

    [Fact]
    public void EditWinner_throws_when_setting_is_not_defined_anywhere()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => Resolver.Resolve(EditMode.EditWinner, "/work/proj", winner: null));

        Assert.Contains("override", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OverrideAtProject_targets_project_settings_regardless_of_winner()
    {
        var winner = new SettingOrigin(ScopeKind.User, "/home/u/.claude/settings.json", "model");

        var target = Resolver.Resolve(EditMode.OverrideAtProject, "/work/proj", winner);

        Assert.Equal(ScopeKind.Project, target.Scope);
        Assert.Equal("/work/proj/.claude/settings.json", target.FilePath);
    }

    [Fact]
    public void OverrideAtLocal_targets_local_settings_regardless_of_winner()
    {
        var target = Resolver.Resolve(EditMode.OverrideAtLocal, "/work/proj", winner: null);

        Assert.Equal(ScopeKind.Local, target.Scope);
        Assert.Equal("/work/proj/.claude/settings.local.json", target.FilePath);
    }
}
