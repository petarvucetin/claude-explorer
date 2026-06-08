# Phase 7 — Blueprint UI Shell + Dashboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Stand up the cross-platform desktop app shell (`src/ClaudeExplorer.App`, Photino.Blazor +
MudBlazor, MVVM) in the **Blueprint** aesthetic, with a reusable component library, app chrome
(top bar + left rail), and a fully data-wired **Dashboard** (health, counts, conflicts/warnings,
needs-attention, recent changes) computed from the Phase 1–6 Core engines.

**Architecture:**
- **App project** (`Microsoft.NET.Sdk.Razor`, `OutputType Exe`, net10.0): Photino.Blazor 4.0.13 +
  MudBlazor 9.5.0. Verified to build on net10.
- **MVVM:** `ObservableObject` base (INotifyPropertyChanged); observable ViewModels hold view state;
  Blazor components bind and stay logic-light. ViewModels depend on Core via DI.
- **Dashboard data flow:** a **pure** `DashboardComputer.Compute(DashboardInputs) → DashboardData`
  does all derivation over Core model records (no IO → trivially unit-tested). An
  `IDashboardDataSource` seam gathers the raw `DashboardInputs` for the current workspace;
  `EngineDashboardDataSource` is the real impl over the Core façades (not unit-tested, like the
  `Physical*` seams). `DashboardViewModel` orchestrates: load → compute → expose `DashboardData`.
- **Workspace:** `IWorkspaceContext` (UserDir + ProjectDir + ProjectLabel); at runtime built from
  the OS (user home `~/.claude`, project = current dir).
- **Theme:** Blueprint tokens/CSS ported from `ux-explorations/03-blueprint.html` into bundled CSS;
  Archivo + Spline Sans Mono **bundled** as local woff2 (already done — no runtime CDN).
- **Tests:** `tests/ClaudeExplorer.App.Tests` (xUnit, references the App). ViewModel + computer
  logic only — **no component rendering tests** (per spec); manual visual run via `/run`.

**Tech Stack:** .NET 10, C#, Blazor, Photino.Blazor 4.0.13, MudBlazor 9.5.0, xUnit.

**Status of foundation (already committed on `phase-7-ui-shell`):**
- App project scaffold (Program.cs, App.razor, _Imports.razor, MainLayout, Home probe, index.html,
  app.css), bundled fonts (`wwwroot/fonts/*.woff2` + `css/fonts.css`) — builds clean.
- `Mvvm/ObservableObject.cs` + `App.Tests` project with `ObservableObjectTests` (2 passing).

**Conventions:** forward-slash paths; records for models; `Physical*`/`Engine*` seam impls not
unit-tested; xUnit only; run `dotnet` via PowerShell. Commit per task; `Co-Authored-By` trailer.

**Division of labor (execution note):** Tasks 1–6 are the **testable C# logic** (built TDD-first,
verified by `dotnet test`). Tasks 7–10 are the **presentation layer** (Razor + CSS, verified by
`dotnet build`, visually matched to the prototype). The chosen visual source of truth is
`ux-explorations/03-blueprint.html` (dashboard) — port its `<style>` tokens/components verbatim,
adapting class names to the components below.

---

## Task 1: Workspace context

**Files:** Create `src/ClaudeExplorer.App/Services/IWorkspaceContext.cs`,
`src/ClaudeExplorer.App/Services/WorkspaceContext.cs`; Test
`tests/ClaudeExplorer.App.Tests/Services/WorkspaceContextTests.cs`.

- [ ] **Test first** — `ProjectLabel` is the last path segment; paths are normalized to `/`:

```csharp
using ClaudeExplorer.App.Services;

namespace ClaudeExplorer.App.Tests.Services;

public class WorkspaceContextTests
{
    [Fact]
    public void ProjectLabel_is_the_final_path_segment()
    {
        var ctx = new WorkspaceContext("/home/u", "/work/my-app");
        Assert.Equal("/work/my-app", ctx.ProjectDir);
        Assert.Equal("/home/u", ctx.UserDir);
        Assert.Equal("my-app", ctx.ProjectLabel);
    }

    [Fact]
    public void Backslashes_are_normalized_and_trailing_slash_trimmed()
    {
        var ctx = new WorkspaceContext(@"C:\Users\u", @"C:\work\proj\");
        Assert.Equal("C:/work/proj", ctx.ProjectDir);
        Assert.Equal("proj", ctx.ProjectLabel);
    }
}
```

- [ ] **Implement** `IWorkspaceContext.cs`:

```csharp
namespace ClaudeExplorer.App.Services;

/// <summary>The workspace the app is currently inspecting: the user-global config dir and the
/// active project dir. (Multi-project compare is a later concern; v1 carries one project.)</summary>
public interface IWorkspaceContext
{
    /// <summary>Home dir holding <c>.claude/</c> (e.g. the user's profile dir).</summary>
    string UserDir { get; }
    /// <summary>Active project root (holds <c>.claude/</c> and <c>.mcp.json</c>).</summary>
    string ProjectDir { get; }
    /// <summary>Short display name for the project (final path segment).</summary>
    string ProjectLabel { get; }
}
```

- [ ] **Implement** `WorkspaceContext.cs`:

```csharp
namespace ClaudeExplorer.App.Services;

public sealed class WorkspaceContext : IWorkspaceContext
{
    public string UserDir { get; }
    public string ProjectDir { get; }
    public string ProjectLabel { get; }

    public WorkspaceContext(string userDir, string projectDir)
    {
        UserDir = Normalize(userDir);
        ProjectDir = Normalize(projectDir);
        var i = ProjectDir.LastIndexOf('/');
        ProjectLabel = i >= 0 && i < ProjectDir.Length - 1 ? ProjectDir[(i + 1)..] : ProjectDir;
    }

    private static string Normalize(string p) => p.Replace('\\', '/').TrimEnd('/');
}
```

