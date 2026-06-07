using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Fakes;

public class InMemoryFileSystemTests
{
    [Fact]
    public void Reports_existence_and_reads_content()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/u/.claude/settings.json", "{}");

        Assert.True(fs.FileExists("/u/.claude/settings.json"));
        Assert.False(fs.FileExists("/missing.json"));
        Assert.Equal("{}", fs.ReadAllText("/u/.claude/settings.json"));
    }
}
