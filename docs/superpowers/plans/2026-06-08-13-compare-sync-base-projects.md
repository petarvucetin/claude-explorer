# Compare / Sync (Base + Projects) — Implementation Plan (Phases A · B · C)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Generalize the env-vs-env Compare screen so endpoints are **bases + projects**, compare each endpoint's **owned** config across all categories (incl. a new **Memory** category), and let the user **copy/move** any row in either direction through safe-mutation.

**Architecture:** A `CompareEndpoint` (Base = an environment `~/.claude`; Project = an added folder) maps to a `(readUserDir, readProjectDir)` pair the existing Core readers already accept — Base = `(userDir, "")`, Project = `("", projectDir)`. A persisted `ProjectRegistry` holds added projects. The pure `EnvironmentComparer` gains a Memory category + a view-only flag. Phase C adds per-type copy operations (`SettingsKeyEditor`, file/MCP/hook copy) routed through the existing `SafeMutationService`.

**Tech Stack:** .NET 10, xUnit, Photino.Blazor + MudBlazor, `System.Text.Json.Nodes`.

**Spec:** `docs/superpowers/specs/2026-06-08-compare-sync-base-projects-design.md`. Mockups (committed): `ux-explorations/nav-reorg.html`, `ux-explorations/compare-sync.html` (UI tasks copy markup/CSS from these).

**Conventions (verified against the repo):** xUnit (`[Fact]`/`[Theory]`, underscore names). App tests live in `tests/ClaudeExplorer.App.Tests/`, Core in `tests/ClaudeExplorer.Core.Tests/`; both have an `InMemoryFileSystem` fake (implements `IFileSystem`+`IFileWriter`, `AddFile`). Compare code is in the **App** project, namespace `ClaudeExplorer.App.Compare`. The existing diff model: `enum DiffStatus { Same, Differs, OnlyA, OnlyB }`, `record CompareRow(string Key, DiffStatus Status, string? ValueA, string? ValueB)`, `record CompareCategory(string Name, IReadOnlyList<CompareRow> Rows)` (computed `Same/Differs/OnlyA/OnlyB` counts), `record EnvironmentComparison(IReadOnlyList<CompareCategory> Categories)` with `Find(name)`. `BuildCategory(name, dictA, dictB)` diffs two `IReadOnlyDictionary<string,string>` (value is used for BOTH compare and display).

Run one filtered: `dotnet test tests/ClaudeExplorer.App.Tests/ClaudeExplorer.App.Tests.csproj --filter "FullyQualifiedName~CompareEndpointTests"`
Run all: `dotnet test ClaudeExplorer.slnx`

---

## PHASE A — Left-rail IA reorg

### Task A1: Move Hooks + fold Extensions into Config Artifacts

**Files:** Modify `src/ClaudeExplorer.App/Components/LeftRail.razor` (no test — render-only; gate = build).

- [ ] **Step 1: Edit the rail markup.** In `LeftRail.razor`: (a) delete the `Hooks` `<NavLink>` (lines ~15-18) from the **Workspace** group; (b) delete the entire `<div class="lbl">Extensions</div>` label (line ~38); (c) move the `Hooks` NavLink and the `MCP`+`Plugins` NavLinks so they sit **inside Config Artifacts**, after `Subagents`. Result order:

```razor
    <div class="lbl">Workspace</div>
    <NavLink class="nav" href="/" Match="NavLinkMatch.All"> … Dashboard </NavLink>
    <NavLink class="nav" href="effective"> … Effective Config </NavLink>
    <NavLink class="nav" href="dependencies"> … Dependencies @if (Shell?.HasDependencyProblem == true) { <span class="count bad">1&#x2717;</span> } </NavLink>

    <div class="lbl">Config Artifacts</div>
    <NavLink class="nav" href="commands"> … Commands <span class="count">@Shell?.Commands</span> </NavLink>
    <NavLink class="nav" href="skills"> … Skills <span class="count">@Shell?.Skills</span> </NavLink>
    <NavLink class="nav" href="subagents"> … Subagents <span class="count">@Shell?.Subagents</span> </NavLink>
    <NavLink class="nav" href="hooks"> … Hooks @if (Shell?.HasHookProblem == true) { <span class="count bad">1&#x2717;</span> } else { <span class="count">@Shell?.Hooks</span> } </NavLink>
    <NavLink class="nav" href="mcp"> … MCP @if (Shell?.HasMcpProblem == true) { <span class="count bad">1&#x2717;</span> } else { <span class="count">@Shell?.Mcp</span> } </NavLink>
    <NavLink class="nav" href="plugins"> … Plugins <span class="count">@Shell?.Plugins</span> </NavLink>
```

Keep each NavLink's existing `<svg>` icon and count exactly as they are today — only their grouping/order changes. Leave the `Analyze` and `Discover` groups untouched.

- [ ] **Step 2: Build.** Run `dotnet build src/ClaudeExplorer.App/ClaudeExplorer.App.csproj -v quiet -clp:NoSummary` → `Build succeeded`, 0 errors.
- [ ] **Step 3: Commit.**
```bash
git add src/ClaudeExplorer.App/Components/LeftRail.razor
git commit -m "feat(app): fold Extensions + move Hooks into Config Artifacts (nav reorg)"
```
(append `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>` to every commit body.)

---

## PHASE B — Project endpoints + generalized read-only Compare

