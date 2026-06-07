using ClaudeExplorer.Core.Catalog;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Catalog;

public class CatalogServiceTests
{
    [Fact]
    public void Installed_catalog_merges_marketplaces_sorted_and_deduped_with_trust()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/plugins/marketplaces/claude-plugins-official/.claude-plugin/marketplace.json",
                """
                {
                  "name": "claude-plugins-official",
                  "owner": { "email": "support@anthropic.com" },
                  "plugins": [
                    { "name": "zeta", "description": "z" },
                    { "name": "alpha", "description": "a" },
                    { "name": "alpha", "description": "dup -> deduped" }
                  ]
                }
                """)
            .AddFile("/home/.claude/plugins/marketplaces/community/.claude-plugin/marketplace.json",
                """
                { "name": "community", "owner": { "email": "x@y.com" }, "plugins": [ { "name": "beta" } ] }
                """);

        var catalog = new CatalogService(fs, new FakeCatalogFetcher()).BuildInstalledCatalog("/home");

        // sorted by (source, name); dup alpha collapsed
        Assert.Equal(new[] { "alpha", "zeta", "beta" }, catalog.Select(i => i.Name).ToArray());
        Assert.Equal(TrustLevel.Verified, catalog.Single(i => i.Name == "zeta").Trust);
        Assert.Equal(TrustLevel.Community, catalog.Single(i => i.Name == "beta").Trust);
    }

    [Fact]
    public void Fetches_added_source_metadata_via_fetcher_with_community_trust()
    {
        var manifestUrl = SourceDetector.RawGitHubManifestUrl("octocat", "plugins");
        var fetcher = new FakeCatalogFetcher().Add(manifestUrl,
            """{ "name": "octo", "plugins": [ { "name": "tool", "description": "t" } ] }""");

        var items = new CatalogService(new InMemoryFileSystem(), fetcher).FetchAddedSource("octocat/plugins");

        var tool = Assert.Single(items);
        Assert.Equal("tool", tool.Name);
        Assert.Equal(TrustLevel.Community, tool.Trust);
        Assert.Equal(CatalogSourceKind.GitHub, tool.Source.Kind);
        Assert.Equal(new[] { manifestUrl }, fetcher.Requests); // metadata-only: a single manifest fetch
    }

    [Fact]
    public void Added_source_that_cannot_be_fetched_yields_empty()
    {
        var items = new CatalogService(new InMemoryFileSystem(), new FakeCatalogFetcher())
            .FetchAddedSource("octocat/plugins"); // fetcher has nothing registered -> null
        Assert.Empty(items);
    }
}
