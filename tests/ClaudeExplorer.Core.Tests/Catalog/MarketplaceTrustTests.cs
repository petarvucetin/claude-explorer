using ClaudeExplorer.Core.Catalog;

namespace ClaudeExplorer.Core.Tests.Catalog;

public class MarketplaceTrustTests
{
    [Fact]
    public void Official_marketplace_name_is_verified()
    {
        Assert.Equal(TrustLevel.Verified, MarketplaceTrust.Classify("claude-plugins-official", null));
    }

    [Fact]
    public void Anthropic_owner_email_is_verified_case_insensitively()
    {
        Assert.Equal(TrustLevel.Verified, MarketplaceTrust.Classify("anything", "Support@Anthropic.com"));
    }

    [Theory]
    [InlineData("unifi-plugins", "unifi@privatly.net")]
    [InlineData("context-mode", "code.bm.ksglu@gmail.com")]
    [InlineData(null, null)]
    public void Everything_else_is_community(string? name, string? email)
    {
        Assert.Equal(TrustLevel.Community, MarketplaceTrust.Classify(name, email));
    }
}
