using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.Core.Hooks;

/// <summary>
/// Extracts and replaces a single matcher-group within a source <c>settings.json</c>'s
/// <c>hooks.&lt;event&gt;</c> array, operating on the raw on-disk text. The user edits one block; the
/// whole file is re-serialized (2-space pretty) so the existing safe-mutation diff/backup/undo operate
/// on the real file. Refusals throw <see cref="MutationException"/>.
/// </summary>
public static class HookBlockEditor
{
    private static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true, IndentSize = 2, IndentCharacter = ' ',
    };

    private static readonly JsonDocumentOptions Lenient = new()
    {
        CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
    };

    public static string ExtractBlock(string sourceText, string evt, int sourceGroupIndex)
    {
        var arr = HookArray(sourceText, evt);
        if (arr is null || sourceGroupIndex < 0 || sourceGroupIndex >= arr.Count)
            throw new MutationException($"Hook block hooks.{evt}[{sourceGroupIndex}] not found.");
        return arr[sourceGroupIndex]!.ToJsonString(Pretty);
    }

    public static string SpliceBlock(string sourceText, string evt, int sourceGroupIndex, string editedBlockJson)
    {
        JsonNode? edited;
        try { edited = JsonNode.Parse(editedBlockJson, documentOptions: Lenient); }
        catch (JsonException ex) { throw new MutationException("Edited hook is not valid JSON: " + ex.Message); }

        if (edited is not JsonObject obj || obj["hooks"] is not JsonArray)
            throw new MutationException("Edited hook must be a JSON object with a \"hooks\" array.");

        if (JsonNode.Parse(sourceText, documentOptions: Lenient) is not JsonObject root)
            throw new MutationException("Source settings is not a JSON object.");

        if ((root["hooks"] as JsonObject)?[evt] is not JsonArray arr)
            throw new MutationException($"Source has no hooks.{evt} array.");

        if (sourceGroupIndex < 0 || sourceGroupIndex >= arr.Count)
            throw new MutationException($"Hook block index {sourceGroupIndex} is out of range.");

        arr[sourceGroupIndex] = edited;
        return root.ToJsonString(Pretty);
    }

    private static JsonArray? HookArray(string sourceText, string evt)
    {
        var root = JsonNode.Parse(sourceText, documentOptions: Lenient) as JsonObject;
        return (root?["hooks"] as JsonObject)?[evt] as JsonArray;
    }
}
