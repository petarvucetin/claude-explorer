using ClaudeExplorer.Core.Catalog;

namespace ClaudeExplorer.Core.Tests.Catalog;

public class CatalogModelTests
{
    [Fact]
    public void Source_carries_kind_trust_name_and_location()
    {
        var src = new CatalogSource(CatalogSourceKind.GitHub, TrustLevel.Community, "owner/repo",
            "https://raw.githubusercontent.com/owner/repo/HEAD/.claude-plugin/marketplace.json");

        Assert.Equal(CatalogSourceKind.GitHub, src.Kind);
        Assert.Equal(TrustLevel.Community, src.Trust);
        Assert.Equal("owner/repo", src.Name);
    }

    [Fact]
    public void Item_defaults_tags_empty_and_stats_null_and_keeps_fields()
    {
        var src = new CatalogSource(CatalogSourceKind.ClaudeMarketplace, TrustLevel.Verified, "official", "/p");
        var item = new CatalogItem(
            Name: "feature-dev",
            Type: CatalogItemType.Plugin,
            Summary: "Feature development workflow",
            Author: "Anthropic",
            Category: "development",
            Homepage: "https://example.com",
            Tags: new[] { "community-managed" },
            Source: src,
            Trust: src.Trust);

        Assert.Equal(CatalogItemType.Plugin, item.Type);
        Assert.Equal(TrustLevel.Verified, item.Trust);
        Assert.Equal("Anthropic", item.Author);
        Assert.Contains("community-managed", item.Tags);
        Assert.Null(item.Stats);
    }

    [Fact]
    public void Stats_are_optional_when_present()
    {
        var stats = new CatalogItemStats(Stars: 42);
        Assert.Equal(42, stats.Stars);
        Assert.Null(stats.Downloads);
    }
}
