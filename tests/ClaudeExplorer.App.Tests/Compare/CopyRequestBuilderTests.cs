using ClaudeExplorer.App.Compare;

namespace ClaudeExplorer.App.Tests.Compare;

public class CopyRequestBuilderTests
{
    private static CompareEndpoint Base => CompareEndpoint.Base("win", "Base · Windows", "C:/Users/me");
    private static CompareEndpoint Proj => CompareEndpoint.Project("p1", "Project A", "D:/work/a");

    [Fact]
    public void Settings_targets_shared_settings_json_by_default()
    {
        var row = new CompareRow("model", DiffStatus.Differs, "\"opus\"", "\"sonnet\"");
        var req = CopyRequestBuilder.Build("Settings", row, src: Base, tgt: Proj, local: false);

        Assert.Equal("Settings", req.Category);
        Assert.Equal("C:/Users/me/.claude/settings.json", req.SourceSettingsPath);
        Assert.Equal("D:/work/a/.claude/settings.json", req.TargetSettingsPath);
    }

    [Fact]
    public void Settings_targets_local_settings_when_local_is_true()
    {
        var row = new CompareRow("model", DiffStatus.Differs, "\"opus\"", "\"sonnet\"");
        var req = CopyRequestBuilder.Build("Settings", row, src: Base, tgt: Proj, local: true);
        Assert.Equal("D:/work/a/.claude/settings.local.json", req.TargetSettingsPath);
    }

    [Fact]
    public void Memory_base_resolves_under_dot_claude_project_resolves_at_root()
    {
        var row = new CompareRow("CLAUDE.md", DiffStatus.Differs, "a", "b");
        var req = CopyRequestBuilder.Build("Memory", row, src: Base, tgt: Proj, local: false);
        Assert.Equal("C:/Users/me/.claude/CLAUDE.md", req.SourceFilePath);
        Assert.Equal("D:/work/a/CLAUDE.md", req.TargetFilePath);
    }

    [Fact]
    public void Mcp_base_uses_dot_claude_json_project_uses_dot_mcp_json()
    {
        var row = new CompareRow("ctx7", DiffStatus.Differs, "uvx ctx7", "npx ctx7");
        var req = CopyRequestBuilder.Build("MCP", row, src: Base, tgt: Proj, local: false);
        Assert.Equal("C:/Users/me/.claude.json", req.SourceMcpPath);
        Assert.Equal("D:/work/a/.mcp.json", req.TargetMcpPath);
    }

    [Fact]
    public void Commands_uses_the_rows_resolved_source_path_and_rebases_to_the_target_commands_dir()
    {
        var row = new CompareRow("deploy", DiffStatus.OnlyA, "v1", null,
            PathA: "C:/Users/me/.claude/commands/deploy.md");
        var req = CopyRequestBuilder.Build("Commands", row, src: Base, tgt: Proj, local: false);

        Assert.Equal("C:/Users/me/.claude/commands/deploy.md", req.SourceFilePath);
        Assert.Equal("D:/work/a/.claude/commands/deploy.md", req.TargetFilePath);
    }

    [Fact]
    public void Subagents_rebases_into_the_target_agents_dir()
    {
        var row = new CompareRow("review", DiffStatus.OnlyA, "x", null,
            PathA: "C:/Users/me/.claude/agents/review.md");
        var req = CopyRequestBuilder.Build("Subagents", row, src: Base, tgt: Proj, local: false);

        Assert.Equal("Subagents", req.Category);
        Assert.Equal("C:/Users/me/.claude/agents/review.md", req.SourceFilePath);
        Assert.Equal("D:/work/a/.claude/agents/review.md", req.TargetFilePath);
    }

    [Fact]
    public void Skills_carries_the_source_skill_md_and_target_skill_md()
    {
        var row = new CompareRow("lint", DiffStatus.OnlyA, "x", null,
            PathA: "C:/Users/me/.claude/skills/lint/SKILL.md");
        var req = CopyRequestBuilder.Build("Skills", row, src: Base, tgt: Proj, local: false);

        Assert.Equal("Skills", req.Category);
        Assert.Equal("C:/Users/me/.claude/skills/lint/SKILL.md", req.SourceFilePath);
        Assert.Equal("D:/work/a/.claude/skills/lint/SKILL.md", req.TargetFilePath);
    }

    [Fact]
    public void Hooks_uses_settings_json_paths_respects_local_flag()
    {
        var row = new CompareRow("PreToolUse#0", DiffStatus.OnlyA, "{}", null);
        var req = CopyRequestBuilder.Build("Hooks", row, src: Base, tgt: Proj, local: true);

        Assert.Equal("Hooks", req.Category);
        Assert.Equal("C:/Users/me/.claude/settings.json", req.SourceSettingsPath);
        Assert.Equal("D:/work/a/.claude/settings.local.json", req.TargetSettingsPath);
    }
}