- [ ] Run `dotnet test tests/ClaudeExplorer.App.Tests` → green. Commit:
  `feat(app): workspace context (user + project dirs)`

---

## Task 2: Dashboard data model

**Files:** Create `src/ClaudeExplorer.App/Dashboard/DashboardData.cs`,
`src/ClaudeExplorer.App/Dashboard/DashboardInputs.cs`. (Covered by Task 3 tests.)

- [ ] **Implement** `DashboardData.cs` — the view-ready model the page binds to:

```csharp
namespace ClaudeExplorer.App.Dashboard;

public enum BadgeTone { None, Ok, Warn, Bad }
public enum AttentionTone { Bad, Warn, Info }

/// <summary>One numbered stat card on the dashboard (Commands, MCP Servers, …).</summary>
public sealed record StatCard(string Label, string Index, int Value, string? Badge, BadgeTone Tone, string Sub);

/// <summary>A "needs attention" row: a missing dep, a conflict, or an inconclusive probe.</summary>
public sealed record AttentionItem(AttentionTone Tone, string Title, string Detail);

/// <summary>A recent reversible change for the "recent changes" panel.</summary>
public sealed record RecentChange(string Id, string Title, string Meta, bool IsUndone);

public sealed record DashboardData(
    int Health,
    string HealthCaption,
    string EffectiveForLabel,
    string MergeOrder,
    IReadOnlyList<StatCard> Stats,
    IReadOnlyList<AttentionItem> Attention,
    IReadOnlyList<RecentChange> RecentChanges);
```

- [ ] **Implement** `DashboardInputs.cs` — raw engine outputs for one workspace:

```csharp
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.App.Dashboard;

public sealed record DashboardInputs(
    EffectiveConfig Config,
    ArtifactCatalog Artifacts,
    DependencyReport Dependencies,
    IReadOnlyList<McpServer> McpServers,
    IReadOnlyList<ChangeLogEntry> RecentChanges,
    string ProjectLabel);
```

(No commit yet — committed with Task 3.)

---

## Task 3: DashboardComputer (pure derivation) + tests

**Files:** Create `src/ClaudeExplorer.App/Dashboard/DashboardComputer.cs`; Test
`tests/ClaudeExplorer.App.Tests/Dashboard/DashboardComputerTests.cs`.

**Derivation rules (deterministic):**
- `commands` = artifacts of kind Command; split sub by winner source kind (User/Project/Plugin).
- `skillsAgents` = Skill + Subagent count; sub = distinct plugin names among all winners.
- `mcpTotal` = MCP server count; `mcpDown` = servers whose name appears in `ReferencedBy`
  (`"mcp:<name>"`) of a **Missing** dependency; sub = `"{reachable} reachable"`.
- `depTotal` = dependency results; `depMissing` = Missing count; `depUnverifiable` = Unverifiable.
- `conflicts` = settings with `HasConflict`.
- `warnings` = shadowed artifacts (`IsShadowing`) + `depUnverifiable`.
- `health` = clamp(100 − 8·depMissing − 8·mcpDown − 3·conflicts, 0, 100).
- Attention = one row per Missing dep (Bad), per conflicting setting (Warn), per Unverifiable
  dep (Info), in that order.
- RecentChanges = input changes reversed (newest first), take 5.

- [ ] **Test first** — construct Core records directly (no IO). Full test file:

```csharp
using System.Text.Json.Nodes;
using ClaudeExplorer.App.Dashboard;
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.App.Tests.Dashboard;

public class DashboardComputerTests
{
    private static ResolvedArtifact Art(ArtifactKind kind, string name, ArtifactSourceKind src,
        string? plugin = null, bool shadowed = false)
    {
        var winner = new DiscoveredArtifact(kind, name, null, new ArtifactSource(src, plugin), $"/{name}");
        var shadow = shadowed
            ? new[] { new DiscoveredArtifact(kind, name, null, new ArtifactSource(ArtifactSourceKind.User), $"/u/{name}") }
            : Array.Empty<DiscoveredArtifact>();
        return new ResolvedArtifact(winner, shadow);
    }

    private static EffectiveSetting Setting(string key, bool conflict) =>
        new(key, MergeStrategy.ScalarLastWins, JsonValue.Create("x"), null,
            Array.Empty<SettingContribution>(), conflict);

    private static DependencyResult Dep(string name, DependencyStatusKind kind, params string[] referencedBy) =>
        new(new DependencyRef(name, name, referencedBy), new DependencyStatus(kind));

    private static DashboardInputs Inputs(
        IReadOnlyList<ResolvedArtifact>? artifacts = null,
        IReadOnlyList<EffectiveSetting>? settings = null,
        IReadOnlyList<DependencyResult>? deps = null,
        IReadOnlyList<McpServer>? mcp = null,
        IReadOnlyList<ChangeLogEntry>? changes = null) =>
        new(new EffectiveConfig(settings ?? Array.Empty<EffectiveSetting>()),
            new ArtifactCatalog(artifacts ?? Array.Empty<ResolvedArtifact>()),
            new DependencyReport(deps ?? Array.Empty<DependencyResult>()),
            mcp ?? Array.Empty<McpServer>(),
            changes ?? Array.Empty<ChangeLogEntry>(),
            "webapp");

    [Fact]
    public void Counts_commands_and_skills_with_source_breakdown()
    {
        var data = DashboardComputer.Compute(Inputs(artifacts: new[]
        {
            Art(ArtifactKind.Command, "a", ArtifactSourceKind.User),
            Art(ArtifactKind.Command, "b", ArtifactSourceKind.Project),
            Art(ArtifactKind.Skill, "s", ArtifactSourceKind.Plugin, "acme"),
            Art(ArtifactKind.Subagent, "g", ArtifactSourceKind.Plugin, "acme"),
        }));

        var commands = data.Stats.Single(s => s.Label == "Commands");
        Assert.Equal(2, commands.Value);
        Assert.Equal("01", commands.Index);
        Assert.Contains("1 user", commands.Sub);
        Assert.Contains("1 project", commands.Sub);

        var skills = data.Stats.Single(s => s.Label == "Skills+Agents");
        Assert.Equal(2, skills.Value);
        Assert.Contains("1 plugin", skills.Sub); // 1 distinct plugin (acme)
    }

    [Fact]
    public void Mcp_down_counts_servers_with_a_missing_dependency()
    {
        var data = DashboardComputer.Compute(Inputs(
            mcp: new[]
            {
                new McpServer("context7", "uvx", new[] { "context7" }, ScopeKind.Project),
                new McpServer("ok", "node", Array.Empty<string>(), ScopeKind.User),
            },
            deps: new[]
            {
                Dep("uvx", DependencyStatusKind.Missing, "mcp:context7"),
                Dep("node", DependencyStatusKind.Found, "mcp:ok"),
            }));

        var mcp = data.Stats.Single(s => s.Label == "MCP Servers");
        Assert.Equal(2, mcp.Value);
        Assert.Equal("1 down", mcp.Badge);
        Assert.Equal(BadgeTone.Bad, mcp.Tone);
        Assert.Contains("1 reachable", mcp.Sub);
    }

    [Fact]
    public void Dependency_card_flags_missing_count()
    {
        var data = DashboardComputer.Compute(Inputs(deps: new[]
        {
            Dep("node", DependencyStatusKind.Found),
            Dep("uvx", DependencyStatusKind.Missing, "mcp:x"),
        }));

        var dep = data.Stats.Single(s => s.Label == "Dependencies");
        Assert.Equal(2, dep.Value);
        Assert.Equal("1 miss", dep.Badge);
        Assert.Equal(BadgeTone.Warn, dep.Tone);
    }

    [Fact]
    public void Conflicts_and_warnings_are_counted()
    {
        var data = DashboardComputer.Compute(Inputs(
            settings: new[] { Setting("model", true), Setting("outputStyle", false) },
            artifacts: new[] { Art(ArtifactKind.Command, "dup", ArtifactSourceKind.Project, shadowed: true) },
            deps: new[] { Dep("docker", DependencyStatusKind.Unverifiable) }));

        Assert.Equal(1, data.Stats.Single(s => s.Label == "Conflicts").Value);
        // warnings = 1 shadowed artifact + 1 unverifiable dep
        Assert.Equal(2, data.Stats.Single(s => s.Label == "Warnings").Value);
    }

    [Fact]
    public void Health_subtracts_for_missing_conflicts_and_down_servers()
    {
        var data = DashboardComputer.Compute(Inputs(
            settings: new[] { Setting("model", true) },                       // -3
            mcp: new[] { new McpServer("c", "uvx", new[] { "c" }, ScopeKind.Project) },
            deps: new[] { Dep("uvx", DependencyStatusKind.Missing, "mcp:c") })); // -8 missing, -8 down

        Assert.Equal(100 - 8 - 8 - 3, data.Health);
    }

    [Fact]
    public void Health_is_clamped_to_zero()
    {
        var deps = Enumerable.Range(0, 20).Select(i => Dep($"d{i}", DependencyStatusKind.Missing)).ToArray();
        Assert.Equal(0, DashboardComputer.Compute(Inputs(deps: deps)).Health);
    }

    [Fact]
    public void Attention_lists_missing_then_conflict_then_unverifiable()
    {
        var data = DashboardComputer.Compute(Inputs(
            settings: new[] { Setting("model", true) },
            deps: new[]
            {
                Dep("uvx", DependencyStatusKind.Missing, "mcp:context7"),
                Dep("docker", DependencyStatusKind.Unverifiable),
            }));

        Assert.Collection(data.Attention,
            a => { Assert.Equal(AttentionTone.Bad, a.Tone); Assert.Contains("uvx", a.Title); },
            a => { Assert.Equal(AttentionTone.Warn, a.Tone); Assert.Contains("model", a.Title); },
            a => { Assert.Equal(AttentionTone.Info, a.Tone); Assert.Contains("docker", a.Title); });
    }

    [Fact]
    public void Recent_changes_are_newest_first_capped_at_five()
    {
        var changes = Enumerable.Range(1, 7).Select(i => new ChangeLogEntry(
            $"chg-{i}", "2026-06-07", ChangeKind.Edit, ScopeKind.Project, "/p", $"edit {i}",
            null, null, false)).ToList();

        var data = DashboardComputer.Compute(Inputs(changes: changes));

        Assert.Equal(5, data.RecentChanges.Count);
        Assert.Equal("edit 7", data.RecentChanges[0].Title);
        Assert.Equal("edit 3", data.RecentChanges[4].Title);
    }

    [Fact]
    public void Empty_inputs_give_full_health_and_no_rows()
    {
        var data = DashboardComputer.Compute(Inputs());
        Assert.Equal(100, data.Health);
        Assert.Empty(data.Attention);
        Assert.Empty(data.RecentChanges);
        Assert.Equal("webapp", data.EffectiveForLabel);
        Assert.Equal(6, data.Stats.Count);
    }
}
```

- [ ] **Implement** `DashboardComputer.cs`:

