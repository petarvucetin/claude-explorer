using ClaudeExplorer.Core.Artifacts;

namespace ClaudeExplorer.Core.Tests.Artifacts;

public class ArtifactModelTests
{
    [Fact]
    public void Source_label_and_precedence()
    {
        Assert.Equal("User", new ArtifactSource(ArtifactSourceKind.User).Label);
        Assert.Equal("Plugin: superpowers", new ArtifactSource(ArtifactSourceKind.Plugin, "superpowers").Label);
        Assert.True(new ArtifactSource(ArtifactSourceKind.Project).Precedence
                    > new ArtifactSource(ArtifactSourceKind.User).Precedence);
        Assert.True(new ArtifactSource(ArtifactSourceKind.User).Precedence
                    > new ArtifactSource(ArtifactSourceKind.Plugin, "x").Precedence);
    }

    [Fact]
    public void Resolved_artifact_reports_shadowing_and_catalog_filters_by_kind()
    {
        var win = new DiscoveredArtifact(ArtifactKind.Command, "review", "sum",
            new ArtifactSource(ArtifactSourceKind.Project), "/p/.claude/commands/review.md");
        var resolved = new ResolvedArtifact(win, Array.Empty<DiscoveredArtifact>());
        Assert.False(resolved.IsShadowing);

        var catalog = new ArtifactCatalog(new[] { resolved });
        Assert.Single(catalog.OfKind(ArtifactKind.Command));
        Assert.Empty(catalog.OfKind(ArtifactKind.Skill));
    }
}
