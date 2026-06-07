namespace ClaudeExplorer.Core.Dependencies;

/// <summary>
/// The fixed set of runtimes we are willing to probe with a <c>--version</c> call, plus the probe
/// arguments. Membership is matched case-insensitively because executable names are
/// case-insensitive on the primary target OS (Windows); this is a deliberate exception to the
/// ordinal name matching used elsewhere for config keys and artifact names.
/// </summary>
public static class RuntimeAllowlist
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "node", "npm", "npx", "pnpm", "yarn", "bun", "deno",
        "uv", "uvx", "python", "python3",
        "docker", "podman", "git", "claude",
    };

    /// <summary>Arguments used for every probe — we only ever ask for a version.</summary>
    public static readonly IReadOnlyList<string> ProbeArguments = new[] { "--version" };

    public static bool IsAllowed(string executable) => Allowed.Contains(executable);

    /// <summary>A sorted snapshot of the allowlist, for display/tests.</summary>
    public static IReadOnlyList<string> Names => Allowed.OrderBy(x => x, StringComparer.Ordinal).ToList();
}