```csharp
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.App.Dashboard;

/// <summary>Pure derivation of <see cref="DashboardData"/> from raw engine outputs. No IO, so it
/// is fully unit-tested by constructing Core records directly.</summary>
public static class DashboardComputer
{
    public static DashboardData Compute(DashboardInputs input)
    {
        var commands = input.Artifacts.OfKind(ArtifactKind.Command).ToList();
        var cmdUser = commands.Count(a => a.Winner.Source.Kind == ArtifactSourceKind.User);
        var cmdProject = commands.Count(a => a.Winner.Source.Kind == ArtifactSourceKind.Project);
        var cmdPlugin = commands.Count(a => a.Winner.Source.Kind == ArtifactSourceKind.Plugin);

        var skillsAgents = input.Artifacts.OfKind(ArtifactKind.Skill).Count()
                         + input.Artifacts.OfKind(ArtifactKind.Subagent).Count();
        var pluginNames = input.Artifacts.Artifacts
            .Where(a => a.Winner.Source.Kind == ArtifactSourceKind.Plugin && a.Winner.Source.PluginName is not null)
            .Select(a => a.Winner.Source.PluginName!).Distinct().Count();

        var missingServerNames = input.Dependencies.Results
            .Where(r => r.Status.Kind == DependencyStatusKind.Missing)
            .SelectMany(r => r.Ref.ReferencedBy)
            .Where(b => b.StartsWith("mcp:", StringComparison.Ordinal))
            .Select(b => b["mcp:".Length..])
            .ToHashSet(StringComparer.Ordinal);
        var mcpTotal = input.McpServers.Count;
        var mcpDown = input.McpServers.Count(s => missingServerNames.Contains(s.Name));

        var depTotal = input.Dependencies.Results.Count;
        var depMissing = input.Dependencies.Count(DependencyStatusKind.Missing);
        var depUnverifiable = input.Dependencies.Count(DependencyStatusKind.Unverifiable);

        var conflicts = input.Config.Settings.Count(s => s.HasConflict);
        var shadowed = input.Artifacts.Artifacts.Count(a => a.IsShadowing);
        var warnings = shadowed + depUnverifiable;

        var health = Math.Clamp(100 - 8 * depMissing - 8 * mcpDown - 3 * conflicts, 0, 100);

        var stats = new List<StatCard>
        {
            new("Commands", "01", commands.Count, null, BadgeTone.None,
                $"{cmdUser} user / {cmdProject} project / {cmdPlugin} plugin"),
            new("Skills+Agents", "02", skillsAgents, null, BadgeTone.None,
                $"{pluginNames} plugin{(pluginNames == 1 ? "" : "s")}"),
            new("MCP Servers", "03", mcpTotal, mcpDown > 0 ? $"{mcpDown} down" : null,
                mcpDown > 0 ? BadgeTone.Bad : BadgeTone.None, $"{mcpTotal - mcpDown} reachable"),
            new("Dependencies", "04", depTotal, depMissing > 0 ? $"{depMissing} miss" : null,
                depMissing > 0 ? BadgeTone.Warn : BadgeTone.None,
                depMissing > 0 ? "missing on PATH" : "all resolved"),
            new("Conflicts", "05", conflicts, null, BadgeTone.None, "overrides resolved"),
            new("Warnings", "06", warnings, null, BadgeTone.None, "non-blocking"),
        };

        var attention = new List<AttentionItem>();
        foreach (var r in input.Dependencies.Results.Where(r => r.Status.Kind == DependencyStatusKind.Missing))
            attention.Add(new AttentionItem(AttentionTone.Bad, $"Missing {r.Ref.Name}",
                r.Ref.ReferencedBy.Count > 0 ? $"required by {string.Join(", ", r.Ref.ReferencedBy)}" : "unresolved on PATH"));
        foreach (var s in input.Config.Settings.Where(s => s.HasConflict))
            attention.Add(new AttentionItem(AttentionTone.Warn, $"{s.Key} conflict",
                "multiple scopes set this value"));
        foreach (var r in input.Dependencies.Results.Where(r => r.Status.Kind == DependencyStatusKind.Unverifiable))
            attention.Add(new AttentionItem(AttentionTone.Info, $"{r.Ref.Name} probe inconclusive",
                "present, not allowlisted for probing"));

        var recent = input.RecentChanges.Reverse()
            .Take(5)
            .Select(c => new RecentChange(c.Id, c.Description,
                $"{c.Scope} · {c.Timestamp}{(c.IsUndone ? " · undone" : "")}", c.IsUndone))
            .ToList();

        var caption = $"{depMissing} dep missing · {mcpDown} server down · {conflicts} conflicts";
        return new DashboardData(health, caption, input.ProjectLabel,
            "user → project → local", stats, attention, recent);
    }
}
```

- [ ] Run `dotnet test` → green. Commit:
  `feat(app): dashboard data model + pure computer over Core engines`

---

## Task 4: Dashboard data source seam

**Files:** Create `src/ClaudeExplorer.App/Dashboard/IDashboardDataSource.cs`,
`src/ClaudeExplorer.App/Dashboard/EngineDashboardDataSource.cs`; Test fake
`tests/ClaudeExplorer.App.Tests/Fakes/FakeDashboardDataSource.cs`.

- [ ] **Implement** `IDashboardDataSource.cs`:

```csharp
namespace ClaudeExplorer.App.Dashboard;

/// <summary>Gathers raw <see cref="DashboardInputs"/> for the current workspace. The engine impl
/// touches the file system / process runner, so it is not unit-tested; ViewModels are tested
/// against a fake.</summary>
public interface IDashboardDataSource
{
    DashboardInputs GetInputs();
}
```

- [ ] **Implement** `EngineDashboardDataSource.cs` (real impl; not unit-tested):

