using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Artifacts;

public class ArtifactDiscovererCommandTests
{
    [Fact]
    public void Discovers_commands_from_user_and_project_with_name_and_summary()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/commands/standup.md", "---\ndescription: daily standup\n---\nbody")
            .AddFile("/repo/.claude/commands/deploy.md", "# Deploy\nDeploys the app.");

        var found = new ArtifactDiscoverer(fs)
            .Discover("/home", "/repo", Array.Empty<PluginLocation>());

        var standup = found.Single(a => a.Name == "standup");
        Assert.Equal(ArtifactKind.Command, standup.Kind);
        Assert.Equal(ArtifactSourceKind.User, standup.Source.Kind);
        Assert.Equal("daily standup", standup.Summary);

        var deploy = found.Single(a => a.Name == "deploy");
        Assert.Equal(ArtifactSourceKind.Project, deploy.Source.Kind);
        Assert.Equal("Deploys the app.", deploy.Summary);
    }

    [Fact]
    public void Command_name_uses_filename_when_no_frontmatter_name()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/commands/nested/thing.md", "x");
        var found = new ArtifactDiscoverer(fs).Discover("/home", null, Array.Empty<PluginLocation>());
        Assert.Equal("thing", found.Single().Name);
    }
}
