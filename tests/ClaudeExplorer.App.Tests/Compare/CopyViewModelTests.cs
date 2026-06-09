using ClaudeExplorer.App.Compare;
using ClaudeExplorer.App.Tests.Fakes;
using ClaudeExplorer.Core.Mutation;
using ClaudeExplorer.Core.Sync;

namespace ClaudeExplorer.App.Tests.Compare;

public class CopyViewModelTests
{
    private const string Ts = "2026-06-08T00:00:00Z";

    private static (SafeMutationService svc, InMemoryFileSystem fs, CopyViewModel vm) Build()
    {
        var fs = new InMemoryFileSystem();
        var backupFs = new InMemoryFileSystem();
        var svc = new SafeMutationService(fs, fs, new FileBackupStore(backupFs, backupFs, "/bk"), new FakeProcessRunner());
        var vm = new CopyViewModel(svc, new ConfigCopyService(fs), () => Ts);
        return (svc, fs, vm);
    }

    // ── Copy ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Copy_settings_key_writes_target_and_logs()
    {
        var (svc, fs, vm) = Build();
        fs.AddFile("/base/.claude/settings.json", """{ "model": "opus" }""");

        vm.Copy(new CopyRequest("Settings", "model",
            "/base/.claude/settings.json", "/proj/.claude/settings.json"));

        Assert.Null(vm.Error);
        Assert.NotNull(vm.Applied);
        Assert.Contains("opus", fs.ReadAllText("/proj/.claude/settings.json"));
        Assert.Single(svc.ChangeLog.Entries);
    }

    [Fact]
    public void Copy_settings_key_description_is_logged()
    {
        var (svc, fs, vm) = Build();
        fs.AddFile("/base/.claude/settings.json", """{ "model": "sonnet" }""");

        vm.Copy(new CopyRequest("Settings", "model",
            "/base/.claude/settings.json", "/proj/.claude/settings.json"));

        Assert.Null(vm.Error);
        var entry = svc.ChangeLog.Entries.Single();
        Assert.Contains("Settings", entry.Description);
        Assert.Contains("model", entry.Description);
        Assert.Equal(Ts, entry.Timestamp);
    }

    [Fact]
    public void Copy_undo_reverts_target_write()
    {
        var (svc, fs, vm) = Build();
        fs.AddFile("/base/.claude/settings.json", """{ "model": "opus" }""");

        vm.Copy(new CopyRequest("Settings", "model",
            "/base/.claude/settings.json", "/proj/.claude/settings.json"));

        Assert.NotNull(vm.Applied);
        var appliedEntry = vm.Applied!;

        vm.Undo();

        Assert.Null(vm.Error);
        // The target should no longer exist (was created fresh, undo deletes it).
        var targetExists = fs.FileExists("/proj/.claude/settings.json");
        var undoneEntry = svc.ChangeLog.Entries.First(e => e.Id == appliedEntry.Id);
        Assert.True(undoneEntry.IsUndone);
        // Either the file is gone or it no longer contains "opus".
        if (targetExists)
            Assert.DoesNotContain("opus", fs.ReadAllText("/proj/.claude/settings.json"));
    }

    // ── Move ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Move_settings_key_writes_target_and_removes_source_key()
    {
        var (svc, fs, vm) = Build();
        fs.AddFile("/base/.claude/settings.json", """{ "model": "opus", "outputStyle": "auto" }""");
        fs.AddFile("/proj/.claude/settings.json", """{}""");

        vm.Move(new CopyRequest("Settings", "model",
            "/base/.claude/settings.json", "/proj/.claude/settings.json"));

        Assert.Null(vm.Error);
        // Target receives the value.
        Assert.Contains("opus", fs.ReadAllText("/proj/.claude/settings.json"));
        // Source no longer has the key.
        Assert.DoesNotContain("opus", fs.ReadAllText("/base/.claude/settings.json"));
        // Two change-log entries: target write + source removal.
        Assert.Equal(2, svc.ChangeLog.Entries.Count);
    }

    [Fact]
    public void Move_settings_key_source_still_has_other_keys()
    {
        var (_, fs, vm) = Build();
        fs.AddFile("/base/.claude/settings.json", """{ "model": "opus", "outputStyle": "auto" }""");

        vm.Move(new CopyRequest("Settings", "model",
            "/base/.claude/settings.json", "/proj/.claude/settings.json"));

        Assert.Null(vm.Error);
        // "outputStyle" stays in the source.
        Assert.Contains("outputStyle", fs.ReadAllText("/base/.claude/settings.json"));
    }

