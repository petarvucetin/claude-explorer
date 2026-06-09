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
        Array.Empty<McpServer>(), Array.Empty<string>(), new DependencyReport(Array.Empty<DependencyResult>()),
        new Dictionary<string, string>());

    private static EnvironmentService TwoEnvService(InMemoryFileSystem fs)
    {
        var svc = new EnvironmentService(new EnvironmentDiscovery(fs, new FakeWslLocator(), "C:/Users/p"),
                                         new EnvironmentStore(fs, fs, "/s.json"));
        svc.Load();
        svc.AddCustom("D:/wsl", "WSL · Ubuntu");
        return svc;
    }

    /// <summary>Builds a VM with two base envs (win, wsl) and one registered project.</summary>
    private static CompareViewModel BuildVm()
    {
        var fs = new InMemoryFileSystem();
        var envSvc = TwoEnvService(fs);
        var store = new EnvironmentStore(fs, fs, "/reg.json");
        var registry = new ProjectRegistry(store);
        registry.Load();
        registry.Add("Project A", envSvc.Environments[0].Id, "D:/work/a");

        var source = new FakeEnvironmentCompareDataSource();
        // Fake returns EmptySnap for any endpoint not explicitly added — that's fine for these tests.
        return new CompareViewModel(envSvc, registry, source);
    }

    [Fact]
    public void Endpoints_list_includes_bases_and_registered_projects()
    {
        var vm = BuildVm(); // envs: win, wsl ; projects: Project A
        var ids = vm.Endpoints.Select(e => e.Id).ToList();
        Assert.Contains(ids, i => i.StartsWith("base:"));
        Assert.Contains(ids, i => i.StartsWith("proj:"));
    }

    [Fact]
    public void SetEndpoints_loads_a_comparison()
    {
        var vm = BuildVm();
        var a = vm.Endpoints.First(e => e.Kind == EndpointKind.Base);
        var b = vm.Endpoints.First(e => e.Kind == EndpointKind.Project);
        vm.SetEndpoints(a.Id, b.Id);
        Assert.NotNull(vm.Comparison);
        Assert.Equal(a.Id, vm.LeftEndpoint!.Id);
        Assert.Equal(b.Id, vm.RightEndpoint!.Id);
    }

    [Fact]
    public void Load_compares_the_two_selected_environments()
    {
        var fs = new InMemoryFileSystem();
        var envSvc = TwoEnvService(fs);
        var store = new EnvironmentStore(fs, fs, "/reg.json");
        var registry = new ProjectRegistry(store);
        registry.Load();

        var win = envSvc.Environments[0];
        var other = envSvc.Environments.Last();
        var winEp = CompareEndpoint.Base(win.Id, win.Name, win.UserDir);
        var otherEp = CompareEndpoint.Base(other.Id, other.Name, other.UserDir);

        var source = new FakeEnvironmentCompareDataSource()
            .Add(winEp.Id, Snap("opus"))
            .Add(otherEp.Id, Snap("sonnet"));
        var vm = new CompareViewModel(envSvc, registry, source);

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
        var envSvc = TwoEnvService(fs);
        var store = new EnvironmentStore(fs, fs, "/reg.json");
        var registry = new ProjectRegistry(store);
        registry.Load();

        var win = envSvc.Environments[0];
        var other = envSvc.Environments.Last();
        var winEp = CompareEndpoint.Base(win.Id, win.Name, win.UserDir);
        var otherEp = CompareEndpoint.Base(other.Id, other.Name, other.UserDir);

        var source = new FakeEnvironmentCompareDataSource()
            .Add(winEp.Id, Snap("opus"))
            .Add(otherEp.Id, Snap("opus"));
        var vm = new CompareViewModel(envSvc, registry, source);
        vm.Load();

        vm.SelectCategory("MCP");

        Assert.Equal("MCP", vm.SelectedCategory!.Name);
    }
}
