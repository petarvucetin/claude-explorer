using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Discovery;
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Mcp;

/// <summary>
/// Reads MCP servers for the MCP screen, with full provenance. Sources: located settings files'
/// <c>mcpServers</c>, <c>~/.claude.json</c>'s <c>mcpServers</c>, the project <c>.mcp.json</c>, and each
/// installed plugin's <c>.mcp.json</c> (server-at-root). Remote, account-managed connectors (the
/// <c>claude.ai</c> ones) live outside any local file and are not discoverable here.
/// </summary>
public sealed class McpInventoryReader
{
    private static readonly JsonDocumentOptions DocOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IFileSystem _fs;
    private readonly InstalledPluginLocator _plugins;

    public McpInventoryReader(IFileSystem fs)
    {
        _fs = fs;
        _plugins = new InstalledPluginLocator(fs);
    }

    public IReadOnlyList<McpServerInfo> Read(string userDir, string projectDir, string? enterprisePath = null)
    {
        var result = new List<McpServerInfo>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in new SettingsLocator(_fs).Locate(userDir, projectDir, enterprisePath))
            Collect(result, seen, file.Path, ScopeLabel(file.Scope), allowRoot: false);

        Collect(result, seen, $"{userDir}/.claude.json", "user", allowRoot: false);
        CollectProjectsBlock(result, seen, $"{userDir}/.claude.json");
        Collect(result, seen, $"{projectDir}/.mcp.json", "project", allowRoot: true);

        foreach (var plugin in _plugins.Locate(userDir))
            Collect(result, seen, $"{plugin.RootPath}/.mcp.json", $"plugin: {plugin.Name}", allowRoot: true);

        return result;
    }

    private void Collect(List<McpServerInfo> into, HashSet<string> seen, string path, string sourceLabel, bool allowRoot)
    {
        var servers = McpJson.ServersObject(TryParse(path), allowRoot);
        if (servers is null) return;

        foreach (var (name, def) in servers)
        {
            if (def is not JsonObject obj) continue;
            if (!seen.Add($"{name}|{path}")) continue;

            var command = (string?)obj["command"];
            var args = obj["args"] is JsonArray arr
                ? arr.Select(a => (string?)a ?? "").Where(a => a.Length > 0).ToList()
                : new List<string>();
            var url = (string?)obj["url"];
            var env = obj["env"] is JsonObject e
                ? e.ToDictionary(kv => kv.Key, kv => (string?)kv.Value ?? "")
                : new Dictionary<string, string>();

            into.Add(new McpServerInfo(name, McpJson.Transport(obj), command, args, url, env, sourceLabel, path));
        }
    }

    private void CollectProjectsBlock(List<McpServerInfo> into, HashSet<string> seen, string claudeJsonPath)
    {
        var root = TryParse(claudeJsonPath);
        if (root?["projects"] is not JsonObject projects) return;

        foreach (var (projectKey, projectValue) in projects)
        {
            if (projectValue is not JsonObject projectObj) continue;
            if (projectObj["mcpServers"] is not JsonObject mcpServers) continue;

            var projectLabel = projectKey.TrimEnd('/').TrimEnd('\\');
            var lastSep = projectLabel.LastIndexOfAny(['/', '\\']);
            var shortName = lastSep >= 0 ? projectLabel[(lastSep + 1)..] : projectLabel;
            var sourceLabel = $"local: {shortName}";

            foreach (var (name, def) in mcpServers)
            {
                if (def is not JsonObject obj) continue;
                // Include projectKey in dedup key so same-named servers in different projects both appear
                if (!seen.Add($"{name}|{claudeJsonPath}|{projectKey}")) continue;

                var command = (string?)obj["command"];
                var args = obj["args"] is JsonArray arr
                    ? arr.Select(a => (string?)a ?? "").Where(a => a.Length > 0).ToList()
                    : new List<string>();
                var url = (string?)obj["url"];
                var env = obj["env"] is JsonObject e
                    ? e.ToDictionary(kv => kv.Key, kv => (string?)kv.Value ?? "")
                    : new Dictionary<string, string>();

                into.Add(new McpServerInfo(name, McpJson.Transport(obj), command, args, url, env, sourceLabel, claudeJsonPath));
            }
        }
    }

    private JsonObject? TryParse(string path)
    {
        if (!_fs.FileExists(path)) return null;
        try { return JsonNode.Parse(_fs.ReadAllText(path), nodeOptions: null, documentOptions: DocOptions) as JsonObject; }
        catch (JsonException) { return null; }
    }

    private static string ScopeLabel(ScopeKind scope) => scope switch
    {
        ScopeKind.Enterprise => "enterprise",
        ScopeKind.Project => "project",
        ScopeKind.Local => "local",
        ScopeKind.Plugin => "plugin",
        _ => "user",
    };
}
