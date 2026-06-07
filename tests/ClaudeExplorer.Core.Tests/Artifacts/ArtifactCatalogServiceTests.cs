using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Artifacts;

public class ArtifactCatalogServiceTests
{
    [Fact]
    public void Builds_categorized_catalog_with_shadowing_across_all_sources()
    {
        var fs = new InMemoryFileSystem()
            // user
            .AddFile("/home/.claude/commands/review.md", "---\ndescription: user review\n---\nb")
            .AddFile("/home/.claude/skills/graphify/SKILL.md", "---\nname: graphify\ndescription: to graph\n---\nb")
            // project overrides the user 'review' command
            .AddFile("/repo/.claude/commands/review.md", "---\ndescription: project review\n---\nb")
            // plugin
            .AddFile("/plugins/superpowers/skills/tdd/SKILL.md", "---\nname: tdd\ndescription: test first\n---\nb");

        var plugins = new[] { new PluginLocation("superpowers", "/plugins/superpowers") };
        var catalog = new ArtifactCatalogService(fs).Build("/home", "/repo", plugins);

        // review: project wins, user shadowed
        var review = catalog.OfKind(ArtifactKind.Command).Single(r => r.Winner.Name == "review");
        Assert.Equal(ArtifactSourceKind.Project, review.Winner.Source.Kind);
        Assert.Equal("project review", review.Winner.Summary);
        Assert.True(review.IsShadowing);
        Assert.Single(review.Shadowed);

        // skills: graphify (user) + tdd (plugin), no collisions
        var skills = catalog.OfKind(ArtifactKind.Skill).ToList();
        Assert.Equal(2, skills.Count);
        Assert.Contains(skills, s => s.Winner.Name == "graphify" && s.Winner.Source.Kind == ArtifactSourceKind.User);
        Assert.Contains(skills, s => s.Winner.Name == "tdd" && s.Winner.Source.Label == "Plugin: superpowers");
    }

    [Fact]
    public void Empty_workspace_yields_empty_catalog()
    {
        var catalog = new ArtifactCatalogService(new InMemoryFileSystem()).Build("/home");
        Assert.Empty(catalog.Artifacts);
    }
}
