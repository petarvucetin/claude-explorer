using ClaudeExplorer.Core.Rendering;

namespace ClaudeExplorer.Core.Tests.Rendering;

public class MarkdownRendererTests
{
    private readonly MarkdownRenderer _renderer = new();

    [Fact]
    public void ToHtml_null_or_empty_returns_empty_string()
    {
        Assert.Equal("", _renderer.ToHtml(null));
        Assert.Equal("", _renderer.ToHtml(""));
    }

    [Fact]
    public void ToHtml_heading_emits_h1()
    {
        var html = _renderer.ToHtml("# Title");
        Assert.Contains("<h1", html);
        Assert.Contains("Title", html);
    }

    [Fact]
    public void ToHtml_fenced_code_block_emits_pre_code()
    {
        var html = _renderer.ToHtml("```js\nconst x = 1;\n```");
        Assert.Contains("<pre>", html);
        Assert.Contains("<code", html);
    }

    [Fact]
    public void ToHtml_inline_code_emits_code_tag()
    {
        var html = _renderer.ToHtml("use `npx` here");
        Assert.Contains("<code>npx</code>", html);
    }

    [Fact]
    public void ToHtml_pipe_table_emits_table()
    {
        var md = "| A | B |\n| - | - |\n| 1 | 2 |";
        var html = _renderer.ToHtml(md);
        Assert.Contains("<table>", html);
        Assert.Contains("<th>", html);
        Assert.Contains("<td>", html);
    }

    [Fact]
    public void ToHtml_disables_raw_inline_html_so_script_is_escaped()
    {
        var html = _renderer.ToHtml("hello <script>alert(1)</script> world");
        // Raw HTML disabled: the script must NOT survive as a live tag.
        Assert.DoesNotContain("<script>", html);
        // It should appear escaped instead.
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void Render_with_frontmatter_returns_fields_and_body_html_without_frontmatter()
    {
        var content = "---\nname: graphify\ndescription: a graph tool\n---\n# Heading\nbody text\n";
        var result = _renderer.Render(content);

        Assert.Equal("graphify", result.Fields["name"]);
        Assert.Equal("a graph tool", result.Fields["description"]);
        Assert.Contains("<h1", result.Html);
        Assert.Contains("body text", result.Html);
        // Frontmatter keys must not bleed into the rendered body.
        Assert.DoesNotContain("name:", result.Html);
        Assert.DoesNotContain("description:", result.Html);
    }

    [Fact]
    public void Render_plain_markdown_returns_empty_fields()
    {
        var result = _renderer.Render("# Just a title\nsome content");

        Assert.Empty(result.Fields);
        Assert.Contains("<h1", result.Html);
        Assert.Contains("some content", result.Html);
    }

    [Fact]
    public void Render_null_returns_empty_fields_and_empty_html()
    {
        var result = _renderer.Render(null);

        Assert.Empty(result.Fields);
        Assert.Equal("", result.Html);
    }
}
