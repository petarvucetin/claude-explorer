using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Discovery;

/// <summary>
/// Locates the settings files that exist for a workspace. Paths are built with forward
/// slashes for determinism; .NET accepts '/' on all platforms.
/// </summary>
public sealed class SettingsLocator
{
    private readonly IFileSystem _fs;

    public SettingsLocator(IFileSystem fs) => _fs = fs;

    public IReadOnlyList<ConfigFile> Locate(string userDir, string projectDir, string? enterprisePath = null)
    {
        var candidates = new List<ConfigFile>();
        if (enterprisePath is not null)
            candidates.Add(new ConfigFile(ScopeKind.Enterprise, enterprisePath));
        candidates.Add(new ConfigFile(ScopeKind.User, $"{userDir}/.claude/settings.json"));
        candidates.Add(new ConfigFile(ScopeKind.Project, $"{projectDir}/.claude/settings.json"));
        candidates.Add(new ConfigFile(ScopeKind.Local, $"{projectDir}/.claude/settings.local.json"));

        return candidates
            .Where(c => _fs.FileExists(c.Path))
            .OrderBy(c => (int)c.Scope)
            .ToList();
    }
}