### Task B1: `CompareEndpoint` model + read-path mapping

**Files:** Create `src/ClaudeExplorer.App/Compare/CompareEndpoint.cs`; Test `tests/ClaudeExplorer.App.Tests/Compare/CompareEndpointTests.cs`.

- [ ] **Step 1: Failing test**
```csharp
using ClaudeExplorer.App.Compare;

namespace ClaudeExplorer.App.Tests.Compare;

public class CompareEndpointTests
{
    [Fact]
    public void Base_reads_user_dir_with_no_project()
    {
        var ep = CompareEndpoint.Base("win", "Base · Windows", "C:/Users/me");
        Assert.Equal(EndpointKind.Base, ep.Kind);
        Assert.Equal("C:/Users/me", ep.ReadUserDir);
        Assert.Equal("", ep.ReadProjectDir);
    }

    [Fact]
    public void Project_reads_project_dir_with_no_user()
    {
        var ep = CompareEndpoint.Project("p1", "Project A", "D:/work/a");
        Assert.Equal(EndpointKind.Project, ep.Kind);
        Assert.Equal("", ep.ReadUserDir);
        Assert.Equal("D:/work/a", ep.ReadProjectDir);
    }
}
```
- [ ] **Step 2: Run → FAIL** (`CompareEndpoint` missing). `dotnet test tests/ClaudeExplorer.App.Tests/ClaudeExplorer.App.Tests.csproj --filter "FullyQualifiedName~CompareEndpointTests"`
- [ ] **Step 3: Implement** — `src/ClaudeExplorer.App/Compare/CompareEndpoint.cs`:
```csharp
namespace ClaudeExplorer.App.Compare;

public enum EndpointKind { Base, Project }

/// <summary>A comparison endpoint: a base (an environment's <c>~/.claude</c> root) or a project
/// folder. <see cref="ReadUserDir"/>/<see cref="ReadProjectDir"/> are the (userDir, projectDir) the
/// Core readers take to read this endpoint's OWNED config — a base reads as user-only, a project reads
/// as project-only (no base overlay), so copy acts on the files that actually live there.</summary>
public sealed record CompareEndpoint(string Id, EndpointKind Kind, string Label, string UserDir, string? ProjectDir)
{
    public string ReadUserDir => Kind == EndpointKind.Base ? UserDir : "";
    public string ReadProjectDir => Kind == EndpointKind.Base ? "" : (ProjectDir ?? "");

    public static CompareEndpoint Base(string id, string label, string userDir)
        => new($"base:{id}", EndpointKind.Base, label, userDir, null);

    public static CompareEndpoint Project(string id, string label, string projectDir)
        => new($"proj:{id}", EndpointKind.Project, label, "", projectDir);
}
```
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5: Commit** `feat(app): CompareEndpoint model (base/project read-path mapping)`.

---

### Task B2: `ProjectRegistry` + persisted `ComparedProjects`

**Files:** Modify `src/ClaudeExplorer.App/Environments/EnvironmentStore.cs` (add field to `EnvironmentState`); Create `src/ClaudeExplorer.App/Environments/ProjectRegistry.cs`; Test `tests/ClaudeExplorer.App.Tests/Environments/ProjectRegistryTests.cs`.

