using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Artifacts;

public class ArtifactDiscovererSkillTests
{
    [Fact]
    public void Discovers_skills_from_SKILL_md_using_frontmatter_name()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/skills/graphify/SKILL.md", "---\nname: graphify\ndescription: input to graph\n---\nbody")
            .AddFile("/home/.claude/skills/empty-dir/README.md", "not a skill");

        var found = new ArtifactDiscoverer(fs).Discover("/home", null, Array.Empty<PluginLocation>());
        var skills = found.Where(a => a.Kind == ArtifactKind.Skill).ToList();

        Assert.Single(skills);
        Assert.Equal("graphify", skills[0].Name);
        Assert.Equal("input to graph", skills[0].Summary);
        Assert.EndsWith("graphify/SKILL.md", skills[0].FilePath);
    }

    [Fact]
    public void Skill_name_falls_back_to_directory_name()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/skills/my-skill/SKILL.md", "no frontmatter here");
        var found = new ArtifactDiscoverer(fs).Discover("/home", null, Array.Empty<PluginLocation>());
        Assert.Equal("my-skill", found.Single(a => a.Kind == ArtifactKind.Skill).Name);
    }
}
