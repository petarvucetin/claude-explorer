using ClaudeExplorer.App.Screens.Hooks;
using ClaudeExplorer.App.Tests.Fakes;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.App.Tests.Screens;

public class HookEditViewModelTests
{
    private const string File = "/home/.claude/settings.json";
    private const string Source = """
        {
          "hooks": {
            "PostToolUse": [
              { "matcher": "Bash", "hooks": [ { "type": "command", "command": "old.js" } ] }
            ]
          }
        }
        """;

    private static (SafeMutationService svc, InMemoryFileSystem fs) Build()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile(File, Source);
        var backup = new InMemoryFileSystem();
        var svc = new SafeMutationService(fs, fs, new FileBackupStore(backup, backup, "/backups"), new FakeProcessRunner());
        return (svc, fs);
    }

    private static HookRow Row(ScopeKind scope = ScopeKind.User) =>
        new("PostToolUse", "Bash", "old.js", "command", scope, File, "node", HookHealth.Ok, SourceGroupIndex: 0);

    private static HookEditViewModel Vm(SafeMutationService svc, InMemoryFileSystem fs, HookRow row) =>
        new(svc, fs, row, () => "2026-06-08T00:00:00Z", "/repo");

    [Fact]
    public void Load_extracts_the_block_text()
    {
        var (svc, fs) = Build();
        var vm = Vm(svc, fs, Row());
        Assert.Contains("old.js", vm.BlockText);
        Assert.Contains("\"matcher\": \"Bash\"", vm.BlockText);
    }

    [Fact]
    public void Save_writes_spliced_file_and_records_change_log()
    {
        var (svc, fs) = Build();
        var vm = Vm(svc, fs, Row());
        vm.BlockText = """{ "matcher": "Bash", "hooks": [ { "type": "command", "command": "new.js" } ] }""";

        vm.DoPreview();
        vm.Save();

        Assert.NotNull(vm.Applied);
        Assert.Null(vm.Error);
        Assert.Contains("new.js", fs.ReadAllText(File));
        Assert.Single(svc.ChangeLog.Entries);
    }

    [Fact]
    public void Undo_reverts_to_original()
    {
        var (svc, fs) = Build();
        var vm = Vm(svc, fs, Row());
        vm.BlockText = """{ "matcher": "Bash", "hooks": [ { "type": "command", "command": "new.js" } ] }""";
        vm.DoPreview();
        vm.Save();

        vm.Undo();

        Assert.True(vm.Applied!.IsUndone);
        Assert.Contains("old.js", fs.ReadAllText(File));
    }

    [Fact]
    public void Read_only_row_refuses_to_save()
    {
        var (svc, fs) = Build();
        var vm = Vm(svc, fs, Row(ScopeKind.Plugin));
        Assert.False(vm.IsEditable);

        vm.BlockText = """{ "matcher": "Bash", "hooks": [] }""";
        vm.Save();

        Assert.Null(vm.Applied);
        Assert.NotNull(vm.Error);
        Assert.Contains("old.js", fs.ReadAllText(File)); // unchanged
    }

    [Fact]
    public void Invalid_json_surfaces_error_on_preview()
    {
        var (svc, fs) = Build();
        var vm = Vm(svc, fs, Row());
        vm.BlockText = "{ not json";

        vm.DoPreview();

        Assert.NotNull(vm.Error);
        Assert.Null(vm.Preview);
    }

    [Theory]
    [InlineData(ScopeKind.User, true)]
    [InlineData(ScopeKind.Project, false)]
    public void IsGlobalEdit_true_for_non_project_scopes(ScopeKind scope, bool expected)
    {
        var (svc, fs) = Build();
        Assert.Equal(expected, Vm(svc, fs, Row(scope)).IsGlobalEdit);
    }
}
