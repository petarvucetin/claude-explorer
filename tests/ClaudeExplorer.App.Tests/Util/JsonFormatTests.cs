using System.Text.Json.Nodes;
using ClaudeExplorer.App.Util;

namespace ClaudeExplorer.App.Tests.Util;

public class JsonFormatTests
{
    [Fact]
    public void Pretty_indents_with_two_spaces_and_newlines()
    {
        var node = JsonNode.Parse("""{"a":[1,2]}""");

        var result = JsonFormat.Pretty(node).Replace("\r\n", "\n");

        Assert.Equal("{\n  \"a\": [\n    1,\n    2\n  ]\n}", result);
    }

    [Fact]
    public void Pretty_scalar_stays_single_line()
    {
        Assert.Equal("\"opus\"", JsonFormat.Pretty(JsonValue.Create("opus")));
        Assert.Equal("", JsonFormat.Pretty(null));
    }

    [Fact]
    public void TryPretty_formats_minified_json()
    {
        var result = JsonFormat.TryPretty("""{"x":1,"y":2}""").Replace("\r\n", "\n");
        Assert.Equal("{\n  \"x\": 1,\n  \"y\": 2\n}", result);
    }

    [Fact]
    public void TryPretty_leaves_non_json_untouched()
    {
        const string md = "# Title\n\nSome **markdown**, not JSON.";
        Assert.Equal(md, JsonFormat.TryPretty(md));
    }

    [Fact]
    public void TryPretty_accepts_jsonc_comments_and_trailing_commas()
    {
        var result = JsonFormat.TryPretty("{ \"a\": 1, /* note */ }").Replace("\r\n", "\n");
        Assert.Equal("{\n  \"a\": 1\n}", result);
    }
}