- [ ] **Step 1: Failing test**
```csharp
using ClaudeExplorer.App.Environments;
using ClaudeExplorer.App.Tests.Fakes;

namespace ClaudeExplorer.App.Tests.Environments;

public class ProjectRegistryTests
{
    private const string Path = "/home/.claude/.claude-explorer/environments.json";

    private static (ProjectRegistry reg, InMemoryFileSystem fs) Build()
    {
        var fs = new InMemoryFileSystem();
        var store = new EnvironmentStore(fs, fs, Path);
        var reg = new ProjectRegistry(store);
        reg.Load();
        return (reg, fs);
    }

    [Fact]
    public void Add_then_load_round_trips()
    {
        var (reg, fs) = Build();
        reg.Add("Project A", "win", "D:/work/a");

        var reg2 = new ProjectRegistry(new EnvironmentStore(fs, fs, Path));
        reg2.Load();

        var p = Assert.Single(reg2.All);
        Assert.Equal("Project A", p.Name);
        Assert.Equal("D:/work/a", p.ProjectDir);
        Assert.Equal("win", p.EnvId);
    }

    [Fact]
    public void Remove_drops_the_project()
    {
        var (reg, _) = Build();
        reg.Add("A", "win", "D:/a");
        var id = reg.All.Single().Id;
        reg.Remove(id);
        Assert.Empty(reg.All);
    }

    [Fact]
    public void Add_is_idempotent_by_env_and_dir()
    {
        var (reg, _) = Build();
        reg.Add("A", "win", "D:/a");
        reg.Add("A again", "win", "D:/a");
        Assert.Single(reg.All);
    }
}
```
- [ ] **Step 2: Run → FAIL.** filter `ProjectRegistryTests`.
- [ ] **Step 3a: Add the persisted field** — in `EnvironmentStore.cs`, add to `EnvironmentState`:
```csharp
    public List<ProjectEndpointDef> ComparedProjects { get; init; } = new();
```
and include it in the parameterized constructor (add a param `IEnumerable<ProjectEndpointDef>? ComparedProjects = null` → `this.ComparedProjects = ComparedProjects is null ? new() : new(ComparedProjects);`) and in `Empty`. Define the record in `ProjectRegistry.cs` (Step 3b).
- [ ] **Step 3b: Implement** — `src/ClaudeExplorer.App/Environments/ProjectRegistry.cs`:
```csharp
namespace ClaudeExplorer.App.Environments;

/// <summary>A user-registered project folder used as a Compare endpoint.</summary>
public sealed record ProjectEndpointDef(string Id, string Name, string EnvId, string ProjectDir);

/// <summary>
/// Owns the list of project folders the user added as Compare endpoints, persisted in the shared
/// <see cref="EnvironmentState"/> (field <c>ComparedProjects</c>). Independent of the per-environment
/// active project (<see cref="EnvironmentService"/>) — registering a Compare endpoint never repoints
/// the active workspace. Mirrors EnvironmentService's observable shape.
/// </summary>
public sealed class ProjectRegistry
{
    private readonly EnvironmentStore _store;
    private readonly List<ProjectEndpointDef> _projects = new();

    public event Action? Changed;

    public ProjectRegistry(EnvironmentStore store) => _store = store;

    public IReadOnlyList<ProjectEndpointDef> All => _projects;

    public void Load()
    {
        _projects.Clear();
        _projects.AddRange(_store.Load().ComparedProjects);
        Changed?.Invoke();
    }

    public void Add(string name, string envId, string projectDir)
    {
        var dir = projectDir.Replace('\\', '/').TrimEnd('/');
        var id = $"{envId}|{dir}";
        if (_projects.Any(p => p.Id == id)) return;
        _projects.Add(new ProjectEndpointDef(id, name, envId, dir));
        Persist();
        Changed?.Invoke();
    }

    public void Remove(string id)
    {
        if (_projects.RemoveAll(p => p.Id == id) > 0) { Persist(); Changed?.Invoke(); }
    }

    // Preserve the rest of EnvironmentState (active id, custom envs, active-project map) on save.
    private void Persist()
    {
        var s = _store.Load();
        _store.Save(new EnvironmentState
        {
            ActiveId = s.ActiveId,
            Custom = s.Custom,
            Projects = s.Projects,
            ComparedProjects = new List<ProjectEndpointDef>(_projects),
        });
    }
}
```
> Note: `EnvironmentState` uses object-initializer-settable `init` props (it has a `[JsonConstructor]` parameterless ctor), so the `new EnvironmentState { … }` form above works. Confirm `Custom`/`Projects` are `init`-settable (they are).
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5: Commit** `feat(app): ProjectRegistry — persisted project compare endpoints`.

---

### Task B3: Generalize the snapshot to an endpoint (+ Memory)

**Files:** Modify `src/ClaudeExplorer.App/Compare/CompareModels.cs` (add `Memory` to `EnvironmentSnapshot`); Modify `src/ClaudeExplorer.App/Compare/IEnvironmentCompareDataSource.cs` + `EngineEnvironmentCompareDataSource.cs` (snapshot by endpoint). No unit test (engine impl is headless per repo convention — Memory map is tested via the comparer in B4). Gate = build.

