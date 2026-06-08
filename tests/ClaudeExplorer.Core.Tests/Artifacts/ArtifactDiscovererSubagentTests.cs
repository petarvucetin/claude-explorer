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

    [Fact]
    public void Subagent_carries_tools_and_model_frontmatter()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/repo/.claude/agents/cr.md",
                "---\nname: code-reviewer\ndescription: reviews\ntools: Read, Grep, Glob\nmodel: opus\n---\nprompt");

        var agents = new ArtifactDiscoverer(fs)
            .Discover("/home", "/repo", Array.Empty<PluginLocation>())
            .Where(a => a.Kind == ArtifactKind.Subagent).ToList();

        Assert.Equal("Read, Grep, Glob", agents[0].Fm["tools"]);
        Assert.Equal("opus", agents[0].Fm["model"]);
    }

    [Fact]
    public void Command_carries_argument_hint_frontmatter()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/repo/.claude/commands/review.md",
                "---\ndescription: review\nargument-hint: \"[scope]\"\n---\nbody");

        var cmd = new ArtifactDiscoverer(fs)
            .Discover("/home", "/repo", Array.Empty<PluginLocation>())
            .Single(a => a.Kind == ArtifactKind.Command);

        Assert.Equal("[scope]", cmd.Fm["argument-hint"]);
    }

    [Fact]
    public void Skill_counts_bundled_sibling_files_excluding_skill_md()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/skills/graphify/SKILL.md", "---\nname: graphify\ndescription: g\n---\nb")
            .AddFile("/home/.claude/skills/graphify/references/notes.md", "ref")
            .AddFile("/home/.claude/skills/graphify/scripts/run.py", "print(1)");

        var skill = new ArtifactDiscoverer(fs)
            .Discover("/home", null, Array.Empty<PluginLocation>())
            .Single(a => a.Kind == ArtifactKind.Skill);

        Assert.Equal(2, skill.ExtraFileCount);
    }
}
