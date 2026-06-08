using ClaudeExplorer.App.Screens.Artifacts;
using ClaudeExplorer.Core.Artifacts;

namespace ClaudeExplorer.App.Tests.Screens;

public class ArtifactDetailTests
{
    [Fact]
    public void ToolChips_splits_comma_or_space_separated_lists()
    {
        Assert.Equal(new[] { "Read", "Grep", "Glob" }, ArtifactDetail.ToolChips("Read, Grep, Glob"));
        Assert.Equal(new[] { "Read", "Grep" }, ArtifactDetail.ToolChips("Read Grep"));
    }

    [Fact]
    public void ToolChips_star_means_all_and_blank_is_empty()
    {
        Assert.Equal(new[] { "all tools" }, ArtifactDetail.ToolChips("*"));
        Assert.Empty(ArtifactDetail.ToolChips(null));
        Assert.Empty(ArtifactDetail.ToolChips("  "));
    }

    [Fact]
    public void Invocation_prefixes_a_slash_once()
    {
        Assert.Equal("/review", ArtifactDetail.Invocation("review"));
        Assert.Equal("/review", ArtifactDetail.Invocation("/review"));
    }

    [Fact]
    public void Mapper_carries_frontmatter_and_extra_file_count_to_items()
    {
        var fm = new Dictionary<string, string> { ["tools"] = "Read, Grep", ["model"] = "opus" };
        var winner = new DiscoveredArtifact(
            ArtifactKind.Subagent, "code-reviewer", "reviews",
            new ArtifactSource(ArtifactSourceKind.Plugin, "feature-dev"), "/cr.md", fm, ExtraFileCount: 3);
        var catalog = new ArtifactCatalog(new[] { new ResolvedArtifact(winner, Array.Empty<DiscoveredArtifact>()) });

        var item = ArtifactBrowserMapper.Group(catalog).Single().Items.Single();

        Assert.Equal("Read, Grep", item.Frontmatter["tools"]);
        Assert.Equal("opus", item.Frontmatter["model"]);
        Assert.Equal(3, item.ExtraFileCount);
    }
}
