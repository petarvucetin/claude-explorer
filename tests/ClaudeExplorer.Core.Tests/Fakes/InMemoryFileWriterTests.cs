using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Fakes;

public class InMemoryFileWriterTests
{
    [Fact]
    public void WriteAllText_creates_a_readable_file()
    {
        var fs = new InMemoryFileSystem();
        IFileWriter writer = fs;

        writer.WriteAllText("/p/.claude/settings.json", "{}");

        Assert.True(fs.FileExists("/p/.claude/settings.json"));
        Assert.Equal("{}", fs.ReadAllText("/p/.claude/settings.json"));
    }

    [Fact]
    public void WriteAllText_overwrites_existing_content()
    {
        var fs = new InMemoryFileSystem().AddFile("/a.json", "old");
        IFileWriter writer = fs;

        writer.WriteAllText("/a.json", "new");

        Assert.Equal("new", fs.ReadAllText("/a.json"));
    }

    [Fact]
    public void Delete_removes_the_file()
    {
        var fs = new InMemoryFileSystem().AddFile("/a.json", "x");
        IFileWriter writer = fs;

        writer.Delete("/a.json");

        Assert.False(fs.FileExists("/a.json"));
    }

    [Fact]
    public void Delete_is_a_no_op_when_file_is_absent()
    {
        var fs = new InMemoryFileSystem();
        IFileWriter writer = fs;

        writer.Delete("/missing.json"); // must not throw

        Assert.False(fs.FileExists("/missing.json"));
    }

    [Fact]
    public void Writes_normalize_backslashes_so_reads_via_forward_slashes_match()
    {
        var fs = new InMemoryFileSystem();
        IFileWriter writer = fs;

        writer.WriteAllText(@"C:\p\.claude\settings.json", "{}");

        Assert.True(fs.FileExists("C:/p/.claude/settings.json"));
    }
}
