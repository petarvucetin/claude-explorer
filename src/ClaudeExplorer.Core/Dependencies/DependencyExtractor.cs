using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Dependencies;

/// <summary>
/// Turns discovered config into a deduped list of executable dependencies: the <c>command</c>
/// strings inside <c>hooks.*</c> settings, plus the <c>command</c> of each stdio MCP server.
/// Deduped by runtime name; each ref lists the distinct sources that referenced it.
/// </summary>
public sealed class DependencyExtractor
{
    private const string HookPrefix = "hooks.";

    public IReadOnlyList<DependencyRef> Extract(
        EffectiveConfig config,
        IReadOnlyList<McpServer> mcpServers,
        IReadOnlyList<string>? pluginRoots = null)
    {
        var roots = pluginRoots ?? Array.Empty<string>();
        var raw = new List<(string Name, string Raw, string Source, string? ResolvedPath)>();

        foreach (var setting in config.Settings)
        {
            if (!setting.Key.StartsWith(HookPrefix, StringComparison.Ordinal)) continue;
            var evt = setting.Key.Substring(HookPrefix.Length);

            // Iterate contributions (not the merged value) so each command keeps its source file —
            // needed to resolve ${CLAUDE_PLUGIN_ROOT} against the plugin that defined it.
            foreach (var contribution in setting.Contributions)
                foreach (var command in CollectCommands(contribution.Value))
                {
                    var exe = ExecutableExtractor.Extract(command);
                    if (exe is null) continue;
                    var resolved = PluginScriptResolver.Resolve(command, contribution.Origin.FilePath, roots);
                    raw.Add((exe, command, $"hook:{evt}", resolved));
                }
        }

        foreach (var server in mcpServers)
        {
            if (server.Command is null) continue;
            var exe = ExecutableExtractor.Extract(server.Command);
            if (exe is null) continue;
            var rawCmd = server.Args.Count > 0
                ? $"{server.Command} {string.Join(' ', server.Args)}"
                : server.Command;
            // Same resolution as hooks: a plugin server whose command is itself a ${CLAUDE_PLUGIN_ROOT}
            // path is a file check, not a PATH lookup. (Templates usually sit in args, where the command
            // is an ordinary runtime like node/uvx — those still resolve on PATH and are unaffected.)
            var resolved = PluginScriptResolver.Resolve(server.Command, server.OriginFile, roots);
            raw.Add((exe, rawCmd, $"mcp:{server.Name}", resolved));
        }

        return raw
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new DependencyRef(
                Name: g.Key,
                Raw: g.First().Raw,
                ReferencedBy: g.Select(x => x.Source)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToList(),
                ResolvedPath: g.Select(x => x.ResolvedPath).FirstOrDefault(p => p is not null)))
            .OrderBy(d => d.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Recursively collects the value of every property literally named "command".</summary>
    private static IEnumerable<string> CollectCommands(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    if (key == "command" && value is JsonValue v
                        && v.TryGetValue<string>(out var s) && s.Length > 0)
                        yield return s;
                    else
                        foreach (var c in CollectCommands(value)) yield return c;
                }
                break;
            case JsonArray arr:
                foreach (var item in arr)
                    foreach (var c in CollectCommands(item)) yield return c;
                break;
        }
    }
}
