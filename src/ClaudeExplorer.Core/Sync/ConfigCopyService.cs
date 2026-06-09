using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Hooks;
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.Core.Sync;

// ── Records ─────────────────────────────────────────────────────────────────

/// <summary>Describes what to copy/move: which category, which key/name, and the resolved
/// source and target paths.  Only the path pair relevant to the category needs to be filled.</summary>
public sealed record CopyRequest(
    string Category, string Key,
    string? SourceSettingsPath = null, string? TargetSettingsPath = null,
    string? SourceFilePath = null, string? TargetFilePath = null,
    string? SourceMcpPath = null, string? TargetMcpPath = null);

/// <summary>The source edit that removes the item (for Move operations).</summary>
public sealed record SourceRemoval(string Path, string NewContent);

/// <summary>The resulting plan: the target to write plus an optional source removal.</summary>
public sealed record CopyPlan(
    string TargetPath, string NewTargetContent, bool TargetIsJson,
    SourceRemoval? SourceRemoval = null);

// ── Service ──────────────────────────────────────────────────────────────────

/// <summary>Produces a <see cref="CopyPlan"/> for each supported category without touching the
/// file system.  The App layer applies the plan through <c>SafeMutationService</c>.</summary>
public sealed class ConfigCopyService
{
    private static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true, IndentSize = 2, IndentCharacter = ' ',
    };

    private static readonly JsonDocumentOptions Lenient = new()
    {
        CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
    };

    private readonly IFileSystem _fs;

    public ConfigCopyService(IFileSystem fs) => _fs = fs;

    // ── Public API ───────────────────────────────────────────────────────────

    public CopyPlan PlanCopy(CopyRequest req) => Dispatch(req, move: false);

    public CopyPlan PlanMove(CopyRequest req) => Dispatch(req, move: true);

    // ── Dispatch ─────────────────────────────────────────────────────────────

    private CopyPlan Dispatch(CopyRequest req, bool move) =>
        req.Category switch
        {
            "Settings"  => CopySettings(req, move),
            "Memory" or "Commands" or "Skills" or "Subagents" => CopyFile(req, move),
            "MCP"       => CopyMcp(req, move),
            "Hooks"     => CopyHooks(req, move),
            _ => throw new MutationException($"Unknown copy category \"{req.Category}\"."),
        };

    // ── Settings ─────────────────────────────────────────────────────────────

    private CopyPlan CopySettings(CopyRequest req, bool move)
    {
        var sourcePath  = req.SourceSettingsPath!;
        var targetPath  = req.TargetSettingsPath!;
        var sourceText  = ReadText(sourcePath, "{}");
        var targetText  = ReadText(targetPath, "{}");
        var value       = SettingsKeyEditor.GetKey(sourceText, req.Key);
        var newTarget   = SettingsKeyEditor.SetKey(targetText, req.Key, value);

        SourceRemoval? removal = move
            ? new SourceRemoval(sourcePath, SettingsKeyEditor.RemoveKey(sourceText, req.Key))
            : null;

        return new CopyPlan(targetPath, newTarget, TargetIsJson: true, removal);
    }

    // ── Memory / Commands / Skills / Subagents (file copy) ───────────────────

    private CopyPlan CopyFile(CopyRequest req, bool move)
    {
        var sourcePath = req.SourceFilePath!;
        var targetPath = req.TargetFilePath!;
        var content    = ReadText(sourcePath, "");

        // Empty string is the delete sentinel — the apply layer deletes when
        // TargetIsJson==false and SourceRemoval.NewContent is empty.
        SourceRemoval? removal = move ? new SourceRemoval(sourcePath, "") : null;

        return new CopyPlan(targetPath, content, TargetIsJson: false, removal);
    }

    // ── MCP (copy a named server entry between MCP JSON files) ───────────────

    private CopyPlan CopyMcp(CopyRequest req, bool move)
    {
        var sourcePath = req.SourceMcpPath!;
        var targetPath = req.TargetMcpPath!;
        var serverName = req.Key;

        var sourceText = ReadText(sourcePath, "{}");
        var targetText = ReadText(targetPath, "{}");

        var sourceRoot  = ParseJsonObject(sourceText, sourcePath);
        var targetRoot  = ParseJsonObject(targetText, targetPath);

        // Locate the server entry — supports both `{ "mcpServers": { name: … } }` and
        // name-at-root shapes (used by single-server plugin .mcp.json files).
        JsonNode? serverNode = null;
        if (sourceRoot["mcpServers"] is JsonObject mcpServers && mcpServers[serverName] is JsonNode n)
            serverNode = n;
        else if (sourceRoot[serverName] is JsonNode rootEntry)
            serverNode = rootEntry;

        if (serverNode is null)
            throw new MutationException($"MCP server \"{serverName}\" not found in {sourcePath}.");

        // Write into target under the mcpServers wrapper (create if absent).
        var targetServers = (targetRoot["mcpServers"] as JsonObject) ?? new JsonObject();
        targetServers[serverName] = serverNode.DeepClone();
        targetRoot["mcpServers"] = targetServers;
        var newTarget = targetRoot.ToJsonString(Pretty);

        SourceRemoval? removal = null;
        if (move)
        {
            // Remove from source: try mcpServers wrapper first, then root.
            if (sourceRoot["mcpServers"] is JsonObject src && src.ContainsKey(serverName))
                src.Remove(serverName);
            else
                sourceRoot.Remove(serverName);
            removal = new SourceRemoval(sourcePath, sourceRoot.ToJsonString(Pretty));
        }

        return new CopyPlan(targetPath, newTarget, TargetIsJson: true, removal);
    }

    // ── Hooks (key = "<event>#<sourceGroupIndex>") ────────────────────────────

    private CopyPlan CopyHooks(CopyRequest req, bool move)
    {
        var targetPath = req.TargetSettingsPath!;
        var sourcePath = req.SourceSettingsPath!;

        // Parse key: "PreToolUse#0"
        var sep = req.Key.LastIndexOf('#');
        if (sep < 0)
            throw new MutationException($"Hooks key must be \"<event>#<index>\", got \"{req.Key}\".");
        var evt   = req.Key[..sep];
        if (!int.TryParse(req.Key[(sep + 1)..], out var idx))
            throw new MutationException($"Hooks key index is not an integer in \"{req.Key}\".");

        var sourceText = ReadText(sourcePath, "{}");
        var targetText = ReadText(targetPath, "{}");

        // Extract the block JSON from the source hooks array.
        var blockJson = HookBlockEditor.ExtractBlock(sourceText, evt, idx);

        // Parse block; it is the raw group object (no wrapper).
        JsonNode? blockNode;
        try { blockNode = JsonNode.Parse(blockJson, documentOptions: Lenient); }
        catch (JsonException ex)
        { throw new MutationException("Extracted hook block is not valid JSON: " + ex.Message); }

        // Append into target hooks.<event> array (create if absent).
        var targetRoot = ParseJsonObject(targetText, targetPath);
        var hooksObj   = (targetRoot["hooks"] as JsonObject) ?? new JsonObject();
        var evtArr     = (hooksObj[evt] as JsonArray) ?? new JsonArray();
        evtArr.Add(blockNode!.DeepClone());
        hooksObj[evt]       = evtArr;
        targetRoot["hooks"] = hooksObj;
        var newTarget = targetRoot.ToJsonString(Pretty);

        SourceRemoval? removal = null;
        if (move)
        {
            // Remove the group from the source array by splicing it out.
            var newSource = RemoveHookGroup(sourceText, evt, idx);
            removal = new SourceRemoval(sourcePath, newSource);
        }

        return new CopyPlan(targetPath, newTarget, TargetIsJson: true, removal);
    }

    /// <summary>Remove the hook group at <paramref name="idx"/> from <c>hooks.&lt;evt&gt;</c>
    /// in <paramref name="sourceText"/>, returning the re-serialized settings JSON.</summary>
    private static string RemoveHookGroup(string sourceText, string evt, int idx)
    {
        var root = JsonNode.Parse(sourceText, documentOptions: Lenient) as JsonObject
                   ?? throw new MutationException("Source settings is not a JSON object.");

        if (root["hooks"] is not JsonObject hooksObj ||
            hooksObj[evt] is not JsonArray arr)
            throw new MutationException($"Source has no hooks.{evt} array.");

        if (idx < 0 || idx >= arr.Count)
            throw new MutationException($"Hook group index {idx} is out of range.");

        arr.RemoveAt(idx);
        return root.ToJsonString(Pretty);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private string ReadText(string path, string fallback)
        => _fs.FileExists(path) ? _fs.ReadAllText(path) : fallback;

    private static JsonObject ParseJsonObject(string text, string pathForError)
    {
        if (string.IsNullOrWhiteSpace(text)) return new JsonObject();
        try
        {
            return JsonNode.Parse(text, documentOptions: Lenient) as JsonObject
                   ?? throw new MutationException($"File is not a JSON object: {pathForError}");
        }
        catch (JsonException ex)
        { throw new MutationException($"Invalid JSON in {pathForError}: {ex.Message}"); }
    }
}