- [ ] **Step 1: Add `Memory` to the snapshot.** In `CompareModels.cs`, add a field to `EnvironmentSnapshot` (it currently holds Settings/Artifacts/Mcp/Plugins/Dependencies) — append:
```csharp
    // name (e.g. "CLAUDE.md") → content; empty when the file is absent.
    IReadOnlyDictionary<string, string> Memory
```
as the last positional parameter of the `EnvironmentSnapshot` record.
- [ ] **Step 2: Endpoint snapshot.** In `IEnvironmentCompareDataSource.cs` add an overload:
```csharp
    EnvironmentSnapshot Snapshot(CompareEndpoint endpoint);
```
In `EngineEnvironmentCompareDataSource.cs` implement it (reuse the existing readers with the endpoint's read-paths; pass an **empty** plugin list so only OWNED user/project artifacts are compared; read Memory per kind):
```csharp
public EnvironmentSnapshot Snapshot(CompareEndpoint endpoint)
{
    var u = endpoint.ReadUserDir;
    var p = endpoint.ReadProjectDir;
    return new EnvironmentSnapshot(
        _config.Compute(u, p).Settings,
        _artifacts.Build(u, p, plugins: System.Array.Empty<ClaudeExplorer.Core.Artifacts.PluginLocation>()),
        _mcp.Read(u, p),
        endpoint.Kind == EndpointKind.Base ? _plugins.Read(u).ToList() : new System.Collections.Generic.List<string>(),
        _health.Check(u, p),
        ReadMemory(endpoint));
}

private System.Collections.Generic.Dictionary<string, string> ReadMemory(CompareEndpoint e)
{
    var mem = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);
    if (e.Kind == EndpointKind.Base)
        AddMemory(mem, "CLAUDE.md", $"{e.UserDir}/.claude/CLAUDE.md");
    else
    {
        AddMemory(mem, "CLAUDE.md", $"{e.ProjectDir}/CLAUDE.md");
        AddMemory(mem, "CLAUDE.local.md", $"{e.ProjectDir}/CLAUDE.local.md");
    }
    return mem;
}

private void AddMemory(System.Collections.Generic.Dictionary<string, string> mem, string name, string path)
{
    if (_fs.FileExists(path)) mem[name] = _fs.ReadAllText(path);
}
```
Inject `IFileSystem _fs` into `EngineEnvironmentCompareDataSource` (add to its constructor + DI in B6). Keep the existing `Snapshot(ClaudeEnvironment env)` method (used by current tests) — have it delegate: `Snapshot(CompareEndpoint.Base(env.Id, env.Name, env.UserDir))`.
- [ ] **Step 3: Build** → fix call sites of the `EnvironmentSnapshot` constructor (the comparer's tests build snapshots — update them in B4). `dotnet build` may fail until B4 updates test snapshots; that's expected — build the App project (not tests) here: `dotnet build src/ClaudeExplorer.App/ClaudeExplorer.App.csproj -v quiet -clp:NoSummary` → succeeds.
- [ ] **Step 4: Commit** `feat(app): snapshot a CompareEndpoint's owned config (+ Memory)`.

---

### Task B4: Comparer — Memory category + view-only flag

**Files:** Modify `src/ClaudeExplorer.App/Compare/CompareModels.cs` (add `ViewOnly` to `CompareCategory`); Modify `src/ClaudeExplorer.App/Compare/EnvironmentComparer.cs` (Memory map + category); Test `tests/ClaudeExplorer.App.Tests/Compare/EnvironmentComparerTests.cs` (add cases; fix snapshot ctor calls).

- [ ] **Step 1: Failing test** — add to `EnvironmentComparerTests.cs` (and update its snapshot-building helper to pass a `Memory` dict — `new Dictionary<string,string>()` for existing cases):
```csharp
    [Fact]
    public void Memory_category_diffs_claude_md_by_content()
    {
        var a = Snap(memory: new() { ["CLAUDE.md"] = "rules v1", ["CLAUDE.local.md"] = "x" });
        var b = Snap(memory: new() { ["CLAUDE.md"] = "rules v2" });

        var cat = EnvironmentComparer.Compare(a, b).Find("Memory")!;

        Assert.Equal(DiffStatus.Differs, cat.Rows.Single(r => r.Key == "CLAUDE.md").Status);
        Assert.Equal(DiffStatus.OnlyA, cat.Rows.Single(r => r.Key == "CLAUDE.local.md").Status);
    }

    [Fact]
    public void Plugins_and_dependencies_categories_are_view_only()
    {
        var cmp = EnvironmentComparer.Compare(Snap(), Snap());
        Assert.True(cmp.Find("Plugins")!.ViewOnly);
        Assert.True(cmp.Find("Dependencies")!.ViewOnly);
        Assert.False(cmp.Find("Settings")!.ViewOnly);
    }
```
Add a `Snap(...)` helper to the test class with a `memory` parameter (default empty) that builds an `EnvironmentSnapshot` with empty Settings/Artifacts/Mcp/Plugins/Dependencies + the given memory. (Mirror however the file already builds snapshots; just thread `memory` through and default it.)
- [ ] **Step 2: Run → FAIL.** filter `EnvironmentComparerTests`.
- [ ] **Step 3a: `ViewOnly` flag** — in `CompareModels.cs` change `CompareCategory` to carry it:
```csharp
public sealed record CompareCategory(string Name, IReadOnlyList<CompareRow> Rows, bool ViewOnly = false)
{
    public int Same => Rows.Count(r => r.Status == DiffStatus.Same);
    public int Differs => Rows.Count(r => r.Status == DiffStatus.Differs);
    public int OnlyA => Rows.Count(r => r.Status == DiffStatus.OnlyA);
    public int OnlyB => Rows.Count(r => r.Status == DiffStatus.OnlyB);
}
```
- [ ] **Step 3b: Memory map + category + view-only** — in `EnvironmentComparer.cs`:
  - Add to the `Compare` category list, after `BuildCategory("Settings", …)`:
    `BuildCategory("Memory", MemoryMap(a), MemoryMap(b)),`
  - Mark the last two view-only by replacing their two list entries with:
    `BuildCategory("Plugins", PluginMap(a), PluginMap(b), viewOnly: true),`
    `BuildCategory("Dependencies", DepMap(a), DepMap(b), viewOnly: true),`
  - Add the `viewOnly` param to `BuildCategory` and pass it through:
    `private static CompareCategory BuildCategory(string name, IReadOnlyDictionary<string,string> a, IReadOnlyDictionary<string,string> b, bool viewOnly = false)` … `return new CompareCategory(name, rows, viewOnly);`
  - Add the map (content compared directly so Differs is accurate; display value is a short descriptor with an 8-char content hash):
```csharp
    private static Dictionary<string, string> MemoryMap(EnvironmentSnapshot s)
        => s.Memory.ToDictionary(kv => kv.Key, kv => Descriptor(kv.Value), StringComparer.Ordinal);

    private static string Descriptor(string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))[..8];
        return $"present · {bytes.Length} B · {hash}";
    }
```
- [ ] **Step 4: Run → PASS** (new + existing comparer tests).
- [ ] **Step 5: Commit** `feat(app): Compare gains Memory category + view-only Plugins/Deps`.

---

### Task B5: `CompareViewModel` — endpoint-pair selection

**Files:** Modify `src/ClaudeExplorer.App/Compare/CompareViewModel.cs`; Test `tests/ClaudeExplorer.App.Tests/Compare/CompareViewModelTests.cs` (extend).

- [ ] **Step 1: Failing test** — add (the test fake `IEnvironmentCompareDataSource` must implement the new `Snapshot(CompareEndpoint)` overload — update the fake to return a canned snapshot for any endpoint):
```csharp
    [Fact]
    public void Endpoints_list_includes_bases_and_registered_projects()
    {
        var vm = BuildVm(); // envs: win, wsl ; projects: Project A
        var ids = vm.Endpoints.Select(e => e.Id).ToList();
        Assert.Contains("base:win", ids);
        Assert.Contains("proj:" /*prefix*/, ids.First(i => i.StartsWith("proj:")));
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
```
Add a `BuildVm()` helper that constructs an `EnvironmentService` (with a fake discovery returning win+wsl) and a `ProjectRegistry` (with one added project), plus a fake data source whose `Snapshot(CompareEndpoint)` returns an empty-but-valid snapshot.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement** — change `CompareViewModel` to take a `ProjectRegistry` and expose endpoints + endpoint selection. Replace the env-pair members:
```csharp
public sealed class CompareViewModel : ObservableObject
{
    private readonly EnvironmentService _environments;
    private readonly ProjectRegistry _projects;
    private readonly IEnvironmentCompareDataSource _source;

    private CompareEndpoint? _left, _right;
    private EnvironmentComparison? _comparison;
    private CompareCategory? _selected;
    private bool _isLoading;
    private string? _error;

    public CompareViewModel(EnvironmentService environments, ProjectRegistry projects, IEnvironmentCompareDataSource source)
    {
        _environments = environments;
        _projects = projects;
        _source = source;
    }

    public IReadOnlyList<CompareEndpoint> Endpoints =>
        _environments.Environments.Select(e => CompareEndpoint.Base(e.Id, e.Name, e.UserDir))
            .Concat(_projects.All.Select(p => CompareEndpoint.Project(p.Id, p.Name, p.ProjectDir)))
            .ToList();

    public CompareEndpoint? LeftEndpoint { get => _left; private set => SetProperty(ref _left, value); }
    public CompareEndpoint? RightEndpoint { get => _right; private set => SetProperty(ref _right, value); }
    public EnvironmentComparison? Comparison { get => _comparison; private set => SetProperty(ref _comparison, value); }
    public CompareCategory? SelectedCategory { get => _selected; private set => SetProperty(ref _selected, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string? ErrorMessage { get => _error; private set => SetProperty(ref _error, value); }

    public void SetEndpoints(string leftId, string rightId)
    {
        var eps = Endpoints;
        LeftEndpoint = eps.FirstOrDefault(e => e.Id == leftId);
        RightEndpoint = eps.FirstOrDefault(e => e.Id == rightId);
        Load();
    }

    public void Swap() { (LeftEndpoint, RightEndpoint) = (RightEndpoint, LeftEndpoint); Load(); }

    public void SelectCategory(string name) => SelectedCategory = Comparison?.Find(name) ?? SelectedCategory;

    public void Load()
    {
        IsLoading = true;
        try
        {
            var eps = Endpoints;
            LeftEndpoint ??= eps.FirstOrDefault();
            RightEndpoint ??= eps.Skip(1).FirstOrDefault() ?? LeftEndpoint;
            if (LeftEndpoint is null || RightEndpoint is null)
            {
                ErrorMessage = "Add at least two endpoints (a base + a project) to compare.";
                Comparison = null;
                return;
            }
            ErrorMessage = null;
            Comparison = EnvironmentComparer.Compare(_source.Snapshot(LeftEndpoint), _source.Snapshot(RightEndpoint));
            SelectedCategory = Comparison.Categories.FirstOrDefault();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }
}
```
- [ ] **Step 4: Run → PASS.** (Also update any other test/file referencing the removed `SetEnvironments`/`LeftEnv`/`RightEnv` — search and fix.)
- [ ] **Step 5: Commit** `feat(app): CompareViewModel selects base/project endpoints`.

---

### Task B6: Compare screen UI (endpoint pickers + Add project) + DI

**Files:** Modify `src/ClaudeExplorer.App/Pages/Compare.razor`; Modify `src/ClaudeExplorer.App/Program.cs` (register `ProjectRegistry`, inject `IFileSystem` into the data source, update `CompareViewModel` ctor); optionally `wwwroot/css/blueprint.css`. No unit test — build + `/run`. **Copy the exact endpoint-picker / tab / table markup + classes from the committed mockup `ux-explorations/compare-sync.html`** (it uses real blueprint tokens).

- [ ] **Step 1: DI.** In `Program.cs`: register `builder.Services.AddSingleton(sp => { var r = new ProjectRegistry(sp.GetRequiredService<EnvironmentStore>()); r.Load(); return r; });`. Update the `EngineEnvironmentCompareDataSource` registration to pass `IFileSystem` (its new ctor param). Update the `CompareViewModel` transient registration to also resolve `ProjectRegistry` (if it's a plain `AddTransient<CompareViewModel>()`, DI resolves the new ctor automatically — verify the new ctor's deps are all registered: `EnvironmentService`, `ProjectRegistry`, `IEnvironmentCompareDataSource` ✓).
- [ ] **Step 2: Rewrite `Compare.razor`** to: bind two **endpoint pickers** (`<select>` or a dropdown) over `Vm.Endpoints` (group bases vs projects; show `Label`), a **⇄ swap** button (`Vm.Swap()`), and an **"＋ Add project…"** action opening a modal (path + name inputs, mirroring `EnvironmentSelector.razor`'s Add-custom modal) that calls the injected `ProjectRegistry.Add(name, activeEnvId, dir)` then `Vm.Load()`. Render category tabs from `Vm.Comparison.Categories` (mark `ViewOnly` tabs with a `view` affordance), the summary counts, and the diff table from `Vm.SelectedCategory.Rows` with the row-status accent. **No copy buttons yet** (Phase C). Use the markup/classes from `compare-sync.html`. Wire `@inject ProjectRegistry Projects`, `@inject EnvironmentService EnvService`, `RefreshService`. Keep `@implements IDisposable` + subscribe to `EnvService.Changed`/`Projects.Changed`/`Refresh.Requested` → `Vm.Load()` + `StateHasChanged`.
- [ ] **Step 3: Build** → `dotnet build src/ClaudeExplorer.App/ClaudeExplorer.App.csproj -v quiet -clp:NoSummary` → 0 errors. Reconcile any remaining references to the old `SetEnvironments`/`LeftEnv` in `Compare.razor`.
- [ ] **Step 4: Manual `/run`** (deferred — note in handoff): Compare lists Base·Windows / (Base·WSL) / added projects; Add project works; pick base↔project → diff renders incl. Memory; Plugins/Deps tabs show but are view-only.
- [ ] **Step 5: Commit** `feat(app): Compare screen — base/project endpoint pickers + Add project (read-only)`.

---

## PHASE C — Copy / Move

### Task C1: `SettingsKeyEditor` — set/remove a top-level key

**Files:** Create `src/ClaudeExplorer.Core/Sync/SettingsKeyEditor.cs`; Test `tests/ClaudeExplorer.Core.Tests/Sync/SettingsKeyEditorTests.cs`.

- [ ] **Step 1: Failing test**
```csharp
using ClaudeExplorer.Core.Mutation;
using ClaudeExplorer.Core.Sync;

namespace ClaudeExplorer.Core.Tests.Sync;

public class SettingsKeyEditorTests
{
    [Fact]
    public void SetKey_adds_into_empty_and_preserves_siblings()
    {
        var outp = SettingsKeyEditor.SetKey("""{ "model": "opus" }""", "env", """{ "A": "1" }""");
        Assert.Contains("\"model\": \"opus\"", outp);
        Assert.Contains("\"env\"", outp);
        Assert.Contains("\"A\": \"1\"", outp);
    }

    [Fact]
    public void SetKey_into_missing_file_creates_object()
        => Assert.Contains("\"model\"", SettingsKeyEditor.SetKey("", "model", "\"opus\""));

    [Fact]
    public void SetKey_overwrites_existing_key()
        => Assert.Contains("\"sonnet\"", SettingsKeyEditor.SetKey("""{ "model": "opus" }""", "model", "\"sonnet\""));

    [Fact]
    public void RemoveKey_drops_only_that_key()
    {
        var outp = SettingsKeyEditor.RemoveKey("""{ "model": "opus", "env": { "A": "1" } }""", "model");
        Assert.DoesNotContain("model", outp);
        Assert.Contains("\"env\"", outp);
    }

    [Fact]
    public void SetKey_rejects_invalid_value_json()
        => Assert.Throws<MutationException>(() => SettingsKeyEditor.SetKey("{}", "x", "{ not json"));
}
```
- [ ] **Step 2: Run → FAIL.** filter `SettingsKeyEditorTests`.
- [ ] **Step 3: Implement** — `src/ClaudeExplorer.Core/Sync/SettingsKeyEditor.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.Core.Sync;

/// <summary>Set or remove a single top-level key in a settings.json document, re-serialized pretty
/// (2-space). The top-level analogue of <c>HookBlockEditor</c>; refusals throw <see cref="MutationException"/>.</summary>
public static class SettingsKeyEditor
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true, IndentSize = 2, IndentCharacter = ' ' };
    private static readonly JsonDocumentOptions Lenient = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static string SetKey(string sourceText, string key, string valueJson)
    {
        JsonNode? value;
        try { value = JsonNode.Parse(valueJson, documentOptions: Lenient); }
        catch (JsonException ex) { throw new MutationException($"Value for \"{key}\" is not valid JSON: {ex.Message}"); }

        var root = ParseRoot(sourceText);
        root[key] = value;
        return root.ToJsonString(Pretty);
    }

    public static string RemoveKey(string sourceText, string key)
    {
        var root = ParseRoot(sourceText);
        root.Remove(key);
        return root.ToJsonString(Pretty);
    }

    public static string GetKey(string sourceText, string key)
        => ParseRoot(sourceText)[key]?.ToJsonString(Pretty)
           ?? throw new MutationException($"Key \"{key}\" not found.");

    private static JsonObject ParseRoot(string sourceText)
    {
        if (string.IsNullOrWhiteSpace(sourceText)) return new JsonObject();
        try { return JsonNode.Parse(sourceText, documentOptions: Lenient) as JsonObject
                     ?? throw new MutationException("Settings root is not a JSON object."); }
        catch (JsonException ex) { throw new MutationException($"Invalid settings JSON: {ex.Message}"); }
    }
}
```
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5: Commit** `feat(core): SettingsKeyEditor — set/remove a top-level settings key`.

---

### Task C2: `ConfigCopyService` — per-category copy operations

**Files:** Create `src/ClaudeExplorer.Core/Sync/ConfigCopyService.cs` (+ `CopyRequest`/`CopyPlan` records); Test `tests/ClaudeExplorer.Core.Tests/Sync/ConfigCopyServiceTests.cs`. Reuses `SafeMutationService`, `HookBlockEditor`, `SettingsKeyEditor`, `IFileSystem`/`IFileWriter`.

This task produces, for a `(category, key, sourceRoot, targetRoot, targetFile)`, the **new target content** (for JSON categories → goes through `SafeMutationService.PreviewEdit`/`ApplyEdit`) or a **file copy** (for memory/commands/skills). Move additionally removes from source.

- [ ] **Step 1: Failing test** (covers the two representative shapes — a settings key, and a memory file; plus move):
```csharp
using ClaudeExplorer.Core.Sync;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Sync;

public class ConfigCopyServiceTests
{
    [Fact]
    public void Copy_settings_key_writes_value_into_target_settings()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/base/.claude/settings.json", """{ "model": "opus" }""");
        var svc = new ConfigCopyService(fs);

        var plan = svc.PlanCopy(new CopyRequest(
            Category: "Settings", Key: "model",
            SourceSettingsPath: "/base/.claude/settings.json",
            TargetSettingsPath: "/proj/.claude/settings.json"));

        Assert.Equal("/proj/.claude/settings.json", plan.TargetPath);
        Assert.Contains("\"model\": \"opus\"", plan.NewTargetContent);
    }

    [Fact]
    public void Move_settings_key_also_removes_from_source()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/base/.claude/settings.json", """{ "model": "opus", "env": {} }""");
        var svc = new ConfigCopyService(fs);

        var plan = svc.PlanMove(new CopyRequest("Settings", "model",
            "/base/.claude/settings.json", "/proj/.claude/settings.json"));

        Assert.NotNull(plan.SourceRemoval);
        Assert.DoesNotContain("model", plan.SourceRemoval!.NewContent);
        Assert.Contains("\"env\"", plan.SourceRemoval.NewContent);
    }

    [Fact]
    public void Copy_memory_file_reads_source_content()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/base/.claude/CLAUDE.md", "# rules");
        var svc = new ConfigCopyService(fs);

        var plan = svc.PlanCopy(new CopyRequest("Memory", "CLAUDE.md",
            SourceFilePath: "/base/.claude/CLAUDE.md", TargetFilePath: "/proj/CLAUDE.md"));

        Assert.Equal("/proj/CLAUDE.md", plan.TargetPath);
        Assert.Equal("# rules", plan.NewTargetContent);
    }
}
```
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement** — `src/ClaudeExplorer.Core/Sync/ConfigCopyService.cs`. A `CopyRequest` carries the category + key + the resolved source/target paths (the App layer resolves paths from endpoints). `PlanCopy`/`PlanMove` return a `CopyPlan { TargetPath, NewTargetContent, bool TargetIsJson, SourceRemoval? }`. Dispatch by category: **Settings** → `SettingsKeyEditor.SetKey(targetText, key, SettingsKeyEditor.GetKey(sourceText, key))`; **Hooks** (key = `event#index`) → `HookBlockEditor` extract+append; **MCP** (key = server name) → copy the named object between MCP JSON files; **Memory/Commands/Skills/Subagents** → file copy (`NewTargetContent = sourceText`, `TargetIsJson=false`). `PlanMove` = `PlanCopy` + a `SourceRemoval { Path, NewContent }` (settings/hooks/mcp → key/group/server removed; files → `SourceRemoval` with a delete sentinel). Full method bodies follow the SettingsKeyEditor/HookBlockEditor patterns; read source/target via the injected `IFileSystem` (treat missing target as empty). Keep each category branch a small private method.
> The App layer applies a `CopyPlan` through `SafeMutationService` (JSON targets → `PreviewEdit(target, NewTargetContent, validation)` then `ApplyEdit`; file targets → backup+write via `Mutator`/`IFileWriter`) and, for move, applies `SourceRemoval` as a second edit — see C3.
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5: Commit** `feat(core): ConfigCopyService — per-category copy/move plans`.

---

### Task C3: `CopyViewModel` — apply a copy/move through safe-mutation

**Files:** Create `src/ClaudeExplorer.App/Compare/CopyViewModel.cs`; Test `tests/ClaudeExplorer.App.Tests/Compare/CopyViewModelTests.cs`.

- [ ] **Step 1: Failing test** — build a `SafeMutationService` over `InMemoryFileSystem` (as in `SafeEditViewModelTests`) + a `ConfigCopyService`; assert a settings-key copy writes the target file and records a change-log entry, undo reverts, move also removes from source. (Mirror the `HookEditViewModelTests` shape: `new(...)` with a `Func<string>` clock.)
```csharp
    [Fact]
    public void Copy_settings_key_writes_target_and_logs()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/base/.claude/settings.json", """{ "model": "opus" }""");
        var svc = new SafeMutationService(fs, fs, new FileBackupStore(fs, fs, "/bk"), new FakeProcessRunner());
        var vm = new CopyViewModel(svc, new ConfigCopyService(fs), () => "2026-06-08T00:00:00Z");

        vm.Copy(new CopyRequest("Settings", "model",
            "/base/.claude/settings.json", "/proj/.claude/settings.json"));

        Assert.Null(vm.Error);
        Assert.Contains("opus", fs.ReadAllText("/proj/.claude/settings.json"));
        Assert.Single(svc.ChangeLog.Entries);
    }
```
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement** — `CopyViewModel` wraps `ConfigCopyService.PlanCopy/PlanMove` + `SafeMutationService`: build the `CopyPlan`; for JSON targets call `_svc.PreviewEdit(new ResolvedTarget(scope, plan.TargetPath), plan.NewTargetContent, validation)` (validation = `new SettingsValidator().Validate(...)` for settings/mcp, `ValidationResult.Ok` for hooks/files) → `ApplyEdit(preview, now, desc)`; for `Move`, apply `plan.SourceRemoval` as a second `ApplyEdit`. Expose `Copy(req)`, `Move(req)`, `Applied`, `Error`, `Undo()`. Catch exceptions → `Error`. (Reuse `Mutator.PreviewEdit` exposed via `SafeMutationService` — add a thin `SafeMutationService.PreviewEdit(target, content, validation)` passthrough if not already public.)
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5: Commit** `feat(app): CopyViewModel — apply copy/move via safe-mutation`.

---

### Task C4: Compare copy/move UI

**Files:** Modify `src/ClaudeExplorer.App/Pages/Compare.razor` (+ a confirm modal); `wwwroot/css/blueprint.css` (copy-button styles from the mockup). No unit test — build + `/run`. **Use the `→`/`←` button + confirm markup from `ux-explorations/compare-sync.html`.**

- [ ] **Step 1:** Add per-row `→` (A→B) / `←` (B→A) buttons on each diff row, **hidden when `SelectedCategory.ViewOnly`** and disabled where N/A (`→` on an `OnlyB` row; `←` on an `OnlyA` row). Clicking opens a confirm: for a **Settings** target that is a project, a **shared vs local** target picker (maps to `…/.claude/settings.json` vs `…/.claude/settings.local.json`); a diff preview; **Copy / Move / Cancel** (Move shows the "affects every consumer" warning when the **source** endpoint is a Base). On confirm → resolve the source/target paths from `Vm.LeftEndpoint`/`RightEndpoint` + the row, build a `CopyRequest`, call `CopyVm.Copy`/`.Move`, then `Vm.Load()` to re-diff. Show applied + **Undo**.
- [ ] **Step 2: DI** — register `ConfigCopyService` (singleton over `IFileSystem`) and `CopyViewModel` (transient, with `SafeMutationService` + `ConfigCopyService` + `Func<string>`). Inject `CopyViewModel` into `Compare.razor`.
- [ ] **Step 3: Build** → 0 errors.
- [ ] **Step 4: Manual `/run`** (deferred): copy a setting base→project (creates project `settings.json`); copy a command file; move a setting (removes from source, warned); undo from the Change Log.
- [ ] **Step 5: Commit** `feat(app): Compare per-row copy/move (any direction) via safe-mutation`.

---

### Task C5: Full verification

- [ ] **Step 1:** `dotnet test ClaudeExplorer.slnx` → all green (incl. `CompareEndpointTests`, `ProjectRegistryTests`, `EnvironmentComparerTests`, `CompareViewModelTests`, `SettingsKeyEditorTests`, `ConfigCopyServiceTests`, `CopyViewModelTests`).
- [ ] **Step 2:** `dotnet build src/ClaudeExplorer.App/ClaudeExplorer.App.csproj -v quiet -clp:NoSummary` → 0 warnings/errors.
- [ ] **Step 3:** Update `docs/superpowers/HANDOFF.md` (Latest section): Compare/Sync (base+projects) shipped, new test count, tip commit.
- [ ] **Step 4: Commit** `docs: note Compare/Sync feature in HANDOFF`.

---

## Self-review

- **Spec coverage:** Nav reorg (A1); endpoint model (B1); project registry/persistence (B2); owned-config snapshot + Memory read (B3); Memory category + view-only (B4); endpoint-pair VM (B5); endpoint pickers + Add-project UI (B6); per-key settings edit (C1); per-category copy/move plans incl. hooks-reuse + MCP + file copy (C2); safe-mutation apply + undo (C3); copy/move UI + shared/local target + move warning (C4). Excludes (credentials/sessions/cache, plugin copy, effective toggle, bulk) honored. ✓
- **Type consistency:** `CompareEndpoint`(.ReadUserDir/.ReadProjectDir/.Base/.Project), `EndpointKind`, `EnvironmentSnapshot`(+`Memory`), `CompareCategory`(+`ViewOnly`), `ProjectRegistry`/`ProjectEndpointDef`(Id,Name,EnvId,ProjectDir), `CompareViewModel`(Endpoints/Left/RightEndpoint/SetEndpoints/Swap), `SettingsKeyEditor`(SetKey/RemoveKey/GetKey), `ConfigCopyService`(PlanCopy/PlanMove/`CopyRequest`/`CopyPlan`/`SourceRemoval`), `CopyViewModel`(Copy/Move/Undo). Names consistent across tasks. ✓
- **UI tasks** (A1, B6, C4) are build+manual and reference the committed mockups for exact markup/CSS (Photino render is headless-unverifiable — repo convention).

## Out of scope (this plan)
Effective-merged compare toggle; copying plugins/dependencies; comparing credentials/sessions/cache; bulk copy-all.
