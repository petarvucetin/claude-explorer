namespace ClaudeExplorer.App.Services;

/// <summary>The workspace the app is currently inspecting: the user-global config dir and the
/// active project dir. (Multi-project compare is a later concern; v1 carries one project.)</summary>
public interface IWorkspaceContext
{
    /// <summary>Home dir holding <c>.claude/</c> (e.g. the user's profile dir).</summary>
    string UserDir { get; }
    /// <summary>Active project root (holds <c>.claude/</c> and <c>.mcp.json</c>).</summary>
    string ProjectDir { get; }
    /// <summary>Short display name for the project (final path segment).</summary>
    string ProjectLabel { get; }
}
