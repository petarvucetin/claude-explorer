using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClaudeExplorer.Core.Mutation;

/// <summary>
/// Structural validator for <c>settings.json</c> content. Not a full JSON-schema engine: it parses
/// the JSON (tolerating comments + trailing commas, exactly like <c>SettingsReader</c>) and checks
/// the shape of the keys this tool understands, so a write can never corrupt a settings file or
/// produce a type the merge engine would silently drop. All problems are collected, not just the
/// first.
/// </summary>
public sealed class SettingsValidator
{
    private static readonly JsonDocumentOptions DocOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public ValidationResult Validate(string content)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(content, nodeOptions: null, documentOptions: DocOptions);
        }
        catch (JsonException ex)
        {
            return ValidationResult.Fail($"Invalid JSON: {ex.Message}");
        }

        if (node is not JsonObject root)
            return ValidationResult.Fail("Settings root must be a JSON object.");

        var errors = new List<string>();

        CheckString(root, "model", "model", errors);
        CheckString(root, "outputStyle", "outputStyle", errors);

        if (root.TryGetPropertyValue("env", out var env) && env is not null)
        {
            if (env is not JsonObject envObj)
                errors.Add("\"env\" must be a JSON object.");
            else
                foreach (var kv in envObj)
                    if (!IsString(kv.Value))
                        errors.Add($"\"env.{kv.Key}\" must be a string.");
        }

        if (root.TryGetPropertyValue("permissions", out var perms) && perms is not null)
        {
            if (perms is not JsonObject permObj)
                errors.Add("\"permissions\" must be a JSON object.");
            else
            {
                CheckStringArray(permObj, "allow", "permissions.allow", errors);
                CheckStringArray(permObj, "deny", "permissions.deny", errors);
                CheckStringArray(permObj, "ask", "permissions.ask", errors);
                CheckString(permObj, "defaultMode", "permissions.defaultMode", errors);
            }
        }

        if (root.TryGetPropertyValue("hooks", out var hooks) && hooks is not null && hooks is not JsonObject)
            errors.Add("\"hooks\" must be a JSON object.");

        return errors.Count == 0 ? ValidationResult.Ok : new ValidationResult(false, errors);
    }

    private static bool IsString(JsonNode? node)
        => node is JsonValue v && v.TryGetValue<string>(out _);

    private static void CheckString(JsonObject obj, string key, string label, List<string> errors)
    {
        if (obj.TryGetPropertyValue(key, out var val) && val is not null && !IsString(val))
            errors.Add($"\"{label}\" must be a string.");
    }

    private static void CheckStringArray(JsonObject obj, string key, string label, List<string> errors)
    {
        if (!obj.TryGetPropertyValue(key, out var val) || val is null) return;
        if (val is not JsonArray arr)
        {
            errors.Add($"\"{label}\" must be an array of strings.");
            return;
        }
        foreach (var item in arr)
            if (!IsString(item))
            {
                errors.Add($"\"{label}\" must contain only strings.");
                break;
            }
    }
}