```csharp
using ClaudeExplorer.App.Services;
using ClaudeExplorer.Core;
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.App.Dashboard;

public sealed class EngineDashboardDataSource : IDashboardDataSource
{
    private readonly IWorkspaceContext _workspace;
    private readonly EffectiveConfigService _config;
    private readonly ArtifactCatalogService _artifacts;
    private readonly DependencyHealthService _health;
    private readonly McpServerReader _mcp;
    private readonly SafeMutationService _mutation;

    public EngineDashboardDataSource(
        IWorkspaceContext workspace,
        EffectiveConfigService config,
        ArtifactCatalogService artifacts,
        DependencyHealthService health,
        McpServerReader mcp,
        SafeMutationService mutation)
    {
        _workspace = workspace;
        _config = config;
        _artifacts = artifacts;
        _health = health;
        _mcp = mcp;
        _mutation = mutation;
    }

    public DashboardInputs GetInputs()
    {
        var user = _workspace.UserDir;
        var project = _workspace.ProjectDir;
        return new DashboardInputs(
            _config.Compute(user, project),
            _artifacts.Build(user, project),
            _health.Check(user, project),
            _mcp.Read(user, project),
            _mutation.ChangeLog.Entries,
            _workspace.ProjectLabel);
    }
}
```

- [ ] **Implement** test fake `FakeDashboardDataSource.cs`:

```csharp
using ClaudeExplorer.App.Dashboard;

namespace ClaudeExplorer.App.Tests.Fakes;

public sealed class FakeDashboardDataSource : IDashboardDataSource
{
    private readonly DashboardInputs _inputs;
    public int Calls { get; private set; }
    public FakeDashboardDataSource(DashboardInputs inputs) => _inputs = inputs;
    public DashboardInputs GetInputs() { Calls++; return _inputs; }
}
```

(No commit yet — committed with Task 5.)

---

## Task 5: ViewModels (Dashboard + Shell) + tests

**Files:** Create `src/ClaudeExplorer.App/ViewModels/DashboardViewModel.cs`,
`src/ClaudeExplorer.App/ViewModels/ShellViewModel.cs`; Test
`tests/ClaudeExplorer.App.Tests/ViewModels/DashboardViewModelTests.cs`.

- [ ] **Test first:**

```csharp
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
```

- [ ] **Implement** `DashboardViewModel.cs`:

```csharp
using ClaudeExplorer.App.Dashboard;
using ClaudeExplorer.App.Mvvm;

namespace ClaudeExplorer.App.ViewModels;

/// <summary>Loads the dashboard: pull raw inputs from the data source, run the pure computer,
/// expose the result. View binds to <see cref="Data"/> / <see cref="IsLoading"/>.</summary>
public sealed class DashboardViewModel : ObservableObject
{
    private readonly IDashboardDataSource _source;
    private DashboardData? _data;
    private bool _isLoading;

    public DashboardViewModel(IDashboardDataSource source) => _source = source;

    public DashboardData? Data { get => _data; private set => SetProperty(ref _data, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }

    public void Load()
    {
        IsLoading = true;
        try { Data = DashboardComputer.Compute(_source.GetInputs()); }
        finally { IsLoading = false; }
    }
}
```

- [ ] **Implement** `ShellViewModel.cs` (drives top bar + rail badges):

```csharp
using ClaudeExplorer.App.Dashboard;
using ClaudeExplorer.App.Mvvm;
using ClaudeExplorer.App.Services;

namespace ClaudeExplorer.App.ViewModels;

/// <summary>App-chrome state: the project label and a few rolled-up counts for the rail badges,
/// computed from the same dashboard inputs.</summary>
public sealed class ShellViewModel : ObservableObject
{
    private readonly IDashboardDataSource _source;
    private readonly IWorkspaceContext _workspace;
    private int _commandsAndSkills;
    private bool _hasDependencyProblem;
    private bool _hasMcpProblem;

    public ShellViewModel(IDashboardDataSource source, IWorkspaceContext workspace)
    {
        _source = source;
        _workspace = workspace;
    }

    public string ProjectLabel => _workspace.ProjectLabel;
    public int CommandsAndSkills { get => _commandsAndSkills; private set => SetProperty(ref _commandsAndSkills, value); }
    public bool HasDependencyProblem { get => _hasDependencyProblem; private set => SetProperty(ref _hasDependencyProblem, value); }
    public bool HasMcpProblem { get => _hasMcpProblem; private set => SetProperty(ref _hasMcpProblem, value); }

    public void Load()
    {
        var data = DashboardComputer.Compute(_source.GetInputs());
        var stat = data.Stats;
        CommandsAndSkills = Value(stat, "Commands") + Value(stat, "Skills+Agents");
        HasDependencyProblem = stat.Single(s => s.Label == "Dependencies").Badge is not null;
        HasMcpProblem = stat.Single(s => s.Label == "MCP Servers").Badge is not null;
    }

    private static int Value(IReadOnlyList<StatCard> stats, string label)
        => stats.Single(s => s.Label == label).Value;
}
```

- [ ] Run `dotnet test` → green. Commit:
  `feat(app): dashboard + shell view models with data-source seam`

---

## Task 6: DI wiring (Program.cs)

**Files:** Modify `src/ClaudeExplorer.App/Program.cs`.

- [ ] Register Core seams, façades, workspace, data source, ViewModels. Replace the service-
  registration block (after `AddMudServices`) so it reads:

```csharp
        builder.Services.AddLogging();
        builder.Services.AddMudServices();

        // Core seams (real machine impls).
        builder.Services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        builder.Services.AddSingleton<IFileWriter, PhysicalFileWriter>();
        builder.Services.AddSingleton<IPathResolver, PhysicalPathResolver>();
        builder.Services.AddSingleton<IProcessRunner>(_ => new PhysicalProcessRunner());

        // Workspace: user home (holds ~/.claude) + current dir as the active project.
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var project = Directory.GetCurrentDirectory();
        builder.Services.AddSingleton<IWorkspaceContext>(new WorkspaceContext(home, project));

        // Core façades.
        builder.Services.AddSingleton(sp => new EffectiveConfigService(sp.GetRequiredService<IFileSystem>()));
        builder.Services.AddSingleton(sp => new ArtifactCatalogService(sp.GetRequiredService<IFileSystem>()));
        builder.Services.AddSingleton(sp => new DependencyHealthService(
            sp.GetRequiredService<IFileSystem>(), sp.GetRequiredService<IPathResolver>(), sp.GetRequiredService<IProcessRunner>()));
        builder.Services.AddSingleton(sp => new McpServerReader(sp.GetRequiredService<IFileSystem>()));
        builder.Services.AddSingleton<IBackupStore>(sp => new FileBackupStore(
            sp.GetRequiredService<IFileSystem>(), sp.GetRequiredService<IFileWriter>(),
            $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).Replace('\\','/')}/.claude/.claude-explorer/backups"));
        builder.Services.AddSingleton(sp => new SafeMutationService(
            sp.GetRequiredService<IFileSystem>(), sp.GetRequiredService<IFileWriter>(),
            sp.GetRequiredService<IBackupStore>(), sp.GetRequiredService<IProcessRunner>()));

        // Dashboard data + view models.
        builder.Services.AddSingleton<IDashboardDataSource, EngineDashboardDataSource>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<ShellViewModel>();
```

  Add the needed `using` directives at the top:
