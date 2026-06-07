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

    [Fact]
    public void Shadowed_entries_retain_their_summary_and_path()
    {
        var user = new DiscoveredArtifact(ArtifactKind.Command, "review", "u",
            new ArtifactSource(ArtifactSourceKind.User), "/U/review");
        var project = new DiscoveredArtifact(ArtifactKind.Command, "review", "p",
            new ArtifactSource(ArtifactSourceKind.Project), "/P/review");

        var catalog = new ArtifactResolver().Resolve(new[] { user, project });
        var review = catalog.Artifacts.Single();

        Assert.Equal(ArtifactSourceKind.Project, review.Winner.Source.Kind);
        var shadow = Assert.Single(review.Shadowed);
        Assert.Equal("u", shadow.Summary);
        Assert.Equal("/U/review", shadow.FilePath);
    }

    [Fact]
    public void Plugin_vs_plugin_same_name_is_deterministic()
    {
        // Two Plugin-source skills with the same name from different plugins.
        // Ordinal tie-break on plugin name: "alpha" < "beta", so "alpha" wins.
        var beta = new DiscoveredArtifact(ArtifactKind.Skill, "x", null,
            new ArtifactSource(ArtifactSourceKind.Plugin, "beta"), "/beta/x");
        var alpha = new DiscoveredArtifact(ArtifactKind.Skill, "x", null,
            new ArtifactSource(ArtifactSourceKind.Plugin, "alpha"), "/alpha/x");

        var catalog = new ArtifactResolver().Resolve(new[] { beta, alpha });
        var resolved = catalog.Artifacts.Single();

        Assert.Equal("alpha", resolved.Winner.Source.PluginName);
        var shadowedPlugin = Assert.Single(resolved.Shadowed);
        Assert.Equal("beta", shadowedPlugin.Source.PluginName);
    }
}
