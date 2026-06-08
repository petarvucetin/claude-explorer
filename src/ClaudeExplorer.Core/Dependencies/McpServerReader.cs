using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Discovery;
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Mcp;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Dependencies;

/// <summary>
/// An MCP server definition relevant to dependency health. Only stdio servers (those with a
/// <c>command</c>) carry an executable dependency; url/sse servers have <c>Command == null</c>.
/// </summary>
public sealed record McpServer(string Name, string? Command, IReadOnlyList<string> Args, ScopeKind Scope);

/// <summary>
/// Reader for MCP server definitions used by the dependency health check. Pulls servers from the
/// located settings files' <c>mcpServers</c>, <c>~/.claude.json</c>'s <c>mcpServers</c>, the project
/// <c>.mcp.json</c>, and each installed plugin's <c>.mcp.json</c>. Settings/<c>.claude.json</c> use the
/// <c>mcpServers</c> wrapper; <c>.mcp.json</c> files may also place servers at the root
/// (the plugin shape, e.g. <c>{ "linear": { … } }</c>). Malformed/missing sources are skipped.
/// </summary>
public sealed class McpServerReader
{
    private static readonly JsonDocumentOptions DocOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IFileSystem _fs;
    private readonly InstalledPluginLocator _plugins;

    public McpServerReader(IFileSystem fs)
    {
        _fs = fs;
        _plugins = new InstalledPluginLocator(fs);
    }

    public IReadOnlyList<McpServer> Read(string userDir, string projectDir, string? enterprisePath = null)
    {
        var servers = new List<McpServer>();

        foreach (var file in new SettingsLocator(_fs).Locate(userDir, projectDir, enterprisePath))
            servers.AddRange(ReadServers(TryParse(file.Path), file.Scope, allowRoot: false));

        servers.AddRange(ReadServers(TryParse($"{userDir}/.claude.json"), ScopeKind.User, allowRoot: false));
        servers.AddRange(ReadServers(TryParse($"{projectDir}/.mcp.json"), ScopeKind.Project, allowRoot: true));

        foreach (var plugin in _plugins.Locate(userDir))
            servers.AddRange(ReadServers(TryParse($"{plugin.RootPath}/.mcp.json"), ScopeKind.Plugin, allowRoot: true));

        return servers;
    }

    private JsonObject? TryParse(string path)
    {
        if (!_fs.FileExists(path)) return null;
        try { return JsonNode.Parse(_fs.ReadAllText(path), nodeOptions: null, documentOptions: DocOptions) as JsonObject; }
        catch (JsonException) { return null; }
    }

    private static IEnumerable<McpServer> ReadServers(JsonObject? root, ScopeKind scope, bool allowRoot)
    {
        var servers = McpJson.ServersObject(root, allowRoot);
        if (servers is null) yield break;

        foreach (var (name, def) in servers)
        {
            if (def is not JsonObject obj) continue;
            var command = (string?)obj["command"];
            var args = obj["args"] is JsonArray arr
                ? arr.Select(a => (string?)a ?? "").Where(a => a.Length > 0).ToList()
                : new List<string>();
            yield return new McpServer(name, command, args, scope);
        }
    }
}
