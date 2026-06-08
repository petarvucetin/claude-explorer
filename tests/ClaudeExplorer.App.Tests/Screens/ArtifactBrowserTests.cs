using ClaudeExplorer.App.Screens.Artifacts;
using ClaudeExplorer.Core.Artifacts;

namespace ClaudeExplorer.App.Tests.Screens;

public class ArtifactBrowserTests
{
    private static ResolvedArtifact Art(
        ArtifactKind kind,
        string name,
        ArtifactSourceKind srcKind,
        string? plugin = null,
        bool shadowed = false)
    {
        var winner = new DiscoveredArtifact(kind, name, $"Summary of {name}",
            new ArtifactSource(srcKind, plugin), $"/{name}.md");
        var shadows = shadowed
            ? new[] { new DiscoveredArtifact(kind, name, null, new ArtifactSource(ArtifactSourceKind.User), $"/u/{name}.md") }
            : Array.Empty<DiscoveredArtifact>();
        return new ResolvedArtifact(winner, shadows);
    }

    private static ArtifactCatalog Catalog(params ResolvedArtifact[] artifacts)
        => new(artifacts);

    [Fact]
    public void Groups_by_source_label()
    {
        var catalog = Catalog(
            Art(ArtifactKind.Command, "cmd-a", ArtifactSourceKind.User),
            Art(ArtifactKind.Skill, "skill-b", ArtifactSourceKind.Project),
            Art(ArtifactKind.Command, "cmd-c", ArtifactSourceKind.User));

        var groups = ArtifactBrowserMapper.Group(catalog);

        // Should have User and Project groups
        Assert.Equal(2, groups.Count);
        var userGroup = groups.Single(g => g.Label == "User");
        Assert.Equal(2, userGroup.Items.Count);
        var projGroup = groups.Single(g => g.Label == "Project");
        Assert.Single(projGroup.Items);
    }

    [Fact]
    public void Filter_by_kind_removes_non_matching_items()
    {
        var catalog = Catalog(
            Art(ArtifactKind.Command, "cmd", ArtifactSourceKind.User),
            Art(ArtifactKind.Skill, "skill", ArtifactSourceKind.User));

        var groups = ArtifactBrowserMapper.Group(catalog);
        var filtered = ArtifactBrowserMapper.Filter(groups, ArtifactKind.Command, null);

        Assert.Single(filtered);
        Assert.Equal("User", filtered[0].Label);
        Assert.Single(filtered[0].Items);
        Assert.Equal("cmd", filtered[0].Items[0].Name);
    }

    [Fact]
    public void Filter_by_search_uses_ordinal_case_insensitive()
    {
        var catalog = Catalog(
            Art(ArtifactKind.Command, "MyCommand", ArtifactSourceKind.User),
            Art(ArtifactKind.Command, "other", ArtifactSourceKind.User));

        var groups = ArtifactBrowserMapper.Group(catalog);
        var filtered = ArtifactBrowserMapper.Filter(groups, null, "myco");

        Assert.Single(filtered);
        Assert.Equal("MyCommand", filtered[0].Items[0].Name);
    }

    [Fact]
    public void Shadowed_flag_surfaced_on_item()
    {
        var catalog = Catalog(
            Art(ArtifactKind.Command, "shadowed-cmd", ArtifactSourceKind.Project, shadowed: true));

        var groups = ArtifactBrowserMapper.Group(catalog);
        var item = groups[0].Items[0];

        Assert.True(item.IsShadowing);
        Assert.NotEmpty(item.Shadowed);
    }

    [Fact]
    public void Empty_catalog_returns_empty_groups()
    {
        var groups = ArtifactBrowserMapper.Group(Catalog());
        Assert.Empty(groups);
    }

    [Fact]
    public void Filter_with_null_kind_and_empty_search_returns_all()
    {
        var catalog = Catalog(
            Art(ArtifactKind.Command, "a", ArtifactSourceKind.User),
            Art(ArtifactKind.Skill, "b", ArtifactSourceKind.Project));

        var groups = ArtifactBrowserMapper.Group(catalog);
        var filtered = ArtifactBrowserMapper.Filter(groups, null, null);

        Assert.Equal(2, filtered.Count);
    }

    [Fact]
    public void Plugin_source_label_includes_plugin_name()
    {
        var catalog = Catalog(
            Art(ArtifactKind.Skill, "playwright", ArtifactSourceKind.Plugin, plugin: "playwright-plugin"));

        var groups = ArtifactBrowserMapper.Group(catalog);
        Assert.Single(groups);
        Assert.Contains("playwright-plugin", groups[0].Label);
    }
}
