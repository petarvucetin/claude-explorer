using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.App.Screens.Memory;

public enum MemoryScope { Global, Project, Local, Nested }

/// <summary>One discovered CLAUDE.md memory file.</summary>
public sealed record MemoryRow(MemoryScope Scope, string Name, string Path, string Content);

/// <summary>Pure discovery of CLAUDE.md files in load order: global (~/.claude), project root
/// (CLAUDE.md then CLAUDE.local.md), then nested project CLAUDE.md (excluding the root). Absent files
/// are omitted. No writes — read-only over <see cref="IFileSystem"/>.</summary>
public static class MemoryRowsMapper
{
    public static IReadOnlyList<MemoryRow> Discover(IFileSystem fs, string userDir, string projectDir)
    {
        var rows = new List<MemoryRow>();

        var global = $"{userDir.Replace('\\', '/').TrimEnd('/')}/.claude/CLAUDE.md";
        if (!string.IsNullOrEmpty(userDir) && fs.FileExists(global))
            rows.Add(new MemoryRow(MemoryScope.Global, "CLAUDE.md", global, fs.ReadAllText(global)));

        var proj = projectDir.Replace('\\', '/').TrimEnd('/');
        if (!string.IsNullOrEmpty(proj))
        {
            var rootMd = $"{proj}/CLAUDE.md";
            if (fs.FileExists(rootMd))
                rows.Add(new MemoryRow(MemoryScope.Project, "CLAUDE.md", rootMd, fs.ReadAllText(rootMd)));

            var localMd = $"{proj}/CLAUDE.local.md";
            if (fs.FileExists(localMd))
                rows.Add(new MemoryRow(MemoryScope.Local, "CLAUDE.local.md", localMd, fs.ReadAllText(localMd)));

            foreach (var f in fs.GetFiles(proj, "CLAUDE.md", recurse: true))
            {
                if (f == rootMd) continue; // already added as Project
                rows.Add(new MemoryRow(MemoryScope.Nested, "CLAUDE.md", f, fs.ReadAllText(f)));
            }
        }

        return rows;
    }
}
