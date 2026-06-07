namespace ClaudeExplorer.Core.Model;

/// <summary>
/// Configuration scopes, ordered by precedence. Higher integer value wins when a key is
/// defined in multiple scopes. Command-line args (between Enterprise and Local at runtime)
/// are not modeled because this tool reads files only.
/// </summary>
public enum ScopeKind
{
    User = 0,
    Project = 1,
    Local = 2,
    Enterprise = 3,
}
