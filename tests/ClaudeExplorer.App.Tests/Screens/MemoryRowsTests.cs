using ClaudeExplorer.App.Screens.Memory;
using ClaudeExplorer.App.Tests.Fakes;

namespace ClaudeExplorer.App.Tests.Screens;

public class MemoryRowsTests
{
    [Fact]
    public void Discovers_global_then_project_then_nested_in_load_order()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("C:/Users/me/.claude/CLAUDE.md", "# global");
        fs.AddFile("D:/work/a/CLAUDE.md", "# project");
        fs.AddFile("D:/work/a/CLAUDE.local.md", "# local");
        fs.AddFile("D:/work/a/packages/api/CLAUDE.md", "# nested");

        var rows = MemoryRowsMapper.Discover(fs, userDir: "C:/Users/me", projectDir: "D:/work/a");

        Assert.Collection(rows.Select(r => r.Scope),
            s => Assert.Equal(MemoryScope.Global, s),
            s => Assert.Equal(MemoryScope.Project, s),
            s => Assert.Equal(MemoryScope.Local, s),
            s => Assert.Equal(MemoryScope.Nested, s));
        Assert.Equal("D:/work/a/packages/api/CLAUDE.md", rows.Last().Path);
        Assert.Equal("# global", rows.First().Content);
    }

    [Fact]
    public void Omits_absent_files()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("C:/Users/me/.claude/CLAUDE.md", "# global");
        var rows = MemoryRowsMapper.Discover(fs, "C:/Users/me", projectDir: "");
        Assert.Single(rows);
        Assert.Equal(MemoryScope.Global, rows[0].Scope);
    }

    [Fact]
    public void Nested_excludes_the_top_level_project_claude_md()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("D:/work/a/CLAUDE.md", "# project");
        fs.AddFile("D:/work/a/sub/CLAUDE.md", "# nested");
        var rows = MemoryRowsMapper.Discover(fs, "C:/Users/me", "D:/work/a");

        Assert.Single(rows, r => r.Scope == MemoryScope.Project);
        Assert.Single(rows, r => r.Scope == MemoryScope.Nested && r.Path == "D:/work/a/sub/CLAUDE.md");
    }
}
