using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Reading;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Reading;

public class SettingsReaderTests
{
    [Fact]
    public void Parses_object_allowing_comments_and_trailing_commas()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/u/.claude/settings.json", """
            {
              // user model
              "model": "opus",
            }
            """);
        var reader = new SettingsReader(fs);

        var obj = reader.Read(new ConfigFile(ScopeKind.User, "/u/.claude/settings.json"));

        Assert.Equal("opus", (string?)obj["model"]);
    }

    [Fact]
    public void Throws_when_root_is_not_an_object()
    {
        var fs = new InMemoryFileSystem().AddFile("/u/.claude/settings.json", "[1,2,3]");
        var reader = new SettingsReader(fs);

        Assert.Throws<SettingsParseException>(
            () => reader.Read(new ConfigFile(ScopeKind.User, "/u/.claude/settings.json")));
    }
}
