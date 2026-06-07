using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Tests.Model;

public class ScopeKindTests
{
    [Fact]
    public void Precedence_orders_user_lowest_enterprise_highest()
    {
        Assert.True((int)ScopeKind.User < (int)ScopeKind.Project);
        Assert.True((int)ScopeKind.Project < (int)ScopeKind.Local);
        Assert.True((int)ScopeKind.Local < (int)ScopeKind.Enterprise);
    }

    [Fact]
    public void ConfigFile_carries_scope_and_path()
    {
        var f = new ConfigFile(ScopeKind.Project, "/repo/.claude/settings.json");
        Assert.Equal(ScopeKind.Project, f.Scope);
        Assert.Equal("/repo/.claude/settings.json", f.Path);
    }
}
