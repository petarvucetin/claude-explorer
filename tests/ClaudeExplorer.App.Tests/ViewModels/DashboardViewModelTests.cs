using System.Text.Json.Nodes;
using ClaudeExplorer.App.Dashboard;
using ClaudeExplorer.App.Tests.Fakes;
using ClaudeExplorer.App.ViewModels;
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.App.Tests.ViewModels;

public class DashboardViewModelTests
{
    private static DashboardInputs SampleInputs() => new(
        new EffectiveConfig(Array.Empty<EffectiveSetting>()),
        new ArtifactCatalog(new[]
        {
            new ResolvedArtifact(
                new DiscoveredArtifact(ArtifactKind.Command, "x", null,
                    new ArtifactSource(ArtifactSourceKind.User), "/x"),
                Array.Empty<DiscoveredArtifact>()),
        }),
        new DependencyReport(Array.Empty<DependencyResult>()),
        Array.Empty<McpServer>(),
        Array.Empty<ChangeLogEntry>(),
        "webapp");

    [Fact]
    public void Load_populates_data_and_toggles_loading()
    {
        var vm = new DashboardViewModel(new FakeDashboardDataSource(SampleInputs()));
        var loadingStates = new List<bool>();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.IsLoading)) loadingStates.Add(vm.IsLoading); };

        Assert.Null(vm.Data);
        vm.Load();

        Assert.NotNull(vm.Data);
        Assert.False(vm.IsLoading);
        Assert.Equal(1, vm.Data!.Stats.Single(s => s.Label == "Commands").Value);
        Assert.Equal(new[] { true, false }, loadingStates); // turned on then off
    }

    [Fact]
    public void Load_raises_PropertyChanged_for_Data()
    {
        var vm = new DashboardViewModel(new FakeDashboardDataSource(SampleInputs()));
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.Load();

        Assert.Contains(nameof(vm.Data), changed);
    }
}
