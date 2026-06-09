using ClaudeExplorer.Core.Sync;

namespace ClaudeExplorer.App.Compare;

/// <summary>Pure builder that turns a diff row + the source/target endpoints into a Core
/// <see cref="CopyRequest"/>. Encapsulates the per-category on-disk layout: a Base reads/writes
/// under its <c>~/.claude</c>; a Project reads/writes under its folder (<c>.claude/</c> for
/// settings/commands/skills/agents, <c>.mcp.json</c> for MCP, root for CLAUDE.md). For file-based
/// categories (Commands/Subagents/Skills) the SOURCE path comes from the row's resolved path; the
/// TARGET is the same file/skill name rebased into the target endpoint's matching dir.</summary>
public static class CopyRequestBuilder
{
    public static CopyRequest Build(string category, CompareRow row, CompareEndpoint src, CompareEndpoint tgt, bool local)
    {
        var key = row.Key;
        // The overlay orients (src, tgt) for the chosen direction and sets row.SourcePath to the
        // source side's resolved path; file categories read that, value categories ignore it.
        switch (category)
        {
            case "Settings":
            {
                var srcPath = $"{ClaudeRoot(src)}/settings.json";
                var tgtPath = local ? $"{ClaudeRoot(tgt)}/settings.local.json" : $"{ClaudeRoot(tgt)}/settings.json";
                return new CopyRequest("Settings", key, SourceSettingsPath: srcPath, TargetSettingsPath: tgtPath);
            }
            case "Memory":
            {
                var srcPath = MemoryPath(src, key);
                var tgtPath = MemoryPath(tgt, key);
                return new CopyRequest("Memory", key, SourceFilePath: srcPath, TargetFilePath: tgtPath);
            }
            case "MCP":
            {
                var srcPath = McpPath(src);
                var tgtPath = McpPath(tgt);
                return new CopyRequest("MCP", key, SourceMcpPath: srcPath, TargetMcpPath: tgtPath);
            }
            case "Hooks":
            {
                var srcPath = $"{ClaudeRoot(src)}/settings.json";
                var tgtPath = local ? $"{ClaudeRoot(tgt)}/settings.local.json" : $"{ClaudeRoot(tgt)}/settings.json";
                return new CopyRequest("Hooks", key, SourceSettingsPath: srcPath, TargetSettingsPath: tgtPath);
            }
            case "Commands":
            case "Subagents":
            {
                var sub = category == "Commands" ? "commands" : "agents";
                var srcPath = row.SourcePath ?? $"{ClaudeRoot(src)}/{sub}/{key}.md";
                var tgtPath = $"{ClaudeRoot(tgt)}/{sub}/{key}.md";
                return new CopyRequest(category, key, SourceFilePath: srcPath, TargetFilePath: tgtPath);
            }
            case "Skills":
            {
                var srcPath = row.SourcePath ?? $"{ClaudeRoot(src)}/skills/{key}/SKILL.md";
                var tgtPath = $"{ClaudeRoot(tgt)}/skills/{key}/SKILL.md";
                return new CopyRequest("Skills", key, SourceFilePath: srcPath, TargetFilePath: tgtPath);
            }
            default:
                throw new System.InvalidOperationException($"Copy is not supported for category '{category}'.");
        }
    }

    /// <summary>The <c>.claude</c> dir of an endpoint (a Base's <c>~/.claude</c> or a Project's
    /// <c>&lt;projectDir&gt;/.claude</c>).</summary>
    private static string ClaudeRoot(CompareEndpoint e) =>
        e.Kind == EndpointKind.Base ? $"{e.UserDir}/.claude" : $"{e.ProjectDir}/.claude";

    private static string MemoryPath(CompareEndpoint e, string fileName) =>
        e.Kind == EndpointKind.Base ? $"{e.UserDir}/.claude/{fileName}" : $"{e.ProjectDir}/{fileName}";

    private static string McpPath(CompareEndpoint e) =>
        e.Kind == EndpointKind.Base ? $"{e.UserDir}/.claude.json" : $"{e.ProjectDir}/.mcp.json";
}
