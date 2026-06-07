using ClaudeExplorer.Core.Discovery;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Discovery;

public class SettingsLocatorTests
{
    [Fact]
    public void Locates_only_existing_files_in_precedence_order()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/me/.claude/settings.json", "{}")
            .AddFile("/repo/.claude/settings.json", "{}")
            .AddFile("/repo/.claude/settings.local.json", "{}");
        // enterprise file intentionally absent

        var located = new SettingsLocator(fs).Locate("/home/me", "/repo", "/etc/claude/managed-settings.json");

        Assert.Equal(
            new[] { ScopeKind.User, ScopeKind.Project, ScopeKind.Local },
            located.Select(f => f.Scope).ToArray());
        Assert.Equal("/repo/.claude/settings.local.json", located.Single(f => f.Scope == ScopeKind.Local).Path);
    }
}
