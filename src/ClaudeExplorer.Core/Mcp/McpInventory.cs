using System.Text.Json.Nodes;

namespace ClaudeExplorer.Core.Mcp;

/// <summary>How an MCP server is reached.</summary>
public enum McpTransport { Stdio, Http, Sse }

/// <summary>
/// A discovered MCP server, rich enough for the MCP screen: transport, the stdio command/args or the
/// remote url, env vars, and where it was defined (scope or plugin) + the file.
/// </summary>
public sealed record McpServerInfo(
    string Name,
    McpTransport Transport,
    string? Command,
    IReadOnlyList<string> Args,
    string? Url,
    IReadOnlyDictionary<string, string> Env,
    string SourceLabel,
    string SourceFile)
{
    /// <summary>Display endpoint: the remote url, or the stdio command line.</summary>
    public string Endpoint => Url
        ?? (Args.Count > 0 ? $"{Command} {string.Join(' ', Args)}" : Command ?? "");
}

/// <summary>Shared parsing helpers for MCP json (settings/.claude.json use the <c>mcpServers</c>
/// wrapper; plugin <c>.mcp.json</c> places servers at the root).</summary>
public static class McpJson
{
    /// <summary>The object whose properties are server definitions: the <c>mcpServers</c> wrapper if
    /// present, otherwise the root itself when <paramref name="allowRoot"/> (i.e. a <c>.mcp.json</c>).</summary>
    public static JsonObject? ServersObject(JsonObject? root, bool allowRoot)
    {
        if (root is null) return null;
        if (root["mcpServers"] is JsonObject wrapped) return wrapped;
        return allowRoot ? root : null;
    }

    public static McpTransport Transport(JsonObject def)
    {
        var type = (string?)def["type"];
        if (type is not null)
        {
            if (type.Equals("http", StringComparison.OrdinalIgnoreCase)) return McpTransport.Http;
            if (type.Equals("sse", StringComparison.OrdinalIgnoreCase)) return McpTransport.Sse;
            if (type.Equals("stdio", StringComparison.OrdinalIgnoreCase)) return McpTransport.Stdio;
        }
        return def["url"] is not null ? McpTransport.Http : McpTransport.Stdio;
    }
}
