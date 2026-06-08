namespace ClaudeExplorer.App.Services;

public sealed class WorkspaceContext : IWorkspaceContext
{
    /// <summary>Label shown when no project is open — the app reads only the standard user-global
    /// <c>~/.claude</c> folder.</summary>
    public const string UserGlobalLabel = "user · ~/.claude";

    public string UserDir { get; }
    public string ProjectDir { get; }
    public string ProjectLabel { get; }

    /// <param name="projectDir">The active project root, or <c>null</c>/empty when no project is
    /// open (the app then reads only the standard user-global <c>~/.claude</c>).</param>
    public WorkspaceContext(string userDir, string? projectDir)
    {
        UserDir = Normalize(userDir);
        ProjectDir = string.IsNullOrEmpty(projectDir) ? "" : Normalize(projectDir);

        if (string.IsNullOrEmpty(ProjectDir))
        {
            ProjectLabel = UserGlobalLabel;
            return;
        }

        var i = ProjectDir.LastIndexOf('/');
        var segment = i >= 0 && i < ProjectDir.Length - 1 ? ProjectDir[(i + 1)..] : string.Empty;
        ProjectLabel = !string.IsNullOrEmpty(segment) ? segment : ProjectDir;
    }

    private static string Normalize(string p) => p.Replace('\\', '/').TrimEnd('/');
}
