# Phase 8 — Per-Screen UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`).

**Goal:** Build the remaining feature screens on the Phase-7 Blueprint shell — each a **tested
ViewModel** + a logic-light Blazor view that reuses the Phase-7 component library and consumes the
matching Core engine. Replaces the Phase-7 route stubs with real screens.

**Screens (each replaces its `Pages/*Stub.razor`):**
1. **Effective Config** — precedence matrix + provenance + read-only source preview + safe-edit flow.
2. **Commands & Skills** — source-grouped master/detail browser.
3. **Dependencies** — config-driven health list (Found/Missing/Unverifiable).
4. **Marketplace** — Browse / Installed / Add-source + install flow (via `claude` CLI).
5. **Recommendations** — project-fit (signals → why+evidence → confidence), the Marketplace "Recommended" tab.
6. **Change Log** — scope-grouped reversible change list with undo.

**Deferred (tracked, NOT in this phase):** multi-project **compare** — requires `IWorkspaceContext`
to carry multiple projects + side-by-side layout; deferred to keep v1 single-project quality high.
Create a Linear backlog issue for it (Task 10).

**Architecture (follow the Phase-7 pattern EXACTLY — it's the in-repo exemplar):**
- Each screen has a ViewModel (`ObservableObject`) with a `Load()` that pulls from a Core façade
  (resolved via DI + `IWorkspaceContext`) and exposes a view-ready model. Pure transformation logic
  goes in a `static *Computer` / mapper so it is unit-tested against Core records (like
  `DashboardComputer`). The engine-touching data source is a thin seam impl, not unit-tested.
- Views bind to the ViewModel, subscribe to `PropertyChanged` → `StateHasChanged`, call `Load()` in
  `OnInitialized`, subscribe to `RefreshService.Requested`, and `IDisposable`-unsubscribe — exactly
  like `Pages/Dashboard.razor`.
- Reuse `CornerTickPanel`, `Pill`, plus the new shared components from Task 1. Match the prototypes
  `ux-explorations/04`–`09` for markup/CSS (the Blueprint classes already exist in `blueprint.css`;
  add screen-specific CSS to `blueprint.css` as needed, ported from the relevant prototype).
- Tests live in `tests/ClaudeExplorer.App.Tests`. Construct Core records directly (no IO).

**Tech Stack:** .NET 10, Blazor, Photino.Blazor, MudBlazor, xUnit. Run `dotnet` via PowerShell.
Commit per task; `Co-Authored-By` trailer. Visual fidelity verified by human `/run` (Photino can't
run headless) — `dotnet build` + ViewModel tests are the automated gates.

**Core engine reference (verified signatures):**
- `EffectiveConfigService(IFileSystem).Compute(userDir, projectDir, enterprisePath?) → EffectiveConfig`
  (`Settings: IReadOnlyList<EffectiveSetting>`; each has `Key`, `Strategy: MergeStrategy`, `Value: JsonNode?`,
  `Winner: SettingOrigin?`, `Contributions: IReadOnlyList<SettingContribution>`, `HasConflict`).
  `SettingOrigin(ScopeKind Scope, string FilePath, string JsonPath)`;
  `SettingContribution(SettingOrigin Origin, JsonNode? Value)`.
- `ArtifactCatalogService(IFileSystem).Build(userDir, projectDir?, plugins?) → ArtifactCatalog`
  (`Artifacts: IReadOnlyList<ResolvedArtifact>`; `ResolvedArtifact(Winner: DiscoveredArtifact, Shadowed: …)`,
  `IsShadowing`; `DiscoveredArtifact(Kind: ArtifactKind, Name, Summary?, Source: ArtifactSource, FilePath)`;
  `ArtifactSource(Kind: ArtifactSourceKind, PluginName?)` with `.Label`).
- `DependencyHealthService(IFileSystem, IPathResolver, IProcessRunner).Check(userDir, projectDir, ent?) → DependencyReport`
  (`Results: IReadOnlyList<DependencyResult>`; `DependencyResult(Ref: DependencyRef, Status: DependencyStatus)`;
  `DependencyRef(Name, Raw, ReferencedBy)`; `DependencyStatus(Kind: DependencyStatusKind, Version?, Path?)`).
- `CatalogService(IFileSystem, ICatalogFetcher)`: `BuildInstalledCatalog(userDir) → IReadOnlyList<CatalogItem>`;
  `FetchAddedSource(input) → IReadOnlyList<CatalogItem>`. `CatalogItem(Name, Type: CatalogItemType, Summary?,
  Author?, Category?, Homepage?, Tags, Source: CatalogSource, Trust: TrustLevel, Stats?)`;
  `CatalogSource(Kind, Trust, Name, Location)`. Real fetcher: `HttpCatalogFetcher` (ICatalogFetcher).
- `RecommendationService(IFileSystem).Recommend(userDir, projectDir, catalog, runtimeAvailability?, itemRuntimes?) → RecommendationResult`
  (`Strong/Consider/AlreadyCovered` over `Recommendation(Item, Reasons: …, Confidence: double, Bucket, Runtimes)`;
  `RecommendationReason(Signal, Text)`; `Signal(Kind: SignalKind, Value, Evidence: IReadOnlyList<Evidence>)`;
  `Evidence(FilePath, Count?, Detail?)`).
- `SafeMutationService(IFileSystem, IFileWriter, IBackupStore, IProcessRunner)`: `ResolveTarget(EditMode, projectDir, winner) → ResolvedTarget`;
  `PreviewSettingsEdit(EditMode, projectDir, SettingOrigin? winner, newContent) → EditPreview`
  (`Target, OldContent, NewContent, Diff, Validation, TargetExisted`); `ApplyEdit(preview, timestamp, desc?) → ChangeLogEntry`;
  `Install(InstallRequest, timestamp) → ChangeLogEntry`; `Undo(ChangeLogEntry)`; `ChangeLog.Entries`, `ChangeLog.ByScope()`.

---

## Task 1: Shared view components

**Files:** Create under `src/ClaudeExplorer.App/Components/`: `TypeBadge.razor`, `TrustBadge.razor`,
`ScopeTag.razor`, `CodeViewer.razor`, `MatchBar.razor`. Append screen CSS to `wwwroot/css/blueprint.css`
(port `code`/dark-preview, `.badge`, `.scope-tag`, `.matchbar`, `.master`/`.detail`, table styles from
prototypes 04/05/07/09). Ensure `_Imports.razor` exposes `ClaudeExplorer.App.Components` (already does).

- [ ] `ScopeTag.razor` — a scope chip:
```razor
@using ClaudeExplorer.Core.Model
<span class="scope-tag s-@Scope.ToString().ToLowerInvariant()">@Scope.ToString().ToUpperInvariant()</span>
@code { [Parameter] public ScopeKind Scope { get; set; } }
```
- [ ] `TypeBadge.razor` — a small uppercase type label:
```razor
<span class="badge">@Text.ToUpperInvariant()</span>
@code { [Parameter] public string Text { get; set; } = ""; }
```
- [ ] `TrustBadge.razor` — verified/community:
```razor
@using ClaudeExplorer.Core.Catalog
<span class="badge @(Trust == TrustLevel.Verified ? "verified" : "community")">@(Trust == TrustLevel.Verified ? "VERIFIED" : "COMMUNITY")</span>
@code { [Parameter] public TrustLevel Trust { get; set; } }
```
- [ ] `CodeViewer.razor` — dark read-only preview with line numbers (port `.code`/dark styles from prototype 04):
```razor
<div class="codeview">
    <div class="codeview-head">@Title</div>
    <pre class="codeview-body"><code>@for (var i = 0; i < _lines.Length; i++)
{<span class="ln">@(i + 1)</span>@_lines[i]
}</code></pre>
</div>
@code {
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public string Content { get; set; } = "";
    private string[] _lines = Array.Empty<string>();
    protected override void OnParametersSet()
        => _lines = (Content ?? "").Replace("\r\n", "\n").Split('\n');
}
```
- [ ] `MatchBar.razor` — confidence bar (0..1 → %):
```razor
<div class="matchbar"><div class="matchbar-fill" style="width:@(Percent)%"></div><span class="matchbar-pct">@Percent%</span></div>
@code {
    [Parameter] public double Confidence { get; set; }
    private int Percent => (int)Math.Round(Math.Clamp(Confidence, 0, 1) * 100);
}
```
- [ ] `dotnet build` → clean. Commit: `feat(app): shared Blueprint view components (badges, scope tag, code viewer, match bar)`

---

## Task 2: Effective Config — matrix view-model + view

**Files:** Create `src/ClaudeExplorer.App/Screens/EffectiveConfig/EffectiveConfigViewModel.cs`,
`.../EffectiveConfigRows.cs` (view model + mapper), `Pages/EffectiveConfig.razor` (replace stub);
Test `tests/ClaudeExplorer.App.Tests/Screens/EffectiveConfigRowsTests.cs`.

**View-model records + mapper** (`EffectiveConfigRows.cs`):
```csharp
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.App.Screens.EffectiveConfig;

public sealed record ScopeCell(bool Present, string? Display, bool IsWinner, bool IsOverridden);
public sealed record SettingRow(
    string Key, MergeStrategy Strategy, string MergeLabel, bool HasConflict, string EffectiveDisplay,
    IReadOnlyDictionary<ScopeKind, ScopeCell> Cells, IReadOnlyList<SettingContribution> Trace, SettingOrigin? Winner);
public sealed record EffectiveConfigView(IReadOnlyList<SettingRow> Rows, int ConflictCount);

public static class EffectiveConfigMapper
{
    private static readonly ScopeKind[] AllScopes = { ScopeKind.Enterprise, ScopeKind.User, ScopeKind.Project, ScopeKind.Local };

    public static EffectiveConfigView Map(EffectiveConfig config)
    {
        var rows = config.Settings.Select(s =>
        {
            var byScope = s.Contributions
                .GroupBy(c => c.Origin.Scope)
                .ToDictionary(g => g.Key, g => g.Last());
            var cells = new Dictionary<ScopeKind, ScopeCell>();
            foreach (var scope in AllScopes)
            {
                if (byScope.TryGetValue(scope, out var contrib))
                {
                    var isWinner = s.Winner is not null && s.Winner.Scope == scope;
                    cells[scope] = new ScopeCell(true, Display(contrib.Value), isWinner, !isWinner && s.Strategy == MergeStrategy.ScalarLastWins);
                }
                else cells[scope] = new ScopeCell(false, null, false, false);
            }
            return new SettingRow(s.Key, s.Strategy, MergeLabel(s.Strategy), s.HasConflict,
                Display(s.Value), cells, s.Contributions, s.Winner);
        }).ToList();
        return new EffectiveConfigView(rows, rows.Count(r => r.HasConflict));
    }

    private static string MergeLabel(MergeStrategy s) => s switch
    {
        MergeStrategy.ListUnion => "merged · union",
        MergeStrategy.ArrayConcat => "merged · concat",
        _ => "scalar · last-wins",
    };

    private static string Display(System.Text.Json.Nodes.JsonNode? node)
        => node is null ? "" : node.ToJsonString();
}
```

**ViewModel** (`EffectiveConfigViewModel.cs`) — loads `EffectiveConfigService.Compute(workspace.UserDir,
workspace.ProjectDir)`, maps via `EffectiveConfigMapper`, exposes `EffectiveConfigView? View`,
`bool IsLoading`, `string? ErrorMessage` (mirror `DashboardViewModel`). Holds the `SafeMutationService`
+ `IWorkspaceContext` for the edit flow (Task 3). `Load()` try/catch like Dashboard.

- [ ] **Test** `EffectiveConfigRowsTests.cs`: build an `EffectiveConfig` with a scalar conflict (User
  `model=opus` wins over Project `model=sonnet`) and a list setting; assert: winner cell `IsWinner`,
  project cell `IsOverridden`, `HasConflict` true, `ConflictCount==1`, `MergeLabel` correct, missing
  scopes `Present==false`, `EffectiveDisplay` is the winner value. (Construct `EffectiveSetting`/
  `SettingContribution`/`SettingOrigin` directly.)
- [ ] **View** `Pages/EffectiveConfig.razor` (`@page "/effective"`): port prototype `04`’s matrix —
  a table with header row `SETTING | ENTERPRISE | USER | PROJECT | LOCAL | → EFFECTIVE`; one row per
  `SettingRow`; winner cell highlighted, overridden cells struck-through, conflict shows a red
  `CONFLICT` `Pill`, merge label under the key; an expand toggle reveals the provenance `Trace`
  (each contribution: `ScopeTag` + value + `FilePath:JsonPath`) and a `CodeViewer` preview of the
  winning file (read the winner file via an injected service or show the winning value). Add an
  **Edit** button per row that opens the safe-edit panel (Task 3). Bind to `EffectiveConfigViewModel`.
- [ ] `dotnet build` + `dotnet test` green. Commit: `feat(app): effective config precedence matrix + provenance`

---

## Task 3: Effective Config — safe-edit flow

**Files:** Create `src/ClaudeExplorer.App/Screens/EffectiveConfig/SafeEditViewModel.cs`;
`src/ClaudeExplorer.App/Components/SafeEditPanel.razor`; wire into `EffectiveConfig.razor`. Test
`tests/ClaudeExplorer.App.Tests/Screens/SafeEditViewModelTests.cs`.

**`SafeEditViewModel`** drives the 3-step flow (prototype `06`): compose (pick `EditMode` —
EditWinner/OverrideAtProject/OverrideAtLocal — + edit the value/JSON) → preview (`SafeMutationService.
PreviewSettingsEdit` → show `Diff` + `Validation`) → apply (`ApplyEdit(preview, timestamp)`), with
**Undo** of the returned `ChangeLogEntry`. Exposes `EditMode Mode`, `string NewContent`, `EditPreview?
Preview`, `ChangeLogEntry? Applied`, `string? Error`. Inject a clock seam `Func<string> nowIso` (DI a
real one; tests pass a fixed stamp) so the timestamp is deterministic.

- [ ] **Test** `SafeEditViewModelTests.cs`: with an in-memory `SafeMutationService` (build it from a
  test `InMemoryFileSystem` + `FileBackupStore` + a fake/real `IProcessRunner` — reuse Core test
  fakes by adding minimal local fakes in `tests/.../Fakes/`): given a winner `SettingOrigin` and new
  content, `Preview()` populates `Preview` with a non-empty `Diff` and valid `Validation`; `Apply()`
  writes the file and returns an entry; `Undo()` reverts. Also: invalid JSON → `Preview.Validation`
  invalid and `Apply()` refused (sets `Error`).
  > NOTE: `tests/ClaudeExplorer.App.Tests` cannot see `ClaudeExplorer.Core.Tests`' internal fakes —
  > add a small `InMemoryFileSystem` (implementing `IFileSystem` + `IFileWriter`) and a
  > `FakeProcessRunner` to `tests/ClaudeExplorer.App.Tests/Fakes/` (copy the shape from Core tests).
- [ ] **`SafeEditPanel.razor`**: the 3-step filmstrip — scope-target radio (EditWinner / Override
  Project / Override Local) with the global-edit warning callout; a textarea bound to `NewContent`;
  Preview button → diff render (reuse `DiffKind` colors) + validation errors; Apply button (disabled
  if invalid) → applied banner + Undo. Port `06`’s structure. Wire it into the matrix row Edit action.
- [ ] `dotnet build` + `dotnet test` green. Commit: `feat(app): safe-edit flow (scope target → diff → apply → undo)`

---

## Task 4: Commands & Skills browser

**Files:** `src/ClaudeExplorer.App/Screens/Artifacts/ArtifactBrowserViewModel.cs`,
`.../ArtifactBrowser.cs` (view model + grouping mapper); `Pages/CommandsSkills.razor` (`@page "/commands"`,
replace stub); Test `tests/ClaudeExplorer.App.Tests/Screens/ArtifactBrowserTests.cs`.

- [ ] **Mapper/view model**: group `ArtifactCatalog.Artifacts` by `Winner.Source` (User/Project/Plugin),
  expose groups with items carrying `Kind`, `Name`, `Summary`, `IsShadowing`, `Winner.FilePath`, and
  the shadowed list. ViewModel exposes the groups + a selected item (master/detail) + a kind filter
  (`ArtifactKind?`) + a search string; expose a filtered view. **Test**: grouping by source, filter by
  kind, search by name substring (ordinal), shadowed flag surfaced.
- [ ] **View** `Pages/CommandsSkills.razor`: port `05` — left master list grouped by source with
  `TypeBadge` (kind) + shadow indicator; right detail with summary + `CodeViewer` of the winner file
  (inject a small file-reader over `IFileSystem`) + action buttons (Copy path; Open/Reveal are no-ops
  or shell-out stubs — keep them present but non-failing). Search box + kind filter chips.
- [ ] `dotnet build` + `dotnet test` green. Commit: `feat(app): commands & skills source-grouped browser`

---

## Task 5: Dependencies screen

**Files:** `src/ClaudeExplorer.App/Screens/Dependencies/DependencyViewModel.cs`,
`.../DependencyRows.cs`; `Pages/Dependencies.razor` (`@page "/dependencies"`, replace stub); Test
`tests/ClaudeExplorer.App.Tests/Screens/DependencyRowsTests.cs`.

- [ ] **Mapper/view model**: map `DependencyReport.Results` → rows (`Name`, status tone
  ok/warn/bad from Found/Unverifiable/Missing, `Version`, `Path`, `ReferencedBy` joined), plus counts
  (found/missing/unverifiable). **Test**: tone mapping per kind; counts; referenced-by formatting.
- [ ] **View** `Pages/Dependencies.razor`: a `CornerTickPanel` summary (counts as `Pill`s) + a list of
  rows with a status `Pill`, version/path in mono, and `ReferencedBy`. Bind to `DependencyViewModel`.
- [ ] `dotnet build` + `dotnet test` green. Commit: `feat(app): dependency health screen`

---

## Task 6: Change Log screen

**Files:** `src/ClaudeExplorer.App/Screens/ChangeLog/ChangeLogViewModel.cs`; `Pages/ChangeLogPage.razor`
(`@page "/changelog"`, replace stub); Test `tests/ClaudeExplorer.App.Tests/Screens/ChangeLogViewModelTests.cs`.

- [ ] **ViewModel**: reads the shared `SafeMutationService.ChangeLog` (singleton via DI), exposes
  `ByScope()` groups + each entry (`Kind`, `Description`, `Timestamp`, `Scope`, `IsUndone`), and an
  `Undo(entry)` command that calls `SafeMutationService.Undo` then re-reads. **Test**: with a
  `SafeMutationService` over an in-memory FS, apply an edit then assert the VM lists it grouped by
  scope; `Undo` marks it undone. (Reuse the Task-3 local fakes.)
- [ ] **View** `Pages/ChangeLogPage.razor`: scope-grouped list (port the dashboard `Recent Changes`
  row style); each entry shows `ScopeTag` + description + timestamp + an **Undo** button (disabled if
  `IsUndone`).
- [ ] `dotnet build` + `dotnet test` green. Commit: `feat(app): scope-aware change log screen with undo`

---

## Task 7: Marketplace (browse / installed / add-source / install)

**Files:** `src/ClaudeExplorer.App/Screens/Marketplace/MarketplaceViewModel.cs`,
`.../MarketplaceModels.cs`; `Pages/Marketplace.razor` (`@page "/marketplace"`, replace stub); Test
`tests/ClaudeExplorer.App.Tests/Screens/MarketplaceViewModelTests.cs`.

- [ ] **ViewModel**: tabs Installed / Add-source. `LoadInstalled()` → `CatalogService.BuildInstalledCatalog(userDir)`.
  `AddSource(input)` → `CatalogService.FetchAddedSource(input)` (metadata-only; community trust),
  exposing the fetched items with a trust warning. `Install(item)` → builds an `InstallRequest`
  (`InstallArgs = ["plugin","install",item.Name]`, `UninstallArgs = ["plugin","uninstall",item.Name]`,
  scope from a picker) and calls `SafeMutationService.Install(request, now)`. Expose `Items`,
  `AddedItems`, `IsLoading`, `Error`, `LastInstall: ChangeLogEntry?`. Inject `CatalogService` (uses a
  **fake `ICatalogFetcher`** in tests), `SafeMutationService`, `IWorkspaceContext`, `Func<string> nowIso`.
  **Test**: `LoadInstalled` over an in-memory FS with a fixture marketplace; `AddSource` via a fake
  fetcher returns normalized community items; `Install` runs the `claude` CLI through a fake
  `IProcessRunner` and records a change-log entry; failed install (non-zero exit) sets `Error`.
- [ ] **View** `Pages/Marketplace.razor`: port `07`/`08` — tabs; item cards with `TypeBadge` +
  `TrustBadge`, summary, author, an Install button (+ scope picker); an Add-source input that detects
  type, shows a **community trust warning**, lists fetched items metadata-only with Install. Show the
  applied install + an Undo (uninstall) using `LastInstall`.
- [ ] `dotnet build` + `dotnet test` green. Commit: `feat(app): marketplace browse + add-source + install`

---

## Task 8: Recommendations (project fit)

**Files:** `src/ClaudeExplorer.App/Screens/Recommendations/RecommendationsViewModel.cs`;
`Pages/Recommendations.razor` (`@page "/recommended"` — also add a rail link, OR surface as the
Marketplace "Recommended" tab; simplest: a dedicated page + rail entry); Test
`tests/ClaudeExplorer.App.Tests/Screens/RecommendationsViewModelTests.cs`.

- [ ] **ViewModel**: `Load()` → catalog = `CatalogService.BuildInstalledCatalog(userDir)` (or added
  sources), then `RecommendationService.Recommend(userDir, projectDir, catalog)`; expose detected
  signals (from the result's reasons) and the three buckets (`Strong`/`Consider`/`AlreadyCovered`),
  each `Recommendation` with `Item`, `Confidence`, `Reasons` (each reason → text + `Evidence` chips),
  `Runtimes`. **Test**: with a constructed `RecommendationResult` (build via a fake source or by
  calling the service over fixtures), assert bucket partitioning, reasons/evidence surfaced,
  confidence passthrough. (Prefer testing a small mapper from `RecommendationResult` → view rows so
  no IO is needed.)
- [ ] **View** `Pages/Recommendations.razor`: port `09` — "Analyzed <project> — N signals" with signal
  chips; sections Strong / Worth considering / Already covered; each card: name + `TypeBadge`/
  `TrustBadge`, WHY text, **evidence chips** (each links to `Evidence.FilePath`), `MatchBar`
  (confidence), runtime annotations ("needs uvx — missing"), Install button (reuse Task-7 install).
- [ ] `dotnet build` + `dotnet test` green. Commit: `feat(app): project-fit recommendations screen`

---

## Task 9: DI + navigation wiring

**Files:** Modify `src/ClaudeExplorer.App/Program.cs`, `src/ClaudeExplorer.App/Components/LeftRail.razor`,
`_Imports.razor`. Delete the replaced stub pages.

- [ ] In `Program.cs` register: `ICatalogFetcher → HttpCatalogFetcher` (singleton), `CatalogService`
  (singleton, from `IFileSystem` + `ICatalogFetcher`), `RecommendationService` (singleton, from
  `IFileSystem`), a clock `Func<string>` returning ISO-8601 now (e.g. `() => DateTime.UtcNow.ToString("o")`),
  and all screen ViewModels (transient): `EffectiveConfigViewModel`, `SafeEditViewModel`,
  `ArtifactBrowserViewModel`, `DependencyViewModel`, `ChangeLogViewModel`, `MarketplaceViewModel`,
  `RecommendationsViewModel`. (`SafeMutationService` is already a singleton from Phase 7 — its
  `ChangeLog` is shared, so the Change Log screen sees edits/installs made elsewhere.)
- [ ] Update `LeftRail.razor`: point nav at the real routes; add a Recommendations entry (e.g. under
  Discover). Remove dead `/mcp` stub if MCP screen is out of scope, OR keep it as a stub (MCP & Plugins
  full screen is not in this phase — leave its stub and note it). Delete the now-replaced stub `.razor`
  files (`EffectiveConfigStub`, `commands`, `dependencies`, `marketplace`, `changelog`).
- [ ] Add any missing `@using` to `_Imports.razor` (`ClaudeExplorer.App.Screens.*`,
  `ClaudeExplorer.Core.Catalog`, `ClaudeExplorer.Core.Recommendations`).
- [ ] `dotnet build` + `dotnet test` green. Commit: `feat(app): wire screens into DI + navigation`

---

## Task 10: Docs + deferral tracking

**Files:** Modify `docs/superpowers/plans/2026-06-07-00-roadmap.md`, `docs/superpowers/HANDOFF.md`.

- [ ] Mark Phase 8 done (commit range post-merge); update test count; set HANDOFF state to "all phases
  complete" and record the per-screen architecture.
- [ ] Note the deferrals explicitly: **multi-project compare** and the **MCP & Plugins** full screen
  (only its stub exists) are deferred — a Linear backlog issue is created for each (or added to CLA-16).
- [ ] Commit: `docs: mark Phase 8 (per-screen UI) done; note deferrals`

---

## Self-Review

**Spec coverage:** Effective Config matrix+provenance+preview ✅ T2; safe-edit (scope target/diff/
validate/backup/undo) ✅ T3; Commands & Skills source-grouped browser ✅ T4; Dependencies ✅ T5;
Change Log ✅ T6; Marketplace browse/add/install + trust ✅ T7; Recommendations why+evidence+confidence
✅ T8; nav/DI ✅ T9. Deferred (tracked): multi-project compare; full MCP & Plugins screen.

**Pattern fidelity:** every screen mirrors the Phase-7 `Dashboard` VM/view pattern (Load + PropertyChanged
+ RefreshService + IDisposable; pure mapper tested over Core records; engine touch only in DI-wired
services). New shared components are logic-light.

**Type consistency:** view-model records and mapper method names are self-contained per screen;
Core façade signatures match those verified above. Timestamp via injected `Func<string>` for
deterministic tests. Install args `["plugin","install",name]` / uninstall `["plugin","uninstall",name]`
match `SafeMutationService.Install`/`Undo`.

**Test isolation:** mappers/VMs tested by constructing Core records or via in-memory FS + local fakes
(added to App.Tests/Fakes); no real network/process. `HttpCatalogFetcher`, engine data sources, and
env workspace are DI-only (not unit-tested), per the seam convention.
