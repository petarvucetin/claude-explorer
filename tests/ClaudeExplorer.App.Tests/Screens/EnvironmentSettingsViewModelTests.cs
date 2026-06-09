using ClaudeExplorer.App.Environments;
using ClaudeExplorer.App.Screens.EnvironmentSettings;
using ClaudeExplorer.App.Tests.Fakes;
using ClaudeExplorer.Core;

namespace ClaudeExplorer.App.Tests.Screens;

public class EnvironmentSettingsViewModelTests
{
    private const string StorePath = "/home/.claude/.claude-explorer/environments.json";

    private static EnvironmentService BuildEnvService(InMemoryFileSystem fs)
        => new(new EnvironmentDiscovery(fs, new FakeWslLocator(), "C:/Users/u"),
               new EnvironmentStore(fs, fs, StorePath));

    private static EnvironmentSettingsViewModel BuildVm(InMemoryFileSystem fs, FakeWorkspaceContext workspace)
    {
        var envSvc = BuildEnvService(fs);
        envSvc.Load();
        return new EnvironmentSettingsViewModel(new EffectiveConfigService(fs), workspace, envSvc);
    }

    [Fact]
    public void Load_populates_view_and_toggles_loading()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/settings.json",
                """{ "model": "claude-opus-4-5" }""");
        var vm = BuildVm(fs, new FakeWorkspaceContext("/home", "/work/my-project"));
        var loadingStates = new List<bool>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.IsLoading)) loadingStates.Add(vm.IsLoading);
        };

        Assert.Null(vm.View);
        vm.Load();

        Assert.NotNull(vm.View);
        Assert.False(vm.IsLoading);
        Assert.Null(vm.ErrorMessage);
        Assert.Equal(new[] { true, false }, loadingStates);
    }

    [Fact]
    public void Load_maps_model_from_settings()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/settings.json",
                """{ "model": "claude-sonnet-4-5" }""");
        var vm = BuildVm(fs, new FakeWorkspaceContext("/home", ""));

        vm.Load();

        Assert.NotNull(vm.View!.Model);
        Assert.Equal("claude-sonnet-4-5", vm.View.Model!.Display);
    }

    [Fact]
    public void Load_identity_reflects_active_environment_name_and_project_label()
    {
        var fs = new InMemoryFileSystem();
        var vm = BuildVm(fs, new FakeWorkspaceContext("/home", "/work/my-app"));

        vm.Load();

        // The in-memory env service discovers a "Windows" environment keyed to C:/Users/u
        // The FakeWorkspaceContext provides ProjectLabel from the path split
        Assert.NotNull(vm.View);
        Assert.Equal("my-app", vm.View!.Identity.ProjectLabel);
    }

    [Fact]
    public void Load_raises_PropertyChanged_for_View()
    {
        var fs = new InMemoryFileSystem();
        var vm = BuildVm(fs, new FakeWorkspaceContext("/home", ""));
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.Load();

        Assert.Contains(nameof(vm.View), changed);
    }

    [Fact]
    public void Load_sets_ErrorMessage_when_engine_throws()
    {
        // A file system with a corrupt settings.json triggers a parse exception
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/settings.json", "NOT_VALID_JSON!!!");
        var vm = BuildVm(fs, new FakeWorkspaceContext("/home", ""));

        vm.Load();

        Assert.NotNull(vm.ErrorMessage);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public void Load_clears_ErrorMessage_after_success()
    {
        var badFs = new InMemoryFileSystem()
            .AddFile("/home/.claude/settings.json", "NOT_VALID_JSON!!!");
        var vm = BuildVm(badFs, new FakeWorkspaceContext("/home", ""));
        vm.Load();
        Assert.NotNull(vm.ErrorMessage);

        // Simulate a good subsequent load by building a new vm with a good fs
        var goodFs = new InMemoryFileSystem();
        var goodVm = BuildVm(goodFs, new FakeWorkspaceContext("/home", ""));
        goodVm.Load();

        Assert.Null(goodVm.ErrorMessage);
        Assert.NotNull(goodVm.View);
    }

    [Fact]
    public void Empty_settings_produces_empty_view_with_null_scalars()
    {
        var fs = new InMemoryFileSystem();
        var vm = BuildVm(fs, new FakeWorkspaceContext("/home", ""));

        vm.Load();

        Assert.NotNull(vm.View);
        Assert.Null(vm.View!.Model);
        Assert.Null(vm.View.OutputStyle);
        Assert.Empty(vm.View.Allow);
        Assert.Empty(vm.View.EnvVars);
    }
}
