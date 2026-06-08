namespace ClaudeExplorer.Core.Model;

/// <summary>
/// Configuration scopes, ordered by precedence. Higher integer value wins when a key is
/// defined in multiple scopes. Command-line args (between Enterprise and Local at runtime)
/// are not modeled because this tool reads files only.
/// </summary>
public enum ScopeKind
{
    /// <summary>Config contributed by an installed plugin (e.g. a plugin's hooks). Lowest precedence —
    /// a base layer the user/project/enterprise scopes override.</summary>
    Plugin = -1,
    User = 0,
    Project = 1,
    Local = 2,
    Enterprise = 3,
}
