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

        var text = content.TrimStart('﻿').Replace("\r\n", "\n").Replace("\r", "\n");
        if (!text.StartsWith("---\n", StringComparison.Ordinal))
            return new FrontmatterResult(fields, text);

        int close;          // index of the '\n' that begins the closing fence line
        int bodyStart;
        var idx = text.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (idx >= 0)
        {
            close = idx;
            bodyStart = idx + 5; // length of "\n---\n"
        }
        else if (text.EndsWith("\n---", StringComparison.Ordinal))
        {
            close = text.Length - 4;
            bodyStart = text.Length; // no body
        }
        else
        {
            return new FrontmatterResult(fields, text);
        }
        var block = text.Substring(4, close - 4);
        var body = bodyStart <= text.Length ? text.Substring(bodyStart) : "";

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
