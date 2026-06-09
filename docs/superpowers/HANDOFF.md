# Claude Explorer — Handoff / Continuation Guide

Read this first when resuming with no prior conversation context. It captures where the
build is, how it's being built, and how to pick up the next phase.

## What this project is
A cross-platform desktop app (.NET 10 · Photino + Blazor + MudBlazor, MVVM) to **discover,
safely edit, install, and recommend** the settings & tooling that affect Claude Code.
Full spec: `CLAUDE.md`. UI direction **Blueprint**: prototypes + screenshots in
`ux-explorations/` (`03-blueprint.html` is the chosen look; `04`–`09` are the per-screen
prototypes). Phase decomposition + per-phase scope: `docs/superpowers/plans/2026-06-07-00-roadmap.md`.

## Latest (2026-06-08)
- **Per-screen Compare/Sync shipped on `feat-per-screen-compare-sync`** (tip `750ff1f`, 443 tests):
  Compare/Sync moved out of the central `/compare` page into every artifact screen as a reusable
  overlay. Key changes:
  - **`CompareBar` + `DiffOverlay` components** (A/B picker + per-row diff chips + Copy/Move/Undo)
    wired on Commands, Skills, Subagents, Hooks, MCP, EffectiveConfig, Plugins, Dependencies, Memory.
  - **`CompareContext` singleton** — persistent A/B endpoint selection across navigation.
  - **Enriched diff rows** — `CompareRow.PathA/PathB/ContentA/ContentB` carry resolved on-disk paths
    so Commands/Subagents/Skills/Memory copy/move now works.
  - **`CopyRequestBuilder`** — pure per-category path builder (Settings/Memory/MCP/Hooks → settings
    JSON; Commands/Subagents → file; Skills → dir).
  - **Recursive Skills directory copy/move** — `CopyPlan` extended with `Writes`/`Removals` lists;
    `CopyViewModel.Undo()` reverses the whole group in reverse order.
  - **Undo-able delete** — `ChangeKind.Delete` + `Mutator.ApplyDelete` (backup → delete → record;
    Undo re-creates original); exposed via `SafeMutationService.ApplyDelete`.
  - **Hooks compare category** — `EnvironmentComparer` now produces `hooks.<event>#<index>` rows;
    "Agents" renamed to "Subagents" throughout.
  - **Memory screen** — new `/memory` page (`MemoryRowsMapper.Discover` → global/project/local/nested
    CLAUDE.md; `MemoryViewModel`); left-rail entry added under Config Artifacts.
  - **Central Compare page retired** — `Compare.razor`, `CompareViewModel`, `CompareViewModelTests`
    deleted; left-rail Analyze block removed.
  - **"＋ Add project endpoint…"** action moved into the environment selector dropdown.
  - Spec/plan: `docs/superpowers/specs/2026-06-08-per-screen-compare-design.md`,
    `docs/superpowers/plans/2026-06-08-14-per-screen-compare-sync.md`.
- **Compare / Sync (base + projects) shipped to `main`** (tip `0df2a1c`): the env-vs-env Compare
  screen is generalized so endpoints are **bases** (environments' `~/.claude`) **+ projects** (added
  folders, persisted via `ProjectRegistry`). Compares each endpoint's **owned** config across categories
  incl. a new **Memory** (CLAUDE.md) category (Plugins/Deps are view-only), and supports per-row
  **copy/move in any direction** through safe-mutation — for Settings, Memory, and MCP rows
  (commands/skills/subagents copy deferred: the diff row lacks the artifact path; file-move-delete also
  deferred). New: `Compare/CompareEndpoint`, `Environments/ProjectRegistry`, `Compare/CopyViewModel`;
  Core `Sync/SettingsKeyEditor` + `Sync/ConfigCopyService`. Left-rail reorg: Hooks/MCP/Plugins folded
  into **Config Artifacts**. Spec/plan: `docs/superpowers/specs/2026-06-08-compare-sync-base-projects-design.md`,
  `docs/superpowers/plans/2026-06-08-13-compare-sync-base-projects.md`.
