using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Discovery;
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Dependencies;

/// <summary>
/// An MCP server definition relevant to dependency health. Only stdio servers (those with a
/// <c>command</c>) carry an executable dependency; url/sse servers have <c>Command == null</c>.
/// </summary>
public sealed record McpServer(string Name, string? Command, IReadOnlyList<string> Args, ScopeKind Scope);

/// <summary>
/// Minimal reader for MCP server definitions: pulls the <c>mcpServers</c> object from the located
/// settings files and from a project-root <c>.mcp.json</c>. Malformed/missing sources are skipped.
/// (Full MCP/plugin parsing — including <c>~/.claude.json</c> — is a later phase.)
/// </summary>
public sealed class McpServerReader
{
    private static readonly JsonDocumentOptions DocOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IFileSystem _fs;

    public McpServerReader(IFileSystem fs) => _fs = fs;

    public IReadOnlyList<McpServer> Read(string userDir, string projectDir, string? enterprisePath = null)
    {
        var servers = new List<McpServer>();

        foreach (var file in new SettingsLocator(_fs).Locate(userDir, projectDir, enterprisePath))
            servers.AddRange(ReadFrom(TryParse(file.Path), file.Scope));

        var mcpJson = $"{projectDir}/.mcp.json";
        if (_fs.FileExists(mcpJson))
            servers.AddRange(ReadFrom(TryParse(mcpJson), ScopeKind.Project));

        return servers;
    }

    private JsonObject? TryParse(string path)
    {
        if (!_fs.FileExists(path)) return null;
        try { return JsonNode.Parse(_fs.ReadAllText(path), nodeOptions: null, documentOptions: DocOptions) as JsonObject; }
        catch (JsonException) { return null; }
    }

    private static IEnumerable<McpServer> ReadFrom(JsonObject? root, ScopeKind scope)
    {
        if (root?["mcpServers"] is not JsonObject servers) yield break;

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
