namespace ClaudeExplorer.Core.Dependencies;

/// <summary>
/// Expands a <c>${CLAUDE_PLUGIN_ROOT}</c>-templated command into the absolute script path it points
/// to. The variable is the install root of the plugin that <em>defines</em> the command, so it is
/// resolved against the plugin root that owns the defining file (its provenance origin) rather than
/// guessed. Returns <c>null</c> for ordinary commands or when no owning root is known.
/// </summary>
public static class PluginScriptResolver
{
    private const string Variable = "${CLAUDE_PLUGIN_ROOT}";

    public static string? Resolve(string command, string originFilePath, IReadOnlyList<string> pluginRoots)
    {
        var token = ExecutableExtractor.FirstToken(command);
        if (token.Length == 0 || !token.Contains(Variable, StringComparison.Ordinal)) return null;

        var origin = Normalize(originFilePath);
        var owningRoot = pluginRoots
            .Select(Normalize)
            .Where(root => origin.StartsWith(root + "/", StringComparison.Ordinal))
            .OrderByDescending(root => root.Length)     // deepest owning root wins if any nest
            .FirstOrDefault();
        if (owningRoot is null) return null;

        return Normalize(token.Replace(Variable, owningRoot, StringComparison.Ordinal));
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimEnd('/');
}
