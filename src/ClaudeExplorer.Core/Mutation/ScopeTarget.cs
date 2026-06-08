using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Mutation;

/// <summary>How the user wants an edit to land relative to the current winning value.</summary>
public enum EditMode
{
    /// <summary>Write into the file/scope that currently provides the winning value.</summary>
    EditWinner,

    /// <summary>Create or update an override in the project <c>settings.json</c>.</summary>
    OverrideAtProject,

    /// <summary>Create or update an override in the project <c>settings.local.json</c>.</summary>
    OverrideAtLocal,
}

/// <summary>The concrete destination an edit resolves to.</summary>
public sealed record ResolvedTarget(ScopeKind Scope, string FilePath);

/// <summary>
/// Resolves an <see cref="EditMode"/> plus workspace context to the concrete settings file an
/// edit will be written to. "Edit winner" follows the current provenance; the override modes
/// always target the project / local settings files regardless of where the winner lives. Paths
/// use forward slashes and mirror <c>SettingsLocator</c>'s layout.
/// </summary>
public sealed class ScopeTargetResolver
{
    public ResolvedTarget Resolve(EditMode mode, string projectDir, SettingOrigin? winner)
    {
        var proj = projectDir.Replace('\\', '/').TrimEnd('/');
        return mode switch
        {
            EditMode.EditWinner => winner is not null
                ? new ResolvedTarget(winner.Scope, winner.FilePath)
                : throw new InvalidOperationException(
                    "Cannot edit the winning source: the setting is not defined in any scope. " +
                    "Choose an override target (Project or Local) instead."),
            EditMode.OverrideAtProject => new ResolvedTarget(ScopeKind.Project, $"{proj}/.claude/settings.json"),
            EditMode.OverrideAtLocal => new ResolvedTarget(ScopeKind.Local, $"{proj}/.claude/settings.local.json"),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown edit mode."),
        };
    }
}
