using ClaudeExplorer.Core.Catalog;

namespace ClaudeExplorer.Core.Tests.Catalog;

public class MarketplaceManifestParserTests
{
    private static CatalogSource Src(TrustLevel trust = TrustLevel.Community)
        => new(CatalogSourceKind.ClaudeMarketplace, trust, "mkt", "/p");

    private const string Manifest = """
        {
          "name": "mkt",
          "owner": { "name": "Owner", "email": "owner@example.com" },
          "plugins": [
            {
              "name": "feature-dev",
              "description": "Feature development workflow",
              "author": { "name": "Anthropic" },
              "category": "development",
              "homepage": "https://example.com/fd",
              "tags": ["community-managed"],
              "source": "./plugins/feature-dev"
            },
            {
              "name": "minimal",
              "source": { "source": "url", "url": "https://github.com/x/y.git" }
            },
            {
              "description": "no name -> skipped"
            }
          ]
        }
        """;

    [Fact]
    public void Parses_plugins_into_items_with_fields_and_inherited_trust()
    {
        var items = MarketplaceManifestParser.Parse(Manifest, Src(TrustLevel.Verified));

        Assert.Equal(2, items.Count); // the unnamed entry is skipped

        var fd = items.Single(i => i.Name == "feature-dev");
        Assert.Equal(CatalogItemType.Plugin, fd.Type);
        Assert.Equal("Feature development workflow", fd.Summary);
        Assert.Equal("Anthropic", fd.Author);
        Assert.Equal("development", fd.Category);
        Assert.Equal("https://example.com/fd", fd.Homepage);
        Assert.Contains("community-managed", fd.Tags);
        Assert.Equal(TrustLevel.Verified, fd.Trust);

        var minimal = items.Single(i => i.Name == "minimal");
        Assert.Null(minimal.Summary);
        Assert.Null(minimal.Author);
        Assert.Empty(minimal.Tags);
    }

    [Fact]
    public void ReadHeader_returns_name_and_owner_email()
    {
        var (name, email) = MarketplaceManifestParser.ReadHeader(Manifest);
        Assert.Equal("mkt", name);
        Assert.Equal("owner@example.com", email);
    }

    [Fact]
    public void Malformed_or_empty_manifest_yields_no_items()
    {
        Assert.Empty(MarketplaceManifestParser.Parse("{ not json", Src()));
        Assert.Empty(MarketplaceManifestParser.Parse(null, Src()));
        Assert.Empty(MarketplaceManifestParser.Parse("""{ "name": "x" }""", Src())); // no plugins array
    }
}
