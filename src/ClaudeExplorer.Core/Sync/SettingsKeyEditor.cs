using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.Core.Sync;

/// <summary>Set or remove a single top-level key in a settings.json document, re-serialized pretty
/// (2-space). The top-level analogue of <c>HookBlockEditor</c>; refusals throw <see cref="MutationException"/>.</summary>
public static class SettingsKeyEditor
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true, IndentSize = 2, IndentCharacter = ' ' };
    private static readonly JsonDocumentOptions Lenient = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static string SetKey(string sourceText, string key, string valueJson)
    {
        JsonNode? value;
        try { value = JsonNode.Parse(valueJson, documentOptions: Lenient); }
        catch (JsonException ex) { throw new MutationException($"Value for \"{key}\" is not valid JSON: {ex.Message}"); }

        var root = ParseRoot(sourceText);
        root[key] = value;
        return root.ToJsonString(Pretty);
    }

    public static string RemoveKey(string sourceText, string key)
    {
        var root = ParseRoot(sourceText);
        root.Remove(key);
        return root.ToJsonString(Pretty);
    }

    public static string GetKey(string sourceText, string key)
        => ParseRoot(sourceText)[key]?.ToJsonString(Pretty)
           ?? throw new MutationException($"Key \"{key}\" not found.");

    private static JsonObject ParseRoot(string sourceText)
    {
        if (string.IsNullOrWhiteSpace(sourceText)) return new JsonObject();
        try
        {
            return JsonNode.Parse(sourceText, documentOptions: Lenient) as JsonObject
                   ?? throw new MutationException("Settings root is not a JSON object.");
        }
        catch (JsonException ex) { throw new MutationException($"Invalid settings JSON: {ex.Message}"); }
    }
}
