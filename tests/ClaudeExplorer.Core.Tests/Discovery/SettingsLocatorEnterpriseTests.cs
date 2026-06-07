using ClaudeExplorer.Core.Discovery;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Discovery;

public class SettingsLocatorEnterpriseTests
{
    // T5: enterprise file exists → included as ScopeKind.Enterprise, ordered last (highest precedence)
    [Fact]
    public void Enterprise_file_included_when_exists_and_ordered_last()
    {
        const string enterprisePath = "/etc/claude/managed-settings.json";

        var fs = new InMemoryFileSystem()
            .AddFile("/home/me/.claude/settings.json", "{}")
            .AddFile("/repo/.claude/settings.json", "{}")
            .AddFile(enterprisePath, "{}");
        // settings.local.json intentionally absent

        var located = new SettingsLocator(fs).Locate("/home/me", "/repo", enterprisePath);

        // Enterprise is present
        var enterprise = located.SingleOrDefault(f => f.Scope == ScopeKind.Enterprise);
        Assert.NotNull(enterprise);
        Assert.Equal(enterprisePath, enterprise!.Path);

        // Enterprise is ordered last (highest precedence integer = 3)
        Assert.Equal(ScopeKind.Enterprise, located[^1].Scope);

        // Order is User, Project, Enterprise
        Assert.Equal(
            new[] { ScopeKind.User, ScopeKind.Project, ScopeKind.Enterprise },
            located.Select(f => f.Scope).ToArray());
    }
}
