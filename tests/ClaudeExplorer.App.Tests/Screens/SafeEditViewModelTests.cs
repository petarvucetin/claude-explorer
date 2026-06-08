using ClaudeExplorer.App.Screens.EffectiveConfig;
using ClaudeExplorer.App.Tests.Fakes;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.App.Tests.Screens;

public class SafeEditViewModelTests
{
    private const string ProjectDir = "/myproject";
    private const string SettingsPath = "/myproject/.claude/settings.json";
    private const string BackupRoot = "/backups";

    private static (SafeMutationService svc, InMemoryFileSystem fs) BuildService(string? existingContent = null)
    {
        var fs = new InMemoryFileSystem();
        if (existingContent is not null)
            fs.AddFile(SettingsPath, existingContent);
        var backupFs = new InMemoryFileSystem();
        var backupStore = new FileBackupStore(backupFs, backupFs, BackupRoot);
        var runner = new FakeProcessRunner();
        var svc = new SafeMutationService(fs, fs, backupStore, runner);
        return (svc, fs);
    }

    private static SafeEditViewModel BuildVm(SafeMutationService svc, SettingOrigin? winner = null)
    {
        var ts = "2026-06-07T00:00:00Z";
        return new SafeEditViewModel(svc, winner, () => ts, ProjectDir);
    }

    [Theory]
    [InlineData(ScopeKind.User, true)]        // global scopes warn
    [InlineData(ScopeKind.Enterprise, true)]
    [InlineData(ScopeKind.Plugin, true)]      // plugin base layer is global too
    [InlineData(ScopeKind.Project, false)]    // project-specific scopes do not
    [InlineData(ScopeKind.Local, false)]
    public void IsGlobalEdit_warns_for_every_non_project_scope_when_editing_the_winner(ScopeKind scope, bool expected)
    {
        var (svc, _) = BuildService();
        var winner = new SettingOrigin(scope, $"/x/settings.json", "model");
        var vm = BuildVm(svc, winner);
        vm.Mode = EditMode.EditWinner;

        Assert.Equal(expected, vm.IsGlobalEdit);
    }

    [Fact]
    public void IsGlobalEdit_is_false_when_overriding_rather_than_editing_the_winner()
    {
        var (svc, _) = BuildService();
        var vm = BuildVm(svc, new SettingOrigin(ScopeKind.User, "/x/settings.json", "model"));
        vm.Mode = EditMode.OverrideAtProject;

        Assert.False(vm.IsGlobalEdit);
    }

    [Fact]
    public void Preview_with_valid_json_populates_diff_and_validates()
    {
        var original = """{"model":"sonnet"}""";
        var (svc, _) = BuildService(original);
        var winner = new SettingOrigin(ScopeKind.Project, SettingsPath, "model");
        var vm = BuildVm(svc, winner);
        vm.Mode = EditMode.EditWinner;
        vm.NewContent = """{"model":"opus"}""";

        vm.DoPreview();

        Assert.NotNull(vm.Preview);
        Assert.True(vm.Preview!.Validation.IsValid);
        Assert.True(vm.Preview.Diff.HasChanges);
        Assert.Null(vm.Error);
    }

    [Fact]
    public void Apply_writes_file_and_returns_change_log_entry()
    {
        var original = """{"model":"sonnet"}""";
        var (svc, fs) = BuildService(original);
        var winner = new SettingOrigin(ScopeKind.Project, SettingsPath, "model");
        var vm = BuildVm(svc, winner);
        vm.Mode = EditMode.EditWinner;
        vm.NewContent = """{"model":"opus"}""";

        vm.DoPreview();
        vm.Apply();

        Assert.NotNull(vm.Applied);
        Assert.Null(vm.Error);
        Assert.True(fs.FileExists(SettingsPath));
        Assert.Contains("opus", fs.ReadAllText(SettingsPath));
        Assert.Single(svc.ChangeLog.Entries);
    }

    [Fact]
    public void Undo_reverts_file_content()
    {
        var original = """{"model":"sonnet"}""";
        var (svc, fs) = BuildService(original);
        var winner = new SettingOrigin(ScopeKind.Project, SettingsPath, "model");
        var vm = BuildVm(svc, winner);
        vm.Mode = EditMode.EditWinner;
        vm.NewContent = """{"model":"opus"}""";

        vm.DoPreview();
        vm.Apply();
        Assert.NotNull(vm.Applied);

        vm.Undo();

        Assert.True(vm.Applied!.IsUndone);
        Assert.Contains("sonnet", fs.ReadAllText(SettingsPath));
        Assert.Null(vm.Error);
    }

    [Fact]
    public void Invalid_json_preview_sets_validation_invalid()
    {
        var (svc, _) = BuildService("""{"model":"sonnet"}""");
        var winner = new SettingOrigin(ScopeKind.Project, SettingsPath, "model");
        var vm = BuildVm(svc, winner);
        vm.Mode = EditMode.EditWinner;
        vm.NewContent = "{ not valid json";

        vm.DoPreview();

        Assert.NotNull(vm.Preview);
        Assert.False(vm.Preview!.Validation.IsValid);
        Assert.NotEmpty(vm.Preview.Validation.Errors);
    }

    [Fact]
    public void Apply_refused_when_validation_invalid()
    {
        var (svc, _) = BuildService("""{"model":"sonnet"}""");
        var winner = new SettingOrigin(ScopeKind.Project, SettingsPath, "model");
        var vm = BuildVm(svc, winner);
        vm.Mode = EditMode.EditWinner;
        vm.NewContent = "{ bad json";

        vm.DoPreview();

        // Even if we call Apply directly (bypassing the disabled button), it should set Error
        vm.Apply();

        Assert.Null(vm.Applied);
        Assert.NotNull(vm.Error);
    }

    [Fact]
    public void Override_at_project_targets_project_settings_json()
    {
        var (svc, _) = BuildService();
        var vm = BuildVm(svc, winner: null);
        vm.Mode = EditMode.OverrideAtProject;
        vm.NewContent = """{"model":"opus"}""";

        vm.DoPreview();

        Assert.NotNull(vm.Preview);
        Assert.Equal(SettingsPath, vm.Preview!.Target.FilePath);
    }
}
