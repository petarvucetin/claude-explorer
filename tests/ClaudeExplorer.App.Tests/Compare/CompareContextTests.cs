using System.Text.Json.Nodes;
using ClaudeExplorer.App.Compare;
using ClaudeExplorer.App.Environments;
using ClaudeExplorer.App.Tests.Fakes;
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.App.Tests.Compare;

public class CompareContextTests
{
    private static EnvironmentSnapshot Snap(string model) => new(
        new[] { new EffectiveSetting("model", MergeStrategy.ScalarLastWins, JsonValue.Create(model), null, Array.Empty<SettingContribution>(), false) },
        new ArtifactCatalog(Array.Empty<ResolvedArtifact>()),
        Array.Empty<McpServer>(), Array.Empty<string>(), new DependencyReport(Array.Empty<DependencyResult>()),
        new Dictionary<string, string>());

    private static (CompareContext ctx, EnvironmentService env, ProjectRegistry reg, FakeEnvironmentCompareDataSource src) Build()
    {
        var fs = new InMemoryFileSystem();
        var env = new EnvironmentService(new EnvironmentDiscovery(fs, new FakeWslLocator(), "C:/Users/p"),
                                         new EnvironmentStore(fs, fs, "/s.json"));
        env.Load();
        env.AddCustom("D:/wsl", "WSL · Ubuntu");
        var reg = new ProjectRegistry(new EnvironmentStore(fs, fs, "/reg.json"));
        reg.Load();
        var src = new FakeEnvironmentCompareDataSource();
        var ctx = new CompareContext(env, reg, src);
        return (ctx, env, reg, src);
    }

    [Fact]
    public void Is_off_until_B_is_set()
    {
        var (ctx, _, _, _) = Build();
        Assert.False(ctx.IsComparing);
        Assert.Null(ctx.Comparison("Settings"));
    }

    [Fact]
    public void A_defaults_to_active_environment_base()
    {
        var (ctx, env, _, _) = Build();
        Assert.NotNull(ctx.EndpointA);
        Assert.Equal(EndpointKind.Base, ctx.EndpointA!.Kind);
        Assert.EndsWith(env.Active.Id, ctx.EndpointA.Id);
    }

    [Fact]
    public void SetB_enters_compare_mode_and_builds_a_category_comparison()
    {
        var (ctx, env, _, src) = Build();
        var a = ctx.Endpoints.First(e => e.Kind == EndpointKind.Base);
        var b = ctx.Endpoints.Last(e => e.Kind == EndpointKind.Base);
        src.Add(a.Id, Snap("opus")).Add(b.Id, Snap("sonnet"));

        ctx.SetA(a.Id);
        ctx.SetB(b.Id);

        Assert.True(ctx.IsComparing);
        var cat = ctx.Comparison("Settings")!;
        Assert.Equal(DiffStatus.Differs, cat.Rows.Single(r => r.Key == "model").Status);
    }

    [Fact]
    public void ClearB_exits_compare_mode()
    {
        var (ctx, _, _, src) = Build();
        var a = ctx.Endpoints.First(e => e.Kind == EndpointKind.Base);
        var b = ctx.Endpoints.Last(e => e.Kind == EndpointKind.Base);
        src.Add(a.Id, Snap("opus")).Add(b.Id, Snap("opus"));
        ctx.SetB(b.Id);
        Assert.True(ctx.IsComparing);

        ctx.ClearB();
        Assert.False(ctx.IsComparing);
    }

    [Fact]
    public void Selection_persists_across_calls_simulating_navigation()
    {
        var (ctx, _, _, src) = Build();
        var a = ctx.Endpoints.First(e => e.Kind == EndpointKind.Base);
        var b = ctx.Endpoints.Last(e => e.Kind == EndpointKind.Base);
        src.Add(a.Id, Snap("opus")).Add(b.Id, Snap("sonnet"));
        ctx.SetA(a.Id);
        ctx.SetB(b.Id);

        // A second screen reading the same singleton sees the same A/B and a fresh per-category result.
        Assert.Equal(a.Id, ctx.EndpointA!.Id);
        Assert.Equal(b.Id, ctx.EndpointB!.Id);
        Assert.NotNull(ctx.Comparison("MCP"));
    }

    [Fact]
    public void Changed_event_fires_when_B_is_set()
    {
        var (ctx, _, _, src) = Build();
        var b = ctx.Endpoints.Last(e => e.Kind == EndpointKind.Base);
        src.Add(ctx.EndpointA!.Id, Snap("opus")).Add(b.Id, Snap("sonnet"));
        var fired = 0;
        ctx.Changed += () => fired++;

        ctx.SetB(b.Id);
        Assert.True(fired > 0);
    }
}
