using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClaudeExplorer.App.Util;

/// <summary>
/// Consistent pretty-printing for every piece of JSON the app displays: 2-space indent, real
/// newlines, no sprawl. Use <see cref="Pretty"/> for a parsed node and <see cref="TryPretty"/> for raw
/// file text (which leaves non-JSON — e.g. markdown — untouched).
/// </summary>
public static class JsonFormat
{
    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        IndentSize = 2,
        IndentCharacter = ' ',
    };

    private static readonly JsonDocumentOptions Lenient = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Pretty-print a JSON node; empty string for null.</summary>
    public static string Pretty(JsonNode? node) => node?.ToJsonString(Indented) ?? "";

    /// <summary>Pretty-print <paramref name="text"/> if it is JSON; otherwise return it unchanged.</summary>
    public static string TryPretty(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text ?? "";
        try
        {
            var node = JsonNode.Parse(text, nodeOptions: null, documentOptions: Lenient);
            return node?.ToJsonString(Indented) ?? text;
        }
        catch (JsonException)
        {
            return text;
        }
    }
}
