# Artifact Split + Real MCP/Plugins Screens — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development or
> executing-plans. Steps use `- [ ]`. Spec: `docs/superpowers/specs/2026-06-08-artifact-split-mcp-plugins-design.md`.
> UI source of truth: `ux-explorations/11-blueprint-artifact-split.html` (+ `shot-11-*.png`).

**Goal:** Split `Commands & Skills` and `MCP & Plugins` into five separate, bespoke screens —
Commands, Skills, Subagents, MCP, Plugins — with MCP and Plugins backed by real data.

**Architecture:** Each screen = pure tested mapper (Core records → view records) + `ObservableObject`
VM + Blueprint `.razor`, following `IWorkspaceContext`. Two new Core readers (MCP inventory, plugin
inventory); `DiscoveredArtifact` enriched with frontmatter. Read-only this phase.

**Tech Stack:** .NET 10, C#, xUnit; Photino.Blazor + MudBlazor; `InMemoryFileSystem` fixtures.

---

### Task 1: Enrich `DiscoveredArtifact` with frontmatter + extra-file count (Core)

**Files:** Modify `src/ClaudeExplorer.Core/Artifacts/ArtifactModel.cs`,
`src/ClaudeExplorer.Core/Artifacts/ArtifactDiscoverer.cs`;
Test `tests/ClaudeExplorer.Core.Tests/Artifacts/ArtifactDiscoverer{Subagent,Skill,Command}Tests.cs`.

- [ ] Add to `DiscoveredArtifact` (positional defaults keep all existing call sites compiling):
  `IReadOnlyDictionary<string,string>? Frontmatter = null, int ExtraFileCount = 0`. Add a helper
  `Fm => Frontmatter ?? EmptyDict` so consumers never null-check.
- [ ] In `ArtifactDiscoverer`, pass `fm.Fields` into each `new DiscoveredArtifact(...)`. For skills,
  set `ExtraFileCount` = `_fs.GetFiles(skillDir, "*", recurse:true).Count(f => !f.EndsWith("/SKILL.md"))`.
- [ ] Tests: subagent carries `tools`/`model` frontmatter; command carries `argument-hint`; skill with
  a sibling `reference.md` reports `ExtraFileCount == 1`. Run: `dotnet test`. Commit.

### Task 2: MCP inventory model + reader (Core)

**Files:** Create `src/ClaudeExplorer.Core/Mcp/McpInventory.cs`,
`src/ClaudeExplorer.Core/Mcp/McpInventoryReader.cs`; Test
`tests/ClaudeExplorer.Core.Tests/Mcp/McpInventoryReaderTests.cs`.

- [ ] `enum McpTransport { Stdio, Http, Sse }`. Record
  `McpServerInfo(string Name, McpTransport Transport, string? Command, IReadOnlyList<string> Args,
  string? Url, IReadOnlyDictionary<string,string> Env, string SourceLabel, string SourceFile)`.
- [ ] `McpInventoryReader.Read(userDir, projectDir)` collects servers from, in order:
  located settings files' `mcpServers` (wrapper; `SourceLabel` = scope), project `.mcp.json`
  (wrapper), `{userDir}/.claude.json` `mcpServers` (wrapper), and each installed plugin's
  `.mcp.json` (**name-at-root**, `SourceLabel = "plugin: <name>"`) via `InstalledPluginLocator`.
  Transport = from `type` field (`http`/`sse`/`stdio`) else `Url != null ? Http : Stdio`. Dedupe by
  `(Name, SourceFile)`. Malformed/missing skipped.
- [ ] Tests: wrapper shape and name-at-root shape both parse; `linear` http with url; `playwright`
  stdio with command+args; env captured; plugin source label. Run tests. Commit.

### Task 3: MCP screen — health join + VM + view (App)

**Files:** Create `src/ClaudeExplorer.App/Screens/Mcp/McpRows.cs` (mapper),
`McpViewModel.cs`, `src/ClaudeExplorer.App/Pages/Mcp.razor`; Test
`tests/ClaudeExplorer.App.Tests/Screens/McpRowsTests.cs`, `McpViewModelTests.cs`.

- [ ] `McpRowsMapper.Map(IReadOnlyList<McpServerInfo>, DependencyReport)` → `McpRow(Name, Transport,
  Endpoint, SourceLabel, HealthPill {Ok|Missing|Na}, …)`. Stdio → pill from the dep report entry for
  its command's runtime; Http/Sse → `Na`. `Endpoint` = url, or `command + " " + args`.
- [ ] `McpViewModel` (DI: `McpInventoryReader`, `DependencyHealthService`, `IWorkspaceContext`): `Load()`
  reads servers + `Check()` and maps; exposes `Rows`, `Selected`, `IsLoading`, `ErrorMessage`.
- [ ] `Mcp.razor` per mockup (rows + detail panel). Wire `RefreshService`.
- [ ] Tests: stdio-missing → Missing pill; http → Na; VM load populates rows. Run tests. Commit.

### Task 4: Plugin inventory model + reader (Core)

**Files:** Create `src/ClaudeExplorer.Core/Plugins/PluginInventory.cs`,
`PluginInventoryReader.cs`; Test `tests/ClaudeExplorer.Core.Tests/Plugins/PluginInventoryReaderTests.cs`.

