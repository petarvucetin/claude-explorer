using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Hooks;

/// <summary>
/// Best-effort resolver for the script file a hook command executes. Strips a known runtime prefix
/// (node/python/bash/…), picks the first script-looking argument, and resolves it against the source
/// file's directory, the project dir, then the user dir. Returns null for inline commands, bare PATH
/// binaries, and unresolved templated paths (e.g. <c>${CLAUDE_PLUGIN_ROOT}</c>).
/// </summary>
public static class HookScriptResolver
{
    private static readonly HashSet<string> Runtimes = new(StringComparer.OrdinalIgnoreCase)
    {
        "node", "deno", "bun", "python", "python3", "py", "uv", "uvx",
        "sh", "bash", "zsh", "pwsh", "powershell", "ruby", "perl", "php",
    };

    private static readonly Dictionary<string, string> LangByExt = new(StringComparer.OrdinalIgnoreCase)
    {
        [".js"] = "javascript", [".mjs"] = "javascript", [".cjs"] = "javascript",
        [".ts"] = "typescript", [".py"] = "python",
        [".sh"] = "bash", [".bash"] = "bash", [".zsh"] = "bash",
        [".ps1"] = "powershell", [".rb"] = "ruby", [".pl"] = "perl", [".php"] = "php",
        [".json"] = "json", [".yml"] = "yaml", [".yaml"] = "yaml",
        [".cmd"] = "dos", [".bat"] = "dos",
    };

    public static ScriptRef? Resolve(IFileSystem fs, string command, string sourceFileDir, string projectDir, string userDir)
    {
        var tokens = Tokenize(command);
        if (tokens.Count == 0) return null;

        var start = Runtimes.Contains(tokens[0]) ? 1 : 0;
        string? candidate = null;
        for (var i = start; i < tokens.Count; i++)
        {
            if (tokens[i].StartsWith('-')) continue;
            candidate = tokens[i];
            break;
        }
        if (candidate is null) return null;
        if (candidate.Contains("${") || candidate.Contains('%')) return null;

        var ext = ExtensionOf(candidate);
        var known = LangByExt.TryGetValue(ext, out var lang);

        foreach (var cand in Candidates(candidate, sourceFileDir, projectDir, userDir))
        {
            if (fs.FileExists(cand))
                return new ScriptRef(Norm(cand), known ? lang! : "plaintext", true);
        }

        if (!known) return null; // inline command / bare binary, not a script file
        return new ScriptRef(Norm(Candidates(candidate, sourceFileDir, projectDir, userDir).First()), lang!, false);
    }

    private static List<string> Tokenize(string command) =>
        command.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
               .Select(t => t.Trim('"', '\'')).ToList();

    private static string ExtensionOf(string path)
    {
        var slash = path.LastIndexOfAny(new[] { '/', '\\' });
        var name = slash >= 0 ? path[(slash + 1)..] : path;
        var dot = name.LastIndexOf('.');
        return dot >= 0 ? name[dot..] : "";
    }

    private static IEnumerable<string> Candidates(string token, string srcDir, string projDir, string userDir)
    {
        var p = token.Replace('\\', '/');
        if (p.StartsWith('/') || (p.Length > 1 && p[1] == ':')) { yield return p; yield break; }
        if (!string.IsNullOrEmpty(srcDir)) yield return Combine(srcDir, p);
        if (!string.IsNullOrEmpty(projDir)) yield return Combine(projDir, p);
        if (!string.IsNullOrEmpty(userDir)) yield return Combine(userDir, p);
    }

    private static string Combine(string a, string b) => $"{a.Replace('\\', '/').TrimEnd('/')}/{b}";
    private static string Norm(string p) => p.Replace('\\', '/');
}
