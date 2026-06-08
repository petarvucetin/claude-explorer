using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Catalog;
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Mcp;

namespace ClaudeExplorer.Core.Plugins;

/// <summary>
/// Reads the installed-plugin inventory for the Plugins screen, composing
/// <c>installed_plugins.json</c> (name@marketplace, version, scope), <c>known_marketplaces.json</c>
/// (source repo + trust), the user <c>settings.json</c> <c>enabledPlugins</c> map, and a per-plugin
/// "provides" count scanned from the on-disk cache. Local only. Install paths are derived from
/// <paramref name="userDir"/> (env-correct, incl. WSL) rather than the absolute paths in the JSON.
/// </summary>
public sealed class PluginInventoryReader
{
    private static readonly JsonDocumentOptions DocOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IFileSystem _fs;
    private readonly ArtifactDiscoverer _artifacts;

    public PluginInventoryReader(IFileSystem fs)
    {
        _fs = fs;
        _artifacts = new ArtifactDiscoverer(fs);
    }

    public PluginInventory Read(string userDir)
    {
        var pluginsDir = $"{userDir}/.claude/plugins";
        var enabled = ParseEnabled($"{userDir}/.claude/settings.json");
        var marketplaceRepos = ParseMarketplaces($"{pluginsDir}/known_marketplaces.json");

        var plugins = new List<InstalledPluginInfo>();
        var installedRoot = TryParse($"{pluginsDir}/installed_plugins.json")?["plugins"] as JsonObject;
        if (installedRoot is not null)
        {
            foreach (var (key, value) in installedRoot)
            {
                var (name, marketplace) = SplitKey(key);
                var entry = (value as JsonArray)?.FirstOrDefault() as JsonObject;
                if (entry is null) continue;

                var version = (string?)entry["version"] ?? "unknown";
                var scope = (string?)entry["scope"] ?? "user";
                var installPath = $"{pluginsDir}/cache/{marketplace}/{name}/{version}";
                var isEnabled = !enabled.TryGetValue(key, out var e) || e;   // default enabled unless explicitly false

                plugins.Add(new InstalledPluginInfo(
                    name, marketplace, version, scope, installPath, isEnabled,
                    CountProvides(name, installPath),
                    MarketplaceTrust.Classify(marketplace, null)));
            }
        }

        var marketplaces = marketplaceRepos
            .Select(m => new MarketplaceInfo(
                m.Key, m.Value, MarketplaceTrust.Classify(m.Key, null),
                plugins.Count(p => p.Marketplace == m.Key)))
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        plugins.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return new PluginInventory(plugins, marketplaces);
    }

    private ProvidesCounts CountProvides(string name, string installPath)
    {
        var artifacts = _artifacts.Discover("", null, new[] { new PluginLocation(name, installPath) });
        var hooks = CountHookEvents($"{installPath}/hooks/hooks.json");
        var mcp = CountMcpServers($"{installPath}/.mcp.json");
        return new ProvidesCounts(
            artifacts.Count(a => a.Kind == ArtifactKind.Command),
            artifacts.Count(a => a.Kind == ArtifactKind.Skill),
            artifacts.Count(a => a.Kind == ArtifactKind.Subagent),
            hooks, mcp);
    }

    private int CountHookEvents(string path)
        => TryParse(path)?["hooks"] is JsonObject hooks ? hooks.Count : 0;

    private int CountMcpServers(string path)
        => McpJson.ServersObject(TryParse(path), allowRoot: true)?.Count ?? 0;

    private Dictionary<string, bool> ParseEnabled(string settingsPath)
    {
        var map = new Dictionary<string, bool>(StringComparer.Ordinal);
        if (TryParse(settingsPath)?["enabledPlugins"] is JsonObject ep)
            foreach (var (k, v) in ep)
                if (v is JsonValue jv && jv.TryGetValue<bool>(out var b)) map[k] = b;
        return map;
    }

    private Dictionary<string, string?> ParseMarketplaces(string path)
    {
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (TryParse(path) is JsonObject root)
            foreach (var (name, def) in root)
                map[name] = (def as JsonObject)?["source"] is JsonObject src ? (string?)src["repo"] : null;
        return map;
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