```csharp
using ClaudeExplorer.App.Dashboard;
using ClaudeExplorer.App.Services;
using ClaudeExplorer.App.ViewModels;
using ClaudeExplorer.Core;
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Mutation;
```

- [ ] `dotnet build src/ClaudeExplorer.App` → clean. Commit:
  `feat(app): DI wiring for Core engines + view models`

---

## Task 7: Blueprint theme CSS

**Files:** Create `src/ClaudeExplorer.App/wwwroot/css/blueprint.css`; Modify
`wwwroot/index.html` (link it after `app.css`); Modify `wwwroot/css/app.css` (trim probe styles).

- [ ] Port the **`:root` token block and base/grid/panel/chrome styles** from
  `ux-explorations/03-blueprint.html`'s `<style>` into `blueprint.css` **verbatim** (the CSS custom
  properties — `--paper`, `--grid`, `--ink`, `--blue`, status colors — and the `body` graph-paper
  background, `.panel` corner-tick rules, `.topbar`, `.rail`, `.content`, `.pagehead`, `.k`, `.hero`,
  `.gauge`, `.stats`, `.stat`, `.pill`, `.lower`, `.card`, `.rowx`, `.glyph`, `.prov`, `.undo`
  blocks). Keep `font-family:"Archivo"` / `"Spline Sans Mono"` — the bundled @font-face provides
  them. Do **not** copy the prototype's Google Fonts `<link>` (we bundle).
- [ ] In `index.html`, add `<link href="css/blueprint.css" rel="stylesheet" />` after the `app.css`
  link. In `app.css`, remove the `.page-probe` rule (Home probe is replaced in Task 10); keep the
  `#blazor-error-ui` rule.
- [ ] `dotnet build` → clean. Commit: `feat(app): bundle Blueprint theme CSS`

---

## Task 8: Reusable Blueprint components

**Files:** Create under `src/ClaudeExplorer.App/Components/`: `CornerTickPanel.razor`, `Pill.razor`,
`HealthGauge.razor`, `StatCardView.razor`. Update `_Imports.razor` to `@using ClaudeExplorer.App.Components`.

These are logic-light wrappers over the ported CSS classes. Exact markup:

- [ ] `CornerTickPanel.razor` — the corner-tick panel container:

```razor
<div class="panel @Class">
    @ChildContent
</div>

@code {
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string? Class { get; set; }
}
```

- [ ] `Pill.razor` — a status pill (ok/warn/bad):

```razor
@using ClaudeExplorer.App.Dashboard
<span class="pill @ToneClass">@Text</span>

@code {
    [Parameter] public string Text { get; set; } = "";
    [Parameter] public BadgeTone Tone { get; set; } = BadgeTone.None;

    private string ToneClass => Tone switch
    {
        BadgeTone.Ok => "ok",
        BadgeTone.Warn => "warn",
        BadgeTone.Bad => "bad",
        _ => "",
    };
}
```

- [ ] `HealthGauge.razor` — the donut + caption (SVG ring; dash offset from the score):

```razor
<CornerTickPanel Class="gauge">
    <div class="ring">
        <svg width="148" height="148" viewBox="0 0 148 148">
            <circle cx="74" cy="74" r="62" fill="none" stroke="#D8DEE7" stroke-width="9" />
            <circle cx="74" cy="74" r="62" fill="none" stroke="var(--blue)" stroke-width="9"
                    stroke-dasharray="389" stroke-dashoffset="@Offset" />
        </svg>
        <div class="num"><div><b>@Score</b><small>health</small></div></div>
    </div>
    <div class="cap">@Caption</div>
</CornerTickPanel>

@code {
    [Parameter] public int Score { get; set; }
    [Parameter] public string Caption { get; set; } = "";
    // 389 = circumference; offset hides the (100-score)% remainder.
    private double Offset => 389 * (1 - Math.Clamp(Score, 0, 100) / 100.0);
}
```

- [ ] `StatCardView.razor` — one numbered stat card:

```razor
@using ClaudeExplorer.App.Dashboard
<CornerTickPanel Class="stat">
    <div class="num-tag"><span class="t">@Card.Label</span><span class="idx">@Card.Index</span></div>
    <div class="v">
        @Card.Value
        @if (Card.Badge is not null)
        {
            <Pill Text="@Card.Badge" Tone="@Card.Tone" />
        }
    </div>
    <div class="s">@Card.Sub</div>
</CornerTickPanel>

@code {
    [Parameter] public StatCard Card { get; set; } = default!;
}
```

- [ ] `dotnet build` → clean. Commit: `feat(app): reusable Blueprint components (panel, pill, gauge, stat)`

---

## Task 9: App chrome (top bar + left rail) + MainLayout

**Files:** Create `src/ClaudeExplorer.App/Components/TopBar.razor`,
`src/ClaudeExplorer.App/Components/LeftRail.razor`; Rewrite `src/ClaudeExplorer.App/Layout/MainLayout.razor`.

