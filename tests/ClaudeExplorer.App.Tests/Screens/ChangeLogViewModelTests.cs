using ClaudeExplorer.App.Screens.ChangeLog;
using ClaudeExplorer.App.Tests.Fakes;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.App.Tests.Screens;

public class ChangeLogViewModelTests
{
    private const string ProjectDir = "/myproject";
    private const string SettingsPath = "/myproject/.claude/settings.json";
    private const string BackupRoot = "/backups";

    private static SafeMutationService BuildService(InMemoryFileSystem? fs = null)
    {
        fs ??= new InMemoryFileSystem();
        var backupFs = new InMemoryFileSystem();
        var backupStore = new FileBackupStore(backupFs, backupFs, BackupRoot);
        var runner = new FakeProcessRunner();
        return new SafeMutationService(fs, fs, backupStore, runner);
    }

    private static ChangeLogEntry ApplyEdit(SafeMutationService svc, InMemoryFileSystem fs, string content)
    {
        fs.AddFile(SettingsPath, """{"model":"sonnet"}""");
        var winner = new SettingOrigin(ScopeKind.Project, SettingsPath, "model");
        var preview = svc.PreviewSettingsEdit(EditMode.EditWinner, ProjectDir, winner, content);
        return svc.ApplyEdit(preview, "2026-06-07T00:00:00Z", "Test edit");
    }

    [Fact]
    public void Load_groups_entries_by_scope()
    {
        var fs = new InMemoryFileSystem();
        var svc = BuildService(fs);
        var vm = new ChangeLogViewModel(svc);

        // Apply a project-scope edit
        ApplyEdit(svc, fs, """{"model":"opus"}""");

        vm.Load();

        Assert.Single(vm.Groups);
        var group = vm.Groups[0];
        Assert.Equal(ScopeKind.Project, group.Key);
        Assert.Single(group);
    }

    [Fact]
    public void Undo_marks_entry_as_undone()
    {
        var fs = new InMemoryFileSystem();
        var svc = BuildService(fs);
        var vm = new ChangeLogViewModel(svc);

        var entry = ApplyEdit(svc, fs, """{"model":"opus"}""");
        vm.Load();

        // Verify the entry is visible
        Assert.Single(vm.Groups[0]);

        vm.Undo(entry);

        // After undo the log still has the entry but marked undone
        vm.Load();
        Assert.True(vm.Groups[0].First().IsUndone);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public void Undo_twice_sets_error_message()
    {
        var fs = new InMemoryFileSystem();
        var svc = BuildService(fs);
        var vm = new ChangeLogViewModel(svc);

        var entry = ApplyEdit(svc, fs, """{"model":"opus"}""");
        vm.Undo(entry);

        // Second undo should fail — entry already undone
        vm.Undo(entry);

        Assert.NotNull(vm.ErrorMessage);
    }

    [Fact]
    public void Empty_log_returns_empty_groups()
    {
        var svc = BuildService();
        var vm = new ChangeLogViewModel(svc);

        vm.Load();

        Assert.Empty(vm.Groups);
    }
}
