namespace ClaudeExplorer.App.Screens.Artifacts;

/// <summary>Pure helpers for the bespoke artifact detail panes.</summary>
public static class ArtifactDetail
{
    private static readonly char[] ToolSeparators = { ',', ' ', '\t', '\n' };

    /// <summary>Split a subagent's <c>tools</c> frontmatter into chip labels. <c>"*"</c> (or blank →
    /// inherit) is normalized to a single "all tools" chip.</summary>
    public static IReadOnlyList<string> ToolChips(string? toolsField)
    {
        if (string.IsNullOrWhiteSpace(toolsField)) return Array.Empty<string>();
        if (toolsField.Trim() == "*") return new[] { "all tools" };
        return toolsField
            .Split(ToolSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>A command's slash invocation, e.g. <c>review</c> → <c>/review</c>.</summary>
    public static string Invocation(string name) => name.StartsWith('/') ? name : $"/{name}";
}