- [ ] `TopBar.razor` — brand + coordinate + project chip + refresh (port `.topbar` markup):

```razor
@inject IWorkspaceContext Workspace
<header class="topbar">
    <div class="brand"><span class="mark"></span> Claude&nbsp;Explorer</div>
    <span class="coord">FIG.01 — ENVIRONMENT SCHEMATIC</span>
    <div class="spacer"></div>
    <div class="proj"><span class="dot"></span> @Workspace.ProjectLabel</div>
    <div class="iconbtn" title="Refresh" @onclick="OnRefresh">
        <svg viewBox="0 0 24 24" width="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round">
            <path d="M21 12a9 9 0 1 1-3-6.7L21 8" /><path d="M21 3v5h-5" />
        </svg>
    </div>
</header>

@code {
    [Parameter] public EventCallback OnRefresh { get; set; }
}
```

- [ ] `LeftRail.razor` — sectioned nav with `NavLink` (port `.rail` markup; counts via `ShellViewModel`).
  Use `NavLink` to routes `/` (Dashboard), `/effective`, `/commands`, `/mcp`, `/dependencies`,
  `/marketplace`, `/changelog`. Show the `CommandsAndSkills` count on the Commands link, and a `1✗`
  bad badge on MCP/Dependencies when `HasMcpProblem`/`HasDependencyProblem`. Bind to an injected
  `ShellViewModel` parameter:

```razor
@using ClaudeExplorer.App.ViewModels
<aside class="rail">
    <div class="lbl">Workspace</div>
    <NavLink class="nav" href="/" Match="NavLinkMatch.All">@DashIcon Dashboard</NavLink>
    <NavLink class="nav" href="effective">@ConfigIcon Effective Config</NavLink>
    <NavLink class="nav" href="commands">@CmdIcon Commands &amp; Skills <span class="count">@Shell?.CommandsAndSkills</span></NavLink>
    <NavLink class="nav" href="mcp">@McpIcon MCP &amp; Plugins @if (Shell?.HasMcpProblem == true){<span class="count bad">1✗</span>}</NavLink>
    <NavLink class="nav" href="dependencies">@DepIcon Dependencies @if (Shell?.HasDependencyProblem == true){<span class="count bad">1✗</span>}</NavLink>
    <div class="lbl">Discover</div>
    <NavLink class="nav" href="marketplace">@MktIcon Marketplace</NavLink>
    <NavLink class="nav" href="changelog">@LogIcon Change Log</NavLink>
    <div class="foot">WORKSPACE LOADED<br>@(Shell?.ProjectLabel)<br>cli: see Dependencies</div>
</aside>

@code {
    [Parameter] public ShellViewModel? Shell { get; set; }
    // Icon fragments (port the prototype's inline <svg> for each nav item).
    private readonly RenderFragment DashIcon = @<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><rect x="3" y="3" width="7" height="9" rx="1"/><rect x="14" y="3" width="7" height="5" rx="1"/><rect x="14" y="12" width="7" height="9" rx="1"/><rect x="3" y="16" width="7" height="5" rx="1"/></svg>;
    private readonly RenderFragment ConfigIcon = @<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M12 3v18"/><path d="M5 7h14M5 12h9M5 17h6"/></svg>;
    private readonly RenderFragment CmdIcon = @<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="m8 8-4 4 4 4"/><path d="m16 8 4 4-4 4"/></svg>;
    private readonly RenderFragment McpIcon = @<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><rect x="3" y="3" width="18" height="18" rx="2"/><path d="M9 3v18M3 9h6"/></svg>;
    private readonly RenderFragment DepIcon = @<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M12 2 3 7v10l9 5 9-5V7z"/><path d="M3 7l9 5 9-5M12 12v10"/></svg>;
    private readonly RenderFragment MktIcon = @<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><circle cx="11" cy="11" r="7"/><path d="m21 21-4.3-4.3"/></svg>;
    private readonly RenderFragment LogIcon = @<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M3 12a9 9 0 1 0 9-9"/><path d="M12 7v5l3 2M3 4v5h5"/></svg>;
}
```

- [ ] Rewrite `MainLayout.razor` to compose the chrome (grid: topbar / rail / content):

```razor
@inherits LayoutComponentBase
@inject ShellViewModel Shell

<div class="app">
    <TopBar OnRefresh="Reload" />
    <LeftRail Shell="Shell" />
    <main class="content">
        @Body
    </main>
</div>

@code {
    protected override void OnInitialized()
    {
        Shell.PropertyChanged += (_, _) => InvokeAsync(StateHasChanged);
        Reload();
    }

    private void Reload() => Shell.Load();
}
```

  > Note: `.app` is the prototype's CSS grid (`grid-template-columns:230px 1fr; grid-template-rows:58px 1fr`)
  > with `.topbar` spanning both columns — already in `blueprint.css` from Task 7.

- [ ] `dotnet build` → clean. Commit: `feat(app): Blueprint app chrome (top bar + left rail + layout)`

---

## Task 10: Dashboard view + route stubs

**Files:** Create `src/ClaudeExplorer.App/Pages/Dashboard.razor` (route `/`); Delete
`src/ClaudeExplorer.App/Pages/Home.razor`; Create stub pages
`src/ClaudeExplorer.App/Pages/Stubs.razor` (one file with the remaining routes) OR individual stubs.

- [ ] `Dashboard.razor` — bind `DashboardViewModel.Data` to the components (port the `.pagehead`,
  `.hero`, `.stats`, `.lower` structure):

