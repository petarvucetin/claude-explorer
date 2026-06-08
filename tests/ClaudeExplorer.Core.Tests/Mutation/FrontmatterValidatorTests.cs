using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.Core.Tests.Mutation;

public class FrontmatterValidatorTests
{
    [Fact]
    public void Valid_frontmatter_with_required_fields_passes()
    {
        var doc = "---\nname: my-skill\ndescription: Does a thing\n---\nBody text.";

        var result = new FrontmatterValidator().Validate(doc);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Missing_frontmatter_block_is_invalid()
    {
        var result = new FrontmatterValidator().Validate("Just a body, no frontmatter.");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("frontmatter"));
    }

    [Fact]
    public void Missing_required_field_is_invalid()
    {
        var doc = "---\nname: my-skill\n---\nBody.";

        var result = new FrontmatterValidator().Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("description"));
    }

    [Fact]
    public void Blank_required_value_is_invalid()
    {
        var doc = "---\nname: my-skill\ndescription:   \n---\nBody.";

        var result = new FrontmatterValidator().Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("description"));
    }

    [Fact]
    public void Custom_required_fields_are_enforced()
    {
        var doc = "---\nname: cmd\n---\nBody.";

        var result = new FrontmatterValidator("name").Validate(doc);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Crlf_line_endings_are_handled()
    {
        var doc = "---\r\nname: x\r\ndescription: y\r\n---\r\nBody.";

        Assert.True(new FrontmatterValidator().Validate(doc).IsValid);
    }
}
