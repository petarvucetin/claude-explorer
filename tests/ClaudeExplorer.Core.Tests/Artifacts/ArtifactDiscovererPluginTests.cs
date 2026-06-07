using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Artifacts;

public class ArtifactDiscovererPluginTests
{
    [Fact]
    public void Discovers_plugin_commands_and_skills_tagged_with_plugin_name()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/plugins/superpowers/commands/brainstorm.md", "---\ndescription: explore design\n---\nb")
            .AddFile("/plugins/superpowers/skills/tdd/SKILL.md", "---\nname: tdd\ndescription: test first\n---\nb");

        var plugins = new[] { new PluginLocation("superpowers", "/plugins/superpowers") };
        var found = new ArtifactDiscoverer(fs).Discover("/home", null, plugins);

        Assert.All(found, a =>
        {
            Assert.Equal(ArtifactSourceKind.Plugin, a.Source.Kind);
            Assert.Equal("superpowers", a.Source.PluginName);
        });
        Assert.Contains(found, a => a.Kind == ArtifactKind.Command && a.Name == "brainstorm");
        Assert.Contains(found, a => a.Kind == ArtifactKind.Skill && a.Name == "tdd");
    }
}
