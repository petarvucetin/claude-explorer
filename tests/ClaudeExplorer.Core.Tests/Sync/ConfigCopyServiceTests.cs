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
}
