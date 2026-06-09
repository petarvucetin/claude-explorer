namespace ClaudeExplorer.Core.Dependencies;

/// <summary>
/// Pulls the underlying executable out of a command line. The executable is the first token
/// (surrounding quotes honored), reduced to its file name without directory and without a trailing
/// Windows extension — so <c>/usr/bin/node</c> → <c>node</c> and <c>npx -y @scope/pkg</c> →
/// <c>npx</c>. Returns <c>null</c> for blank input.
/// </summary>
public static class ExecutableExtractor
{
    private static readonly string[] WindowsExtensions = { ".exe", ".cmd", ".bat", ".com" };

    public static string? Extract(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine)) return null;

        var first = FirstToken(commandLine);
        return first.Length == 0 ? null : BaseName(first);
    }

    /// <summary>The first whitespace-delimited token of a command line, honoring surrounding quotes
    /// and preserving the raw path (no base-name reduction). Empty for blank input.</summary>
    public static string FirstToken(string s)
    {
        int i = 0;
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;

        if (i < s.Length && (s[i] == '"' || s[i] == '\''))
        {
            char quote = s[i++];
            int start = i;
            while (i < s.Length && s[i] != quote) i++;
            return s.Substring(start, i - start);
        }

        int tokenStart = i;
        while (i < s.Length && !char.IsWhiteSpace(s[i])) i++;
        return s.Substring(tokenStart, i - tokenStart);
    }

    private static string BaseName(string token)
    {
        var trimmed = token.Replace('\\', '/').TrimEnd('/');
        var slash = trimmed.LastIndexOf('/');
        var name = slash >= 0 ? trimmed.Substring(slash + 1) : trimmed;

        foreach (var ext in WindowsExtensions)
            if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return name.Substring(0, name.Length - ext.Length);

        return name;
    }
}
