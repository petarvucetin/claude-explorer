namespace ClaudeExplorer.App.Services;

public sealed class WorkspaceContext : IWorkspaceContext
{
    public string UserDir { get; }
    public string ProjectDir { get; }
    public string ProjectLabel { get; }

    public WorkspaceContext(string userDir, string projectDir)
    {
        UserDir = Normalize(userDir);
        ProjectDir = Normalize(projectDir);
        var i = ProjectDir.LastIndexOf('/');
        var segment = i >= 0 && i < ProjectDir.Length - 1 ? ProjectDir[(i + 1)..] : string.Empty;
        ProjectLabel = !string.IsNullOrEmpty(segment) ? segment
                     : !string.IsNullOrEmpty(ProjectDir) ? ProjectDir
                     : "/";
    }

    private static string Normalize(string p) => p.Replace('\\', '/').TrimEnd('/');
}