- **Hooks inline editor shipped to `main`** (tip `41287c5`): Hooks rows redesigned (matcher → fully
  visible tool chips, scope/health top-right, command on its own line); clicking a row opens an inline
  accordion with the hook's matcher-group as **editable, pretty-printed JSON** (spliced back into the
  source `settings.json` via the existing safe-mutation flow — diff/backup/validate/change-log/undo)
  plus the **referenced script file rendered read-only with syntax highlighting**. New Core helpers
  `HookBlockEditor` + `HookScriptResolver` (`src/ClaudeExplorer.Core/Hooks/`); App `HookEditViewModel`
  + `HookMatcher`; `CodeViewer` upgraded with `Language`/`Capped` params backed by **bundled
  highlight.js** (`wwwroot/lib/highlight/`, read-only views only). Plugin/enterprise hooks are
  read-only. Spec/plan: `docs/superpowers/specs/2026-06-08-hooks-inline-editor-design.md`,
  `docs/superpowers/plans/2026-06-08-12-hooks-inline-editor.md`.
- Also note: a `.grp-label` section-header restyle (blue tick + dark name + count chip) shipped earlier
  the same day (`089dc2e`).
- **`dotnet test` → 443 passing** (253 Core + 190 App). `.NET SDK 10.0.300`+.
- **The phase-by-phase prose below is from 2026-06-07 and is behind `main`** — Phases 10–11 (env
  settings sync, artifact-split/real MCP+Plugins screens) and the work above are not reflected in it.

## Current state (2026-06-07)
- **Phases 1–9 are merged and pushed to `main`** (`git log` tip ≈ `d74a58a`). **Phase 10 (env settings
  sync, epic CLA-97) is DONE** — delivered via per-screen compare/sync (plans 13+14); CLA-97 closed 2026-06-08.
- **`dotnet test` → 309 passing** (205 Core + 104 App) *(superseded — see Latest above)*. `.NET SDK 10.0.300` is installed.
  Solution file is `ClaudeExplorer.slnx` (new .NET 10 format — normal). Run `dotnet` via
  PowerShell (it is NOT on the Bash tool's PATH here — `dotnet … | Select-Object` in Bash
  fails with exit 127).
