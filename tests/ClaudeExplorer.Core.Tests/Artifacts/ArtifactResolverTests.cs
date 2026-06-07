using ClaudeExplorer.Core.Artifacts;

namespace ClaudeExplorer.Core.Tests.Artifacts;

public class ArtifactResolverTests
{
    private static DiscoveredArtifact A(ArtifactKind kind, string name, ArtifactSourceKind src, string? plugin = null)
        => new(kind, name, null, new ArtifactSource(src, plugin), $"/{src}/{name}");

    [Fact]
    public void Highest_precedence_source_wins_and_others_are_shadowed()
    {
        var input = new[]
        {
            A(ArtifactKind.Command, "review", ArtifactSourceKind.User),
            A(ArtifactKind.Command, "review", ArtifactSourceKind.Project),
            A(ArtifactKind.Command, "review", ArtifactSourceKind.Plugin, "pack"),
        };

        var catalog = new ArtifactResolver().Resolve(input);
        var review = catalog.Artifacts.Single();

        Assert.Equal(ArtifactSourceKind.Project, review.Winner.Source.Kind);
        Assert.True(review.IsShadowing);
        Assert.Equal(2, review.Shadowed.Count);
        Assert.Contains(review.Shadowed, s => s.Source.Kind == ArtifactSourceKind.User);
        Assert.Contains(review.Shadowed, s => s.Source.Kind == ArtifactSourceKind.Plugin);
    }

    [Fact]
    public void Different_kinds_with_same_name_do_not_collide()
    {
        var input = new[]
        {
            A(ArtifactKind.Command, "x", ArtifactSourceKind.User),
            A(ArtifactKind.Skill, "x", ArtifactSourceKind.User),
        };
        var catalog = new ArtifactResolver().Resolve(input);
        Assert.Equal(2, catalog.Artifacts.Count);
        Assert.All(catalog.Artifacts, r => Assert.False(r.IsShadowing));
    }

    [Fact]
    public void Output_is_sorted_by_kind_then_name()
    {
        var input = new[]
        {
            A(ArtifactKind.Skill, "beta", ArtifactSourceKind.User),
            A(ArtifactKind.Command, "zeta", ArtifactSourceKind.User),
            A(ArtifactKind.Command, "alpha", ArtifactSourceKind.User),
        };
        var names = new ArtifactResolver().Resolve(input).Artifacts
            .Select(r => $"{r.Winner.Kind}:{r.Winner.Name}").ToArray();
        Assert.Equal(new[] { "Command:alpha", "Command:zeta", "Skill:beta" }, names);
    }
}