- [ ] Records: `ProvidesCounts(int Commands, int Skills, int Subagents, int Hooks, int Mcp)`;
  `InstalledPluginInfo(string Name, string Marketplace, string Version, string Scope, string InstallPath,
  bool Enabled, ProvidesCounts Provides, TrustLevel Trust)`;
  `MarketplaceInfo(string Name, string? SourceRepo, TrustLevel Trust, int InstalledCount)`;
  `PluginInventory(IReadOnlyList<InstalledPluginInfo> Plugins, IReadOnlyList<MarketplaceInfo> Marketplaces)`.
  (`TrustLevel` already exists in `Catalog/`.)
- [ ] `PluginInventoryReader.Read(userDir)`:
  parse `{userDir}/.claude/plugins/installed_plugins.json` (`plugins` map, key `name@marketplace`,
  first entry → version/scope/installPath); parse `known_marketplaces.json` (source repo) and classify
  trust via `MarketplaceTrust`/`InstalledMarketplaceReader`; per plugin compute `ProvidesCounts` by
  scanning its `installPath` (reuse `ArtifactDiscoverer` for cmd/skill/agent counts + `hooks/hooks.json`
  + `.mcp.json` presence); `Enabled` from user `settings.json` `enabledPlugins`. Malformed → skipped.
- [ ] Tests: two marketplaces with correct trust; a plugin's provides counts; enabled flag from
  settings; community vs verified. Run tests. Commit.

### Task 5: Plugins screen — VM + view (App)

**Files:** Create `src/ClaudeExplorer.App/Screens/Plugins/PluginsViewModel.cs`,
`src/ClaudeExplorer.App/Pages/Plugins.razor`; Test
`tests/ClaudeExplorer.App.Tests/Screens/PluginsViewModelTests.cs`.

- [ ] `PluginsViewModel` (DI: `PluginInventoryReader`, `IWorkspaceContext`): `Load()` → `Marketplaces`,
  `Plugins` (+ `IsLoading`/`ErrorMessage`). Optional pure helper for the "provides" label string.
- [ ] `Plugins.razor` per mockup (marketplaces strip + plugin cards). Wire `RefreshService`.
- [ ] Tests: VM load groups marketplaces + plugins; provides-label formatting. Run tests. Commit.

### Task 6: Commands / Skills / Subagents bespoke screens (App)

**Files:** Create under `src/ClaudeExplorer.App/Screens/Artifacts/`:
`CommandRows.cs`+`CommandsViewModel.cs`, `SkillRows.cs`+`SkillsViewModel.cs`,
`SubagentRows.cs`+`SubagentsViewModel.cs`; Pages `Commands.razor`, `Skills.razor`, `Subagents.razor`;
Tests under `tests/ClaudeExplorer.App.Tests/Screens/`.

- [ ] Each VM builds on `ArtifactCatalogService.Build(userDir, projectDir)` (already plugin-aware),
  filters to its `ArtifactKind`, and a pure mapper projects bespoke detail fields from
  `Winner.Fm`: Commands → `argument-hint` + invocation `/name`; Skills → invocable badge
  (frontmatter has a slash trigger) + `ExtraFileCount`; Subagents → `tools` split into chips + `model`.
- [ ] `.razor` per the five mockup views (master/detail for all three; subagents emphasize tool chips).
  Reuse existing `ScopeTag`/badges/`CodeViewer`. Wire `RefreshService`.
- [ ] Tests: each mapper surfaces its type-specific fields; source grouping + shadow flag preserved.
  Run tests. Commit.

### Task 7: Left rail, routing, Shell counts, DI, remove stubs (App)

**Files:** Modify `src/ClaudeExplorer.App/Components/LeftRail.razor`,
`src/ClaudeExplorer.App/ViewModels/ShellViewModel.cs`, `src/ClaudeExplorer.App/Program.cs`;
Delete `src/ClaudeExplorer.App/Pages/CommandsSkills.razor`, `Pages/McpStub.razor`;
Test `tests/ClaudeExplorer.App.Tests/ViewModels/ShellViewModelTests.cs`.

- [ ] `LeftRail.razor`: regroup into Workspace / Config Artifacts / Extensions / Analyze / Discover
  with the five new `NavLink`s (`/commands`,`/skills`,`/subagents`,`/mcp`,`/plugins`); remove the old
  two items + their routes; keep count/problem badges.
- [ ] `ShellViewModel`: replace `CommandsAndSkills` with `Commands`, `Skills`, `Subagents`, `Mcp`,
  `Plugins` int counts (compute from `ArtifactCatalogService` per-kind + the new readers, or extend
  `DashboardComputer`). Keep `HasMcpProblem`/`HasDependencyProblem`. Update tests.
- [ ] `Program.cs`: register `McpInventoryReader`, `PluginInventoryReader`, and the five VMs (transient).
- [ ] Delete `CommandsSkills.razor` + `McpStub.razor`. Run full suite (`dotnet test`). Commit.

### Final

- [ ] Full clean build + `dotnet test` green. Dispatch `feature-dev:code-reviewer` over the branch diff;
  fix high-confidence findings. ff-merge to `main`, push, update Linear + build-status memory.

---

## Self-review notes
- Spec coverage: 5 views (T3,5,6) + 2 readers (T2,4) + artifact enrichment (T1) + IA/Shell/DI (T7) — all covered.
- Type consistency: `McpServerInfo`, `InstalledPluginInfo`, `ProvidesCounts`, `DiscoveredArtifact.Fm`
  used consistently across tasks.
- Read-only: no task wires mutation/install — matches spec scope.
