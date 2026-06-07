using ClaudeExplorer.Core.Catalog;

namespace ClaudeExplorer.Core.Tests.Catalog;

public class SourceDetectorTests
{
    [Fact]
    public void Detects_owner_repo_as_github_with_raw_manifest_url_and_community_trust()
    {
        var src = SourceDetector.Detect("octocat/plugins");

        Assert.Equal(CatalogSourceKind.GitHub, src.Kind);
        Assert.Equal(TrustLevel.Community, src.Trust);
        Assert.Equal("octocat/plugins", src.Name);
        Assert.Equal("https://raw.githubusercontent.com/octocat/plugins/HEAD/.claude-plugin/marketplace.json",
            src.Location);
    }

    [Theory]
    [InlineData("https://github.com/octocat/plugins")]
    [InlineData("https://github.com/octocat/plugins.git")]
    [InlineData("https://github.com/octocat/plugins/")]
    public void Detects_github_urls(string input)
    {
        var src = SourceDetector.Detect(input);
        Assert.Equal(CatalogSourceKind.GitHub, src.Kind);
        Assert.Equal("octocat/plugins", src.Name);
        Assert.Equal(SourceDetector.RawGitHubManifestUrl("octocat", "plugins"), src.Location);
    }

    [Fact]
    public void Detects_plain_url_and_appends_manifest_path_when_not_json()
    {
        var src = SourceDetector.Detect("https://example.com/my-marketplace");
        Assert.Equal(CatalogSourceKind.Url, src.Kind);
        Assert.Equal(TrustLevel.Community, src.Trust);
        Assert.Equal("https://example.com/my-marketplace/.claude-plugin/marketplace.json", src.Location);
    }

    [Fact]
    public void Plain_url_pointing_at_json_is_used_as_is()
    {
        var src = SourceDetector.Detect("https://example.com/m/marketplace.json");
        Assert.Equal(CatalogSourceKind.Url, src.Kind);
        Assert.Equal("https://example.com/m/marketplace.json", src.Location);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a source!!")]
    public void Unrecognized_input_throws(string input)
    {
        Assert.Throws<FormatException>(() => SourceDetector.Detect(input));
    }
}
