using ClaudeExplorer.App.Dashboard;
using ClaudeExplorer.App.Services;
using ClaudeExplorer.App.Tests.Fakes;
using ClaudeExplorer.App.ViewModels;
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;
using ClaudeExplorer.Core.Plugins;

namespace ClaudeExplorer.App.Tests.ViewModels;

public class ShellViewModelTests
{
    private static ShellViewModel BuildVm(DashboardInputs inputs)
        => new(new FakeDashboardDataSource(inputs),
               new PluginInventoryReader(new InMemoryFileSystem()),
               new WorkspaceContext("/home/u", "/work/my-project"));

    private static ResolvedArtifact Art(ArtifactKind kind, string name, ArtifactSourceKind src, string? plugin = null)
    {
        var winner = new DiscoveredArtifact(kind, name, null, new ArtifactSource(src, plugin), $"/{name}");
        return new ResolvedArtifact(winner, Array.Empty<DiscoveredArtifact>());
    }

    private static DependencyResult Dep(string name, DependencyStatusKind kind, params string[] referencedBy) =>
        new(new DependencyRef(name, name, referencedBy), new DependencyStatus(kind));

    /// <summary>
    /// Inputs with:
    ///   - 2 commands (user), 1 skill (user)  → CommandsAndSkills = 3
    ///   - 1 MCP server with a missing dep     → HasMcpProblem = true
    ///   - 1 missing dep                       → HasDependencyProblem = true
    /// </summary>
    private static DashboardInputs BuildInputs() => new(
        new EffectiveConfig(Array.Empty<EffectiveSetting>()),
        new ArtifactCatalog(new[]
        {
            Art(ArtifactKind.Command, "cmd-a", ArtifactSourceKind.User),
            Art(ArtifactKind.Command, "cmd-b", ArtifactSourceKind.User),
            Art(ArtifactKind.Skill, "skill-a", ArtifactSourceKind.User),
        }),
        new DependencyReport(new[]
        {
            Dep("uvx", DependencyStatusKind.Missing, "mcp:context7"),
        }),
        new[]
        {
            new McpServer("context7", "uvx", new[] { "context7" }, ScopeKind.Project),
        },
        Array.Empty<ChangeLogEntry>(),
        "my-project");

    [Fact]
    public void Load_sets_per_type_counts()
    {
        var vm = BuildVm(BuildInputs());

        vm.Load();

        Assert.Equal(2, vm.Commands);
        Assert.Equal(1, vm.Skills);
        Assert.Equal(0, vm.Subagents);
        Assert.Equal(1, vm.Mcp);
        Assert.Equal(0, vm.Plugins);   // empty plugin fs
        Assert.Equal(0, vm.Hooks);     // no hooks in the empty effective config
        Assert.False(vm.HasHookProblem);
    }

    [Fact]
    public void Load_sets_HasDependencyProblem_true_when_dep_missing()
    {
        var vm = BuildVm(BuildInputs());

        vm.Load();

        Assert.True(vm.HasDependencyProblem);
    }

    [Fact]
    public void Load_sets_HasMcpProblem_true_when_mcp_server_down()
    {
        var vm = BuildVm(BuildInputs());

        vm.Load();

        Assert.True(vm.HasMcpProblem);
    }

    [Fact]
    public void ProjectLabel_flows_from_workspace()
    {
        var vm = BuildVm(BuildInputs());

        Assert.Equal("my-project", vm.ProjectLabel);
    }

    [Fact]
    public void Load_does_not_throw_when_stats_list_is_unexpectedly_empty()
    {
        // Verify that FirstOrDefault-based lookups are crash-safe even with an empty stat list.
        // We use a data source that returns minimal inputs (all zeros → all stats present but zeroed).
        var minimalInputs = new DashboardInputs(
            new EffectiveConfig(Array.Empty<EffectiveSetting>()),
            new ArtifactCatalog(Array.Empty<ResolvedArtifact>()),
            new DependencyReport(Array.Empty<DependencyResult>()),
            Array.Empty<McpServer>(),
            Array.Empty<ChangeLogEntry>(),
            "empty");
        var vm = new ShellViewModel(new FakeDashboardDataSource(minimalInputs),
            new PluginInventoryReader(new InMemoryFileSystem()),
            new WorkspaceContext("/home/u", "/work/empty"));

        var ex = Record.Exception(() => vm.Load());

        Assert.Null(ex);
        Assert.Equal(0, vm.Commands);
        Assert.Equal(0, vm.Skills);
        Assert.False(vm.HasDependencyProblem);
        Assert.False(vm.HasMcpProblem);
    }
}
