using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.App.Services;

/// <summary>
/// Decides the active project directory at startup. The app ALWAYS reads the standard user-global
/// <c>~/.claude</c> folder (that is the <c>UserDir</c>); a project is overlaid only when the app is
/// launched from — or explicitly pointed at — a real Claude project (a directory containing a
/// <c>.claude</c> folder). Returns <c>null</c> when there is no project, so a plain desktop launch
/// shows just the standard folder rather than inventing a "project" from the process's working dir.
/// </summary>
public static class WorkspaceResolver
{
    /// <summary>Resolve the project dir: the first command-line argument that is a Claude project,
    /// else <paramref name="currentDir"/> when it is a Claude project, else <c>null</c>.</summary>
    public static string? ResolveProjectDir(IReadOnlyList<string> args, string currentDir, IFileSystem fs)
    {
        foreach (var arg in args)
            if (!string.IsNullOrWhiteSpace(arg) && IsClaudeProject(arg, fs))
                return arg;

        return IsClaudeProject(currentDir, fs) ? currentDir : null;
    }

    /// <summary>A directory is treated as a Claude project when it contains a <c>.claude</c> folder.</summary>
    public static bool IsClaudeProject(string dir, IFileSystem fs)
    {
        if (string.IsNullOrWhiteSpace(dir)) return false;
        var normalized = dir.Replace('\\', '/').TrimEnd('/');
        return fs.DirectoryExists($"{normalized}/.claude");
    }
}