```razor
@page "/"
@using ClaudeExplorer.App.ViewModels
@inject DashboardViewModel Vm
@implements IDisposable

<div class="pagehead">
    <div>
        <div class="k">Environment Status</div>
        <h1>Dashboard</h1>
    </div>
    @if (Vm.Data is not null)
    {
        <div class="scope">EFFECTIVE FOR <b>@Vm.Data.EffectiveForLabel</b><br>MERGE: @Vm.Data.MergeOrder</div>
    }
</div>

@if (Vm.Data is { } data)
{
    <section class="hero">
        <HealthGauge Score="data.Health" Caption="@data.HealthCaption" />
        <div class="stats">
            @foreach (var card in data.Stats)
            {
                <StatCardView Card="card" />
            }
        </div>
    </section>

    <section class="lower">
        <CornerTickPanel Class="card">
            <h2>Needs Attention <span class="tag">precedence-resolved</span></h2>
            <div class="sub">// newest scope wins · click any node to trace source</div>
            @if (data.Attention.Count == 0)
            {
                <div class="rowx"><div class="body"><div class="ttl">All clear — no blocking issues.</div></div></div>
            }
            @foreach (var a in data.Attention)
            {
                <div class="rowx">
                    <div class="glyph @GlyphClass(a.Tone)"></div>
                    <div class="body"><div class="ttl">@a.Title</div><div class="meta">@a.Detail</div></div>
                </div>
            }
        </CornerTickPanel>

        <CornerTickPanel Class="card">
            <h2>Recent Changes <span class="tag">reversible</span></h2>
            <div class="sub">// logged per scope · backup before every write</div>
            @if (data.RecentChanges.Count == 0)
            {
                <div class="rowx"><div class="body"><div class="ttl">No changes yet.</div></div></div>
            }
            @foreach (var c in data.RecentChanges)
            {
                <div class="rowx">
                    <div class="glyph info"></div>
                    <div class="body"><div class="ttl">@c.Title</div><div class="meta">@c.Meta</div></div>
                </div>
            }
        </CornerTickPanel>
    </section>
}

@code {
    protected override void OnInitialized()
    {
        Vm.PropertyChanged += OnVmChanged;
        Vm.Load();
    }

    private void OnVmChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => InvokeAsync(StateHasChanged);

    private static string GlyphClass(Dashboard.AttentionTone tone) => tone switch
    {
        Dashboard.AttentionTone.Bad => "bad",
        Dashboard.AttentionTone.Warn => "warn",
        _ => "info",
    };

    public void Dispose() => Vm.PropertyChanged -= OnVmChanged;
}
```

- [ ] Delete `Pages/Home.razor`. Create stub pages so the rail nav resolves — one route per
  not-yet-built screen, each a simple "coming in Phase 8" panel. Example `Pages/EffectiveConfigStub.razor`:

```razor
@page "/effective"
<div class="pagehead"><div><div class="k">Configuration</div><h1>Effective Config</h1></div></div>
<CornerTickPanel Class="card"><div class="sub" style="padding:18px">Built in Phase 8.</div></CornerTickPanel>
```

  Repeat for routes `/commands`, `/mcp`, `/dependencies`, `/marketplace`, `/changelog` (titles:
  Commands & Skills, MCP & Plugins, Dependencies, Marketplace, Change Log).

- [ ] `dotnet build` → clean; `dotnet test` → all green. Commit:
  `feat(app): Blueprint dashboard view + route stubs`

---

## Task 11: Docs + manual-run note

**Files:** Modify `docs/superpowers/plans/2026-06-07-00-roadmap.md`, `docs/superpowers/HANDOFF.md`.

- [ ] Mark Phase 7 done in the roadmap status table (commit range filled post-merge); update test
  count; set HANDOFF "Next up" to **Phase 8 — Per-screen UI**, and record the App architecture
  (Photino+MudBlazor shell, MVVM, Blueprint theme, bundled fonts, DashboardComputer pattern).
- [ ] Note: the app's **visual/runtime** behavior is verified by a human via `/run` (Photino opens
  a native window — not observable headless). `dotnet build` + ViewModel/computer tests are the
  automated gates.
- [ ] Commit: `docs: mark Phase 7 (UI shell + dashboard) done; next Phase 8`

---

## Self-Review

**Spec coverage (Phase 7 deliverables):** App project (Photino.Blazor) ✅ Task 0/foundation; MVVM
(ObservableObject + ViewModels + DI) ✅ Tasks 1,5,6; Blueprint theme/tokens ✅ Task 7; bundled fonts
✅ foundation; reusable component library ✅ Task 8; app chrome (rail + top bar + project selector +
refresh) ✅ Task 9; Dashboard (health, counts, conflicts, recent changes) ✅ Tasks 3,10; ViewModel
tests ✅ Tasks 1,3,5; manual run via /run ✅ Task 11.

**Placeholder scan:** C# tasks carry complete code + tests; presentation tasks carry complete Razor
and precise "port these blocks from `03-blueprint.html`" instructions (a concrete in-repo source,
not a placeholder).

**Type consistency:** `DashboardData`/`StatCard`/`AttentionItem`/`RecentChange`/`BadgeTone`/
`AttentionTone`, `DashboardInputs`, `DashboardComputer.Compute`, `IDashboardDataSource.GetInputs`,
`DashboardViewModel.{Data,IsLoading,Load}`, `ShellViewModel.{ProjectLabel,CommandsAndSkills,
HasDependencyProblem,HasMcpProblem,Load}`, `IWorkspaceContext.{UserDir,ProjectDir,ProjectLabel}`,
component params (`CornerTickPanel.Class/ChildContent`, `Pill.Text/Tone`, `StatCardView.Card`,
`HealthGauge.Score/Caption`, `TopBar.OnRefresh`, `LeftRail.Shell`) — consistent across tasks and
match the Core façade signatures verified during planning.

**Test isolation:** computer/ViewModel tests construct Core records directly or use
`FakeDashboardDataSource`; no filesystem, no Photino. `EngineDashboardDataSource`,
`Physical*`/`WorkspaceContext`-from-env are wired only in `Program.cs` (not unit-tested), consistent
with the project's seam convention.
