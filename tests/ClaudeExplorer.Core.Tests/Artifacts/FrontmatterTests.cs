using ClaudeExplorer.Core.Artifacts;

namespace ClaudeExplorer.Core.Tests.Artifacts;

public class FrontmatterTests
{
    [Fact]
    public void Parses_fields_and_body_stripping_quotes()
    {
        var content = "---\nname: graphify\ndescription: \"turn input into a graph\"\n---\n# Heading\nbody text\n";
        var fm = Frontmatter.Parse(content);

        Assert.Equal("graphify", fm.Fields["name"]);
        Assert.Equal("turn input into a graph", fm.Fields["description"]);
        Assert.Contains("body text", fm.Body);
        Assert.DoesNotContain("name:", fm.Body);
    }

    [Fact]
    public void Field_lookup_is_case_insensitive()
    {
        var fm = Frontmatter.Parse("---\nName: x\n---\n");
        Assert.Equal("x", fm.Fields["name"]);
    }

    [Fact]
    public void No_frontmatter_returns_empty_fields_and_whole_body()
    {
        var fm = Frontmatter.Parse("# Just a title\ncontent");
        Assert.Empty(fm.Fields);
        Assert.Contains("Just a title", fm.Body);
    }

    [Fact]
    public void Handles_crlf_line_endings()
    {
        var fm = Frontmatter.Parse("---\r\nname: y\r\n---\r\nbody\r\n");
        Assert.Equal("y", fm.Fields["name"]);
        Assert.Contains("body", fm.Body);
    }
}
