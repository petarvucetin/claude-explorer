using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Plugins;

/// <summary>
/// Reads plugin install roots from <c>installed_plugins.json</c> — each root is the value
/// <c>${CLAUDE_PLUGIN_ROOT}</c> expands to for that plugin. Like <see cref="PluginInventoryReader"/>,
/// paths are derived env-correctly from <paramref name="userDir"/>
/// (<c>.claude/plugins/cache/&lt;marketplace&gt;/&lt;plugin&gt;/&lt;version&gt;</c>) rather than trusting
/// the absolute <c>installPath</c> baked into the file. Missing/malformed registry → empty.
/// </summary>
public sealed class PluginRootReader
{
    private static readonly JsonDocumentOptions DocOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IFileSystem _fs;

    public PluginRootReader(IFileSystem fs) => _fs = fs;

    public IReadOnlyList<string> ReadRoots(string userDir)
    {
        var pluginsDir = $"{userDir}/.claude/plugins";
        var roots = new List<string>();

        if (TryParse($"{pluginsDir}/installed_plugins.json")?["plugins"] is not JsonObject installed)
            return roots;

        foreach (var (key, value) in installed)
        {
            var (name, marketplace) = SplitKey(key);
            if ((value as JsonArray)?.FirstOrDefault() is not JsonObject entry) continue;
            var version = (string?)entry["version"] ?? "unknown";
            roots.Add($"{pluginsDir}/cache/{marketplace}/{name}/{version}");
        }

        return roots;
    }

    private static (string Name, string Marketplace) SplitKey(string key)
    {
        var at = key.LastIndexOf('@');
        return at > 0 ? (key[..at], key[(at + 1)..]) : (key, "");
    }

    private JsonObject? TryParse(string path)
    {
        if (!_fs.FileExists(path)) return null;
        try { return JsonNode.Parse(_fs.ReadAllText(path), nodeOptions: null, documentOptions: DocOptions) as JsonObject; }
        catch (JsonException) { return null; }
    }
}
