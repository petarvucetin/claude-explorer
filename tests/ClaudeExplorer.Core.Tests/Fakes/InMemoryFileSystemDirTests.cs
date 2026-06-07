using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Fakes;

public class InMemoryFileSystemDirTests
{
    private static InMemoryFileSystem Fs() => new InMemoryFileSystem()
        .AddFile("/u/.claude/commands/a.md", "a")
        .AddFile("/u/.claude/commands/sub/b.md", "b")
        .AddFile("/u/.claude/commands/c.txt", "c")
        .AddFile("/u/.claude/skills/alpha/SKILL.md", "s");

    [Fact]
    public void DirectoryExists_is_true_when_a_file_lives_under_it()
    {
        var fs = Fs();
        Assert.True(fs.DirectoryExists("/u/.claude/commands"));
        Assert.False(fs.DirectoryExists("/u/.claude/nope"));
    }

    [Fact]
    public void GetDirectories_returns_immediate_children_only()
    {
        var fs = Fs();
        Assert.Equal(new[] { "/u/.claude/skills/alpha" }, fs.GetDirectories("/u/.claude/skills"));
    }

    [Fact]
    public void GetFiles_filters_by_pattern_and_recursion()
    {
        var fs = Fs();
        Assert.Equal(new[] { "/u/.claude/commands/a.md" },
            fs.GetFiles("/u/.claude/commands", "*.md", recurse: false));
        Assert.Equal(new[] { "/u/.claude/commands/a.md", "/u/.claude/commands/sub/b.md" },
            fs.GetFiles("/u/.claude/commands", "*.md", recurse: true));
    }

    [Fact]
    public void GetFiles_on_missing_dir_is_empty()
    {
        Assert.Empty(Fs().GetFiles("/missing", "*.md", recurse: true));
    }
}
