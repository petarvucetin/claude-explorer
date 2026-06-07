using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Artifacts;

public class ArtifactDiscovererSubagentTests
{
    [Fact]
    public void Discovers_top_level_agent_md_files_only()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/repo/.claude/agents/reviewer.md", "---\nname: reviewer\ndescription: reviews code\n---\nbody")
            .AddFile("/repo/.claude/agents/notes/scratch.md", "nested - should be ignored");

        var found = new ArtifactDiscoverer(fs).Discover("/home", "/repo", Array.Empty<PluginLocation>());
        var agents = found.Where(a => a.Kind == ArtifactKind.Subagent).ToList();

        Assert.Single(agents);
        Assert.Equal("reviewer", agents[0].Name);
        Assert.Equal(ArtifactSourceKind.Project, agents[0].Source.Kind);
        Assert.Equal("reviews code", agents[0].Summary);
    }
}
