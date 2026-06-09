using ClaudeExplorer.Core.Sync;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Sync;

public class ConfigCopyServiceTests
{
    [Fact]
    public void Copy_settings_key_writes_value_into_target_settings()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/base/.claude/settings.json", """{ "model": "opus" }""");
        var svc = new ConfigCopyService(fs);

        var plan = svc.PlanCopy(new CopyRequest(
            Category: "Settings", Key: "model",
            SourceSettingsPath: "/base/.claude/settings.json",
            TargetSettingsPath: "/proj/.claude/settings.json"));

        Assert.Equal("/proj/.claude/settings.json", plan.TargetPath);
        Assert.Contains("\"model\": \"opus\"", plan.NewTargetContent);
    }

    [Fact]
    public void Move_settings_key_also_removes_from_source()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/base/.claude/settings.json", """{ "model": "opus", "env": {} }""");
        var svc = new ConfigCopyService(fs);

        var plan = svc.PlanMove(new CopyRequest("Settings", "model",
            "/base/.claude/settings.json", "/proj/.claude/settings.json"));

        Assert.NotNull(plan.SourceRemoval);
        Assert.DoesNotContain("model", plan.SourceRemoval!.NewContent);
        Assert.Contains("\"env\"", plan.SourceRemoval.NewContent);
    }

    [Fact]
    public void Copy_memory_file_reads_source_content()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/base/.claude/CLAUDE.md", "# rules");
        var svc = new ConfigCopyService(fs);

        var plan = svc.PlanCopy(new CopyRequest("Memory", "CLAUDE.md",
            SourceFilePath: "/base/.claude/CLAUDE.md", TargetFilePath: "/proj/CLAUDE.md"));

        Assert.Equal("/proj/CLAUDE.md", plan.TargetPath);
        Assert.Equal("# rules", plan.NewTargetContent);
    }

    [Fact]
    public void Copy_skill_directory_enumerates_every_file_under_the_skill_folder()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/base/.claude/skills/lint/SKILL.md", "# lint");
        fs.AddFile("/base/.claude/skills/lint/scripts/run.sh", "echo hi");
        fs.AddFile("/base/.claude/skills/lint/references/notes.md", "notes");
        var svc = new ConfigCopyService(fs);

        var plan = svc.PlanCopy(new CopyRequest("Skills", "lint",
            SourceFilePath: "/base/.claude/skills/lint/SKILL.md",
            TargetFilePath: "/proj/.claude/skills/lint/SKILL.md"));

        // Three writes, each rebased under the target skill dir; no removals on copy.
        Assert.Equal(3, plan.Writes.Count);
        Assert.Contains(plan.Writes, w => w.Path == "/proj/.claude/skills/lint/SKILL.md" && w.Content == "# lint");
        Assert.Contains(plan.Writes, w => w.Path == "/proj/.claude/skills/lint/scripts/run.sh" && w.Content == "echo hi");
        Assert.Contains(plan.Writes, w => w.Path == "/proj/.claude/skills/lint/references/notes.md" && w.Content == "notes");
        Assert.Empty(plan.Removals);
        Assert.False(plan.Writes[0].IsJson);
    }

    [Fact]
    public void Move_skill_directory_removes_every_source_file()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/base/.claude/skills/lint/SKILL.md", "# lint");
        fs.AddFile("/base/.claude/skills/lint/scripts/run.sh", "echo hi");
        var svc = new ConfigCopyService(fs);

        var plan = svc.PlanMove(new CopyRequest("Skills", "lint",
            SourceFilePath: "/base/.claude/skills/lint/SKILL.md",
            TargetFilePath: "/proj/.claude/skills/lint/SKILL.md"));

        Assert.Equal(2, plan.Writes.Count);
        Assert.Equal(2, plan.Removals.Count);
        Assert.Contains(plan.Removals, r => r.Path == "/base/.claude/skills/lint/SKILL.md" && r.IsDelete);
        Assert.Contains(plan.Removals, r => r.Path == "/base/.claude/skills/lint/scripts/run.sh" && r.IsDelete);
    }

    [Fact]
    public void Copy_command_file_exposes_a_single_write_and_keeps_single_file_fields()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/base/.claude/commands/deploy.md", "# deploy");
        var svc = new ConfigCopyService(fs);

        var plan = svc.PlanCopy(new CopyRequest("Commands", "deploy",
            SourceFilePath: "/base/.claude/commands/deploy.md",
            TargetFilePath: "/proj/.claude/commands/deploy.md"));

        Assert.Equal("/proj/.claude/commands/deploy.md", plan.TargetPath);
        Assert.Equal("# deploy", plan.NewTargetContent);
        Assert.Single(plan.Writes);
        Assert.Equal("/proj/.claude/commands/deploy.md", plan.Writes[0].Path);
    }

    [Fact]
    public void Move_command_file_removes_source_as_a_delete()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/base/.claude/commands/deploy.md", "# deploy");
        var svc = new ConfigCopyService(fs);

        var plan = svc.PlanMove(new CopyRequest("Commands", "deploy",
            SourceFilePath: "/base/.claude/commands/deploy.md",
            TargetFilePath: "/proj/.claude/commands/deploy.md"));

        var removal = Assert.Single(plan.Removals);
        Assert.Equal("/base/.claude/commands/deploy.md", removal.Path);
        Assert.True(removal.IsDelete);
    }
}
