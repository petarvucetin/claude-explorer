using ClaudeExplorer.Core.Artifacts;

namespace ClaudeExplorer.Core.Tests.Artifacts;

public class ArtifactSummaryTests
{
    [Fact]
    public void Prefers_frontmatter_description()
    {
        var fm = Frontmatter.Parse("---\ndescription: the summary\n---\n# Title\nbody");
        Assert.Equal("the summary", ArtifactSummary.Extract(fm));
    }

    [Fact]
    public void Falls_back_to_first_non_heading_body_line()
    {
        var fm = Frontmatter.Parse("---\nname: x\n---\n# Title\n\nFirst real line.\nsecond");
        Assert.Equal("First real line.", ArtifactSummary.Extract(fm));
    }

    [Fact]
    public void Strips_leading_hashes_when_only_headings_exist()
    {
        var fm = Frontmatter.Parse("## Only A Heading");
        Assert.Equal("Only A Heading", ArtifactSummary.Extract(fm));
    }

    [Fact]
    public void Returns_null_when_empty()
    {
        var fm = Frontmatter.Parse("---\nname: x\n---\n   \n");
        Assert.Null(ArtifactSummary.Extract(fm));
    }
}
