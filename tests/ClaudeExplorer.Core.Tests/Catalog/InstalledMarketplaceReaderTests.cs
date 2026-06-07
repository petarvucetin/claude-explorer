using ClaudeExplorer.Core.Catalog;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Catalog;

public class InstalledMarketplaceReaderTests
{
    [Fact]
    public void Reads_official_as_verified_and_community_as_community()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/plugins/marketplaces/claude-plugins-official/.claude-plugin/marketplace.json",
                """
                {
                  "name": "claude-plugins-official",
                  "owner": { "name": "Anthropic", "email": "support@anthropic.com" },
                  "plugins": [ { "name": "feature-dev", "description": "wf" } ]
                }
                """)
            .AddFile("/home/.claude/plugins/marketplaces/unifi-plugins/.claude-plugin/marketplace.json",
                """
                {
                  "name": "unifi-plugins",
                  "owner": { "name": "sirkirby", "email": "unifi@privatly.net" },
                  "plugins": [ { "name": "unifi-network", "description": "net" } ]
                }
                """);

        var items = new InstalledMarketplaceReader(fs).Read("/home");

        var fd = items.Single(i => i.Name == "feature-dev");
        Assert.Equal(TrustLevel.Verified, fd.Trust);
        Assert.Equal(CatalogSourceKind.ClaudeMarketplace, fd.Source.Kind);
        Assert.Equal("claude-plugins-official", fd.Source.Name);

        var net = items.Single(i => i.Name == "unifi-network");
        Assert.Equal(TrustLevel.Community, net.Trust);
        Assert.Equal("unifi-plugins", net.Source.Name);
    }

    [Fact]
    public void Marketplace_directory_without_a_manifest_is_skipped()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/plugins/marketplaces/broken/README.md", "no manifest here");

        Assert.Empty(new InstalledMarketplaceReader(fs).Read("/home"));
    }

    [Fact]
    public void No_marketplaces_directory_yields_empty()
    {
        Assert.Empty(new InstalledMarketplaceReader(new InMemoryFileSystem()).Read("/home"));
    }
}