- Library so far (`src/ClaudeExplorer.Core`):
  - **Config engine** (`Model/`, `Io/IFileSystem.cs`, `Discovery/`, `Reading/`, `Merge/`,
    `EffectiveConfigService.cs`) — effective merged `settings.json` across scopes with
    provenance, conflicts, per-key merge (scalar last-wins, permission-list union, hooks
    concat, env expansion).
  - **Artifact discovery** (`Artifacts/`) — commands/skills/subagents across User/Project/
    Plugin with frontmatter parsing, summary extraction, and shadow/override resolution.
  - **Dependency health** (`Dependencies/`) — `IProcessRunner`/`IPathResolver` seams
    (+ `Physical*` impls + fakes), `ExecutableExtractor`, minimal `McpServerReader`
    (`mcpServers` from settings + project `.mcp.json`), `DependencyExtractor` (hooks `command`
    strings + MCP commands → deduped refs), `DependencyChecker` (resolve + allowlisted
    `--version` probe → Found/Missing/Unverifiable), `DependencyHealthService` façade.
    **Safety:** only the 15-runtime allowlist is ever executed, only with `--version`, by
    resolved path; discovered commands/arbitrary binaries are never run.
  - **Catalog** (`Catalog/`) — `ICatalogFetcher` seam (+ `HttpCatalogFetcher` + fake; the only
    network boundary), `SourceDetector` (owner/repo · github URL · http(s) URL → typed
    `CatalogSource`), `MarketplaceManifestParser` (`marketplace.json` → `CatalogItem`s),
    `MarketplaceTrust` (official Anthropic = Verified, else Community), `InstalledMarketplaceReader`
    (reads `~/.claude/plugins/marketplaces/*` via `IFileSystem`), `CatalogService` façade
    (`BuildInstalledCatalog` local + `FetchAddedSource` remote). **Metadata-only:** only a manifest
    GET ever hits the network; nothing is downloaded/run/installed (that's Phase 6).
  - **Recommendations** (`Recommendations/`) — pluggable `ISignalDetector`s (Language/Framework/
    TestRunner/Database via marker files) → `SignalDetectionService` → `ProjectSignals` (each
    `Signal` carries file `Evidence`); `InstalledPluginsReader` (plugin names from the cache);
    `RecommendationMatcher` (token match name/tag/summary → confidence + Strong/Consider/
    AlreadyCovered buckets; excludes installed; drops reason-less items); `RecommendationService`
    façade (+ optional runtime/dep-health annotation). **Local-only:** reads the project tree, never
    uploads it; no network in this namespace. Every recommendation carries why + linkable evidence.
  - **Safe-mutation** (`Mutation/`) — `IFileWriter` write seam (+ `PhysicalFileWriter` impl;
    `InMemoryFileSystem` extended to also implement `IFileWriter` for test isolation);
    `ScopeTargetResolver` (resolves `EditMode` — EditWinner / OverrideAtProject / OverrideAtLocal —
    to a concrete `ResolvedTarget` file path); `SettingsValidator` (structural JSON validation:
    tolerates comments + trailing commas, checks model/outputStyle/env/permissions/hooks shapes,
    collects all errors); `FrontmatterValidator` (validates `---` frontmatter presence + required
    fields, reusing the Phase-2 `Frontmatter` parser); `DiffGenerator` (LCS-based line diff →
    `Diff`/`DiffLine`/`DiffKind` model with 1-based line numbers); `FileBackupStore` (implements
    `IBackupStore` — timestamped, counter-disambiguated `.bak` snapshots using `IFileSystem` +
    `IFileWriter` seams); `ChangeLog` (scope-aware in-memory record — sequential ids, `MarkUndone`,
    `ByScope` grouped in precedence order); `Mutator` (validate → backup → write → record for
    config edits; `IProcessRunner` + `claude` CLI delegation for installs; `Undo` restores/deletes
    or runs uninstall command); `SafeMutationService` façade (wires resolver + mutator; owns the
    session `ChangeLog`; single entry point for the UI). Plan: `2026-06-07-06-safe-mutation.md`.
  - **Blueprint UI shell + Dashboard** (`src/ClaudeExplorer.App`) — Photino.Blazor 4.0.13 +
    MudBlazor 9.5.0 desktop app; MVVM (`ObservableObject` base, `DashboardViewModel`,
    `ShellViewModel`); Blueprint theme (graph-paper grid, corner-tick panels, Archivo + Spline
    Sans Mono bundled as woff2, electric-blue accent) ported from `ux-explorations/03-blueprint.html`
    into `wwwroot/css/blueprint.css`; reusable components (`CornerTickPanel`, `Pill`, `HealthGauge`,
    `StatCardView`); app chrome (`TopBar`, `LeftRail`, `MainLayout`); Dashboard page (health gauge,
    stat cards, Needs Attention, Recent Changes) bound to `DashboardViewModel` → `DashboardComputer`
    (pure derivation, fully tested) → `EngineDashboardDataSource` (real Core façades). Plan:
    `2026-06-07-07-blueprint-ui-shell.md`.
  - **Per-screen UI** (`src/ClaudeExplorer.App/Screens/*`, `Pages/*`) — Phase 8 (branch
    `phase-8-per-screen-ui`). Architecture: each screen has an `ObservableObject` ViewModel
    (loads from Core via DI + `IWorkspaceContext`, exposes view-ready model), a pure static
    mapper/computer (tested over Core records, no IO), and a logic-light Blazor view
    (subscribes to `PropertyChanged` + `RefreshService.Requested`, `IDisposable`). Screens:
    - **Effective Config** (`EffectiveConfigViewModel` + `EffectiveConfigMapper` + `EffectiveConfig.razor`)
      — precedence matrix with provenance trace and safe-edit panel (`SafeEditViewModel` + `SafeEditPanel.razor`).
    - **Commands & Skills** (`ArtifactBrowserViewModel` + `ArtifactBrowserMapper` + `CommandsSkills.razor`)
      — source-grouped master/detail browser with kind filter + search.
    - **Dependencies** (`DependencyViewModel` + `DependencyRowsMapper` + `Dependencies.razor`)
      — config-driven health list (Found/Missing/Unverifiable).
    - **Change Log** (`ChangeLogViewModel` + `ChangeLogPage.razor`) — scope-grouped reversible
      change list with undo.
    - **Marketplace** (`MarketplaceViewModel` + `MarketplaceMapper` + `Marketplace.razor`) —
      Installed / Add-source tabs; fetches metadata-only via `CatalogService`; installs through
      `SafeMutationService`. `FakeCatalogFetcher` added to App.Tests/Fakes.
    - **Recommendations** (`RecommendationsViewModel` + `RecommendationsMapper` + `Recommendations.razor`)
      — project-fit signals → Strong/Consider/AlreadyCovered buckets with evidence chips,
      confidence bar (`MatchBar`), and runtime annotations.
    Shared components: `TypeBadge`, `TrustBadge`, `ScopeTag`, `CodeViewer`, `MatchBar`, `RecCard`.
    Blueprint CSS extended for all screens in `wwwroot/css/blueprint.css`.
    **Note:** Visual/runtime behavior verified by human via `/run` (Photino opens a native window
    — not observable headless). `dotnet build` + ViewModel/mapper tests are the automated gates.
- **Phase 9 architecture (multi-environment + compare):**
  - `ClaudeEnvironment` / `EnvironmentKind` — model for Windows, WSL, and custom config roots.
  - `IWslLocator` / `WslLocator` — process seam (shells `wsl.exe`); UTF-16LE sanitization helpers (`CleanLines` / `CleanPath`) are unit-tested.
  - `EnvironmentDiscovery` — always includes a Windows env; adds WSL distros that have a `~/.claude` folder (UNC path via `wslpath -w "$HOME"`).
  - `EnvironmentStore` — JSON persistence of active env id, custom envs, and per-env project map. Uses mutable class (not positional record) for STJ round-trip compatibility.
  - `EnvironmentService` — observable singleton (singleton in DI; `Changed` event); composes discovery + custom; `SetActive`, `SetProject`, `AddCustom`, `Remove`, `Refresh`.
  - `ActiveEnvironmentWorkspaceContext` — thin `IWorkspaceContext` adapter over `EnvironmentService.Active`; makes every existing screen env-aware with no per-screen change.
  - `EnvironmentComparer` — pure static 7-category diff (Settings / Commands / Skills / Agents / MCP / Plugins / Dependencies); settings arrays compared as sorted sets; fully tested.
  - `IEnvironmentCompareDataSource` / `EngineEnvironmentCompareDataSource` — snapshot seam (not unit-tested, mirrors `Physical*` pattern).
  - `CompareViewModel` — default left = active env, right = first other; `SelectCategory`, `SetEnvironments`.
  - `EnvironmentSelector.razor` — top-bar chip with dropdown, Add Custom dialog, Refresh.
  - `Compare.razor` — `/compare` page; category tabs, summary bar, diff table with row accents + status chips.
  - `blueprint.css` — extended with `--win`/`--wsl`/`--custom` tokens, `.envchip`, `.cats/.cat`, `.summary/.scount`, `.cmp-table`/`.cmp-stat`/row accents.
  - `Program.cs` — old fixed `WorkspaceContext` registration replaced; `EnvironmentService` is singleton (loaded in factory); `WorkspaceResolver` retained for future "open project".
- **Deferrals (backlog):**
  - **Phase 10 — Environment settings sync: DONE** (CLA-97 closed 2026-06-08) — delivered via per-screen compare/sync overlays (plans 13+14) routing copies through the Phase-6 safe-mutation layer (`Sync/ConfigCopyService` + `SettingsKeyEditor` + `CopyViewModel`). Artifact/MCP/plugin file-sync remains deferred (CLA-96).
  - **Full MCP & Plugins screen** — only a stub page (`McpStub.razor` at `/mcp`) exists;
    full screen (MCP server definitions, scope/enabled state, plugin management) is deferred.

## Git
- Work on `main` (normal repo, no worktrees). Remote `origin` = the GitHub URL above; `gh`
  is authenticated as `petarvucetin`. Set `GIT_TERMINAL_PROMPT=0` so auth failures fail fast.
- Per phase: branch `phase-N-<slug>` off `main`; fast-forward merge back; push `main`;
  delete the branch. Commit messages end with the `Co-Authored-By: Claude …` trailer.

## Linear (READ THIS — easy to get wrong)
- Destination is the **`claude-browser` WORKSPACE**, team key **CLA**
  (`https://linear.app/claude-browser/team/CLA/...`). It is a SEPARATE workspace, **not**
  `station5-jarvis`. Before creating issues, run `list_teams` and confirm the **Claude
  Browser / CLA** team is returned; if only `station5-jarvis` shows, the Linear MCP is
  authed to the wrong workspace — re-auth via `/mcp` to the claude-browser workspace.
  (To run different Linear workspaces per project, add per-project, distinctly-named Linear
  MCP servers at local scope and auth each separately — the global plugin server holds one
  workspace token at a time.)
- Structure: one Linear **Project per phase** ("Phase N — …", already created for 1–8),
  one **issue per plan task**. Phases 1 & 2 projects are Completed (CLA-5…15, CLA-17…26).
  Phases 3–8 each have a Backlog **epic** issue (CLA-27…CLA-32) carrying the scope — break
  each into per-task issues when its detailed plan is written. Tech-debt issue: **CLA-16**.

## The execution playbook (how each phase was built — repeat it)
This used the **superpowers** skills. Per phase:
1. **Write the detailed plan** (`writing-plans` skill): `docs/superpowers/plans/2026-06-07-0N-<slug>.md`,
   bite-sized TDD tasks with EXACT file paths + COMPLETE C# + COMPLETE xUnit tests + commit
   messages. No placeholders. Use the roadmap's phase outline as the starting scope.
2. **Create Linear issues** under that phase's Project (one per task, status Todo).
3. **Branch** `phase-N-<slug>` off `main`; commit the plan.
4. **Build** (`subagent-driven-development` skill): because the tasks are tightly coupled
   (one cohesive library), dispatch ONE implementer subagent (model: sonnet, general-purpose)
   to execute the whole committed plan TDD-style with a commit per task. Mark issues In Progress.
5. **Two-stage review** (fresh subagents): spec-compliance first (general-purpose, sonnet) →
   then code-quality (`feature-dev:code-reviewer`). Apply `receiving-code-review` rigor:
   verify each finding (reject false positives — e.g. Phase 1's case-insensitive Find would
   break env keys; Phase 2's "frontmatter `---`" issue was a false positive), fix the real
   ones with a fix subagent, re-review until APPROVED. Log out-of-scope findings to a backlog
   issue (that's how CLA-16 was created).
6. **Finish** (`finishing-a-development-branch` skill): verify `dotnet test` green on the
   merged result → fast-forward merge to `main` → push `origin main` → delete branch.
7. **Close Linear**: issues → Done; phase Project → Completed; create a backlog issue for any
   deferred findings.

Model choice: implementer + reviewers = **sonnet** (cheap, the plan carries the exact code);
escalate to opus only if blocked. Each phase ≈ 1 plan + ~30–75 subagent tool-uses + reviews.

## Conventions & gotchas (carried forward)
- xUnit only (no FluentAssertions v8+). `System.Text.Json.Nodes`. Forward-slash paths.
- Testability via injected seams (`IFileSystem` exists; add `IProcessRunner`/`IPathResolver`/
  catalog-fetcher in later phases) — fakes in tests, never touch the real machine in tests.
- Names matched case-sensitively (ordinal) by design (commands/skills/env keys).
- Hard rules to honor in later phases: dependency probing = allowlist + `--version` only,
  never execute untrusted config (Phase 3); catalog = metadata-only until install (Phase 4);
  recommendations = local-only analysis (Phase 5); safe-mutation contract = scope-target +
  diff + validate + backup + undo + scope-aware change log (Phase 6).
- `.gitignore` already ignores `bin/`,`obj/`,`.playwright-mcp/`. A local static server for
  the prototypes may still be running on `localhost:8765` (harmless).

## To resume after Phase 9 branch merges

1. **Review + merge Phase 9**: spec-compliance + code-quality review on `phase-9-multi-environment`
   → fast-forward merge to `main` → push `origin main` → delete branch.
   Visual verify: run `/run` to confirm the environment selector (top-bar chip + dropdown),
   Compare screen (category tabs, summary bar, diff table), and LeftRail "Analyze" section
   render correctly. WSL environments appear only if the machine has a WSL distro with `~/.claude`.
2. **Phase 10 — Environment settings sync: DONE** (audit 2026-06-08; CLA-97 closed). No standalone
   Phase-10 plan was written — the intent was delivered & superseded by plans 13 (`compare-sync-base-projects`)
   + 14 (`per-screen-compare-sync`): per-screen compare/sync overlays copy settings/memory/MCP/commands/
   skills/subagents between environments via the Phase-6 safe-mutation layer (`Sync/ConfigCopyService` +
   `SettingsKeyEditor` + `CopyViewModel`, undo as one group). Minor follow-up: `ConfigCopyService.CopyMcp()`
   lacks a direct unit test (path resolution is covered).
3. **Backlog (nice-to-have v1.1+):**
   - Artifact/MCP/plugin file-sync across environments (deferred from Phase 9/10).
   - Full MCP & Plugins screen (definitions from `.mcp.json` / settings / `~/.claude.json`,
     scope/enabled state, plugin management).
   - Store publishing / code signing (architecture is ready; just build pipeline work).
   - Async catalog fetch (current `ICatalogFetcher.FetchText` is sync-over-async; a proper
     async interface + cancellation would improve UI responsiveness on slow networks).
