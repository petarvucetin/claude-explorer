namespace ClaudeExplorer.Core.Artifacts;

/// <summary>Result of parsing a markdown file's YAML-style frontmatter.</summary>
public sealed record FrontmatterResult(IReadOnlyDictionary<string, string> Fields, string Body);

/// <summary>
/// Minimal frontmatter parser: reads a leading <c>---</c>…<c>---</c> block of `key: value`
/// lines (surrounding quotes stripped). Does not support nested YAML, lists, or multi-line values.
/// </summary>
public static class Frontmatter
{
    public static FrontmatterResult Parse(string? content)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(content))
            return new FrontmatterResult(fields, content ?? "");

        var text = content.Replace("\r\n", "\n").Replace("\r", "\n");
        if (!text.StartsWith("---\n", StringComparison.Ordinal))
            return new FrontmatterResult(fields, text);

        var close = text.IndexOf("\n---", 4, StringComparison.Ordinal);
        if (close < 0)
            return new FrontmatterResult(fields, text);

        var block = text.Substring(4, close - 4);
        var nl = text.IndexOf('\n', close + 1);
        var body = nl >= 0 ? text.Substring(nl + 1) : "";

        foreach (var rawLine in block.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            var key = line.Substring(0, colon).Trim();
            var value = Unquote(line.Substring(colon + 1).Trim());
            if (key.Length > 0 && !fields.ContainsKey(key))
                fields[key] = value;
        }

        return new FrontmatterResult(fields, body);
    }

    private static string Unquote(string s)
        => s.Length >= 2 && ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\''))
            ? s.Substring(1, s.Length - 2)
            : s;
}
