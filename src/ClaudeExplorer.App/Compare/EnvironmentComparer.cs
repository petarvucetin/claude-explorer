using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.App.Compare;

/// <summary>Pure diff of two environment snapshots into per-category rows. No IO — tested by
/// constructing Core records directly.</summary>
public static class EnvironmentComparer
{
    public static EnvironmentComparison Compare(EnvironmentSnapshot a, EnvironmentSnapshot b)
        => new(new List<CompareCategory>
        {
            BuildCategory("Settings", SettingsMap(a), SettingsMap(b)),
            BuildCategory("Commands", ArtifactMap(a, ArtifactKind.Command), ArtifactMap(b, ArtifactKind.Command)),
            BuildCategory("Skills", ArtifactMap(a, ArtifactKind.Skill), ArtifactMap(b, ArtifactKind.Skill)),
            BuildCategory("Agents", ArtifactMap(a, ArtifactKind.Subagent), ArtifactMap(b, ArtifactKind.Subagent)),
            BuildCategory("MCP", McpMap(a), McpMap(b)),
            BuildCategory("Memory", MemoryMap(a), MemoryMap(b)),
            BuildCategory("Plugins", PluginMap(a), PluginMap(b), viewOnly: true),
            BuildCategory("Dependencies", DepMap(a), DepMap(b), viewOnly: true),
        });

    private static CompareCategory BuildCategory(
        string name, IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b,
        bool viewOnly = false)
    {
        var rows = new List<CompareRow>();
        foreach (var key in a.Keys.Union(b.Keys).OrderBy(k => k, StringComparer.Ordinal))
        {
            var hasA = a.TryGetValue(key, out var va);
            var hasB = b.TryGetValue(key, out var vb);
            var status = (hasA, hasB) switch
            {
                (true, true) => va == vb ? DiffStatus.Same : DiffStatus.Differs,
                (true, false) => DiffStatus.OnlyA,
                _ => DiffStatus.OnlyB,
            };
            rows.Add(new CompareRow(key, status, hasA ? va : null, hasB ? vb : null));
        }
        return new CompareCategory(name, rows, viewOnly);
    }

    private static Dictionary<string, string> SettingsMap(EnvironmentSnapshot s)
        => s.Settings.ToDictionary(x => x.Key, x => Canonical(x.Value), StringComparer.Ordinal);

    private static Dictionary<string, string> ArtifactMap(EnvironmentSnapshot s, ArtifactKind kind)
        => s.Artifacts.OfKind(kind).ToDictionary(a => a.Winner.Name, a => a.Winner.Summary ?? "", StringComparer.Ordinal);

    private static Dictionary<string, string> McpMap(EnvironmentSnapshot s)
        => s.Mcp.GroupBy(m => m.Name, StringComparer.Ordinal)
               .ToDictionary(g => g.Key, g => $"{g.First().Command} {string.Join(" ", g.First().Args)}".Trim(), StringComparer.Ordinal);

    private static Dictionary<string, string> PluginMap(EnvironmentSnapshot s)
        => s.Plugins.Distinct(StringComparer.Ordinal).ToDictionary(p => p, _ => "installed", StringComparer.Ordinal);

    private static Dictionary<string, string> DepMap(EnvironmentSnapshot s)
        => s.Dependencies.Results.GroupBy(r => r.Ref.Name, StringComparer.Ordinal)
               .ToDictionary(g => g.Key, g => g.First().Status.Kind.ToString(), StringComparer.Ordinal);

    private static Dictionary<string, string> MemoryMap(EnvironmentSnapshot s)
        => s.Memory.ToDictionary(kv => kv.Key, kv => Descriptor(kv.Value), StringComparer.Ordinal);

    private static string Descriptor(string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))[..8];
        return $"present · {bytes.Length} B · {hash}";
    }

    /// <summary>Canonical comparable form of a setting value; arrays compare as sorted sets.</summary>
    private static string Canonical(JsonNode? node)
    {
        if (node is null) return "";
        if (node is JsonArray arr)
            return "[" + string.Join(",", arr.Select(e => e?.ToJsonString() ?? "null").OrderBy(x => x, StringComparer.Ordinal)) + "]";
        return node.ToJsonString();
    }
}
