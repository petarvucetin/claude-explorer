using ClaudeExplorer.Core.Plugins;

namespace ClaudeExplorer.App.Screens.Plugins;

/// <summary>Pure formatting helpers for the Plugins screen.</summary>
public static class PluginCardMapper
{
    /// <summary>"provides" chips, e.g. ["14 skills", "4 hooks"] — pluralized, zeros omitted.</summary>
    public static IReadOnlyList<string> ProvidesParts(ProvidesCounts p)
    {
        var parts = new List<string>();
        Add(parts, p.Commands, "command");
        Add(parts, p.Skills, "skill");
        Add(parts, p.Subagents, "subagent");
        Add(parts, p.Hooks, "hook");
        if (p.Mcp > 0) parts.Add($"{p.Mcp} mcp");
        return parts;
    }

    private static void Add(List<string> parts, int n, string singular)
    {
        if (n > 0) parts.Add($"{n} {singular}{(n == 1 ? "" : "s")}");
    }
}