    [Fact]
    public void Move_undo_restores_source_key_and_reverts_target()
    {
        var (_, fs, vm) = Build();
        fs.AddFile("/base/.claude/settings.json", """{ "model": "opus", "outputStyle": "auto" }""");
        fs.AddFile("/proj/.claude/settings.json", """{}""");

        vm.Move(new CopyRequest("Settings", "model",
            "/base/.claude/settings.json", "/proj/.claude/settings.json"));
        Assert.Null(vm.Error);
        Assert.DoesNotContain("opus", fs.ReadAllText("/base/.claude/settings.json"));

        vm.Undo();

        Assert.Null(vm.Error);
        // Both halves reverted: source key restored, target no longer has the moved key.
        Assert.Contains("opus", fs.ReadAllText("/base/.claude/settings.json"));
        Assert.DoesNotContain("opus", fs.ReadAllText("/proj/.claude/settings.json"));
    }

    // ── File move ────────────────────────────────────────────────────────────

    [Fact]
    public void Move_memory_file_copies_target_and_deletes_source()
    {
        var (svc, fs, vm) = Build();
        fs.AddFile("/base/.claude/CLAUDE.md", "# notes");

        vm.Move(new CopyRequest("Memory", "CLAUDE.md",
            SourceFilePath: "/base/.claude/CLAUDE.md",
            TargetFilePath: "/proj/.claude/CLAUDE.md"));

        Assert.Null(vm.Error);
        Assert.True(fs.FileExists("/proj/.claude/CLAUDE.md"));
        Assert.Equal("# notes", fs.ReadAllText("/proj/.claude/CLAUDE.md"));
        Assert.False(fs.FileExists("/base/.claude/CLAUDE.md")); // source deleted
        Assert.Equal(2, svc.ChangeLog.Entries.Count);            // write + delete
    }

    [Fact]
    public void Move_memory_file_undo_restores_source_and_reverts_target()
    {
        var (_, fs, vm) = Build();
        fs.AddFile("/base/.claude/CLAUDE.md", "# notes");

        vm.Move(new CopyRequest("Memory", "CLAUDE.md",
            SourceFilePath: "/base/.claude/CLAUDE.md",
            TargetFilePath: "/proj/.claude/CLAUDE.md"));
        Assert.False(fs.FileExists("/base/.claude/CLAUDE.md"));

        vm.Undo();

        Assert.Null(vm.Error);
        Assert.True(fs.FileExists("/base/.claude/CLAUDE.md"));   // delete undone
        Assert.Equal("# notes", fs.ReadAllText("/base/.claude/CLAUDE.md"));
        Assert.False(fs.FileExists("/proj/.claude/CLAUDE.md"));  // target write undone
    }

    [Fact]
    public void Copy_skill_directory_writes_every_file_and_logs_each_write()
    {
        var (svc, fs, vm) = Build();
        fs.AddFile("/base/.claude/skills/lint/SKILL.md", "# lint");
        fs.AddFile("/base/.claude/skills/lint/scripts/run.sh", "echo hi");

        vm.Copy(new CopyRequest("Skills", "lint",
            SourceFilePath: "/base/.claude/skills/lint/SKILL.md",
            TargetFilePath: "/proj/.claude/skills/lint/SKILL.md"));

        Assert.Null(vm.Error);
        Assert.Equal("# lint", fs.ReadAllText("/proj/.claude/skills/lint/SKILL.md"));
        Assert.Equal("echo hi", fs.ReadAllText("/proj/.claude/skills/lint/scripts/run.sh"));
        Assert.Equal(2, svc.ChangeLog.Entries.Count);
    }

    [Fact]
    public void Move_skill_directory_undo_restores_all_source_files_and_removes_target()
    {
        var (_, fs, vm) = Build();
        fs.AddFile("/base/.claude/skills/lint/SKILL.md", "# lint");
        fs.AddFile("/base/.claude/skills/lint/scripts/run.sh", "echo hi");

        vm.Move(new CopyRequest("Skills", "lint",
            SourceFilePath: "/base/.claude/skills/lint/SKILL.md",
            TargetFilePath: "/proj/.claude/skills/lint/SKILL.md"));
        Assert.False(fs.FileExists("/base/.claude/skills/lint/SKILL.md"));
        Assert.True(fs.FileExists("/proj/.claude/skills/lint/SKILL.md"));

        vm.Undo();

        Assert.Null(vm.Error);
        Assert.True(fs.FileExists("/base/.claude/skills/lint/SKILL.md"));
        Assert.True(fs.FileExists("/base/.claude/skills/lint/scripts/run.sh"));
        Assert.False(fs.FileExists("/proj/.claude/skills/lint/SKILL.md"));
        Assert.False(fs.FileExists("/proj/.claude/skills/lint/scripts/run.sh"));
    }

    // ── Error handling ────────────────────────────────────────────────────────

    [Fact]
    public void Copy_with_unknown_category_sets_error()
    {
        var (_, _, vm) = Build();

        vm.Copy(new CopyRequest("Unknown", "key", "/a.json", "/b.json"));

        Assert.NotNull(vm.Error);
        Assert.Null(vm.Applied);
    }
}
