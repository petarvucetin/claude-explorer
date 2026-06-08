using System.Text.Json.Nodes;
using ClaudeExplorer.App.Compare;
using ClaudeExplorer.App.Environments;
using ClaudeExplorer.App.Tests.Fakes;
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.App.Tests.Compare;

public class CompareViewModelTests
{
    private static EnvironmentSnapshot Snap(string model) => new(
        new[] { new EffectiveSetting("model", MergeStrategy.ScalarLastWins, JsonValue.Create(model), null, Array.Empty<SettingContribution>(), false) },
        new ArtifactCatalog(Array.Empty<ResolvedArtifact>()),
        Array.Empty<McpServer>(), Array.Empty<string>(), new DependencyReport(Array.Empty<DependencyResult>()));

    private static EnvironmentService TwoEnvService(InMemoryFileSystem fs)
    {
        var svc = new EnvironmentService(new EnvironmentDiscovery(fs, new FakeWslLocator(), "C:/Users/p"),
                                         new EnvironmentStore(fs, fs, "/s.json"));
        svc.Load();
        svc.AddCustom("D:/wsl", "WSL · Ubuntu");
        return svc;
    }

    [Fact]
    public void Load_compares_the_two_selected_environments()
    {
        var fs = new InMemoryFileSystem();
        var svc = TwoEnvService(fs);
        var win = svc.Environments[0];
        var other = svc.Environments.Last();
        var source = new FakeEnvironmentCompareDataSource()
            .Add(win.Id, Snap("opus"))
            .Add(other.Id, Snap("sonnet"));
        var vm = new CompareViewModel(svc, source);

        vm.Load();

        Assert.False(vm.IsLoading);
        Assert.NotNull(vm.Comparison);
        var settings = vm.Comparison!.Find("Settings")!;
        Assert.Equal(DiffStatus.Differs, settings.Rows.Single(r => r.Key == "model").Status);
        Assert.Equal("Settings", vm.SelectedCategory!.Name); // defaults to first category
    }

    [Fact]
    public void SelectCategory_changes_the_visible_category()
    {
        var fs = new InMemoryFileSystem();
        var svc = TwoEnvService(fs);
        var source = new FakeEnvironmentCompareDataSource()
            .Add(svc.Environments[0].Id, Snap("opus"))
            .Add(svc.Environments.Last().Id, Snap("opus"));
        var vm = new CompareViewModel(svc, source);
        vm.Load();

        vm.SelectCategory("MCP");

        Assert.Equal("MCP", vm.SelectedCategory!.Name);
    }
}
