# Claude Explorer — Handoff / Continuation Guide

Read this first when resuming with no prior conversation context. It captures where the
build is, how it's being built, and how to pick up the next phase.

## What this project is
A cross-platform desktop app (.NET 10 · Photino + Blazor + MudBlazor, MVVM) to **discover,
safely edit, install, and recommend** the settings & tooling that affect Claude Code.
Full spec: `CLAUDE.md`. UI direction **Blueprint**: prototypes + screenshots in
`ux-explorations/` (`03-blueprint.html` is the chosen look; `04`–`09` are the per-screen
prototypes). Phase decomposition + per-phase scope: `docs/superpowers/plans/2026-06-07-00-roadmap.md`.

## Current state (2026-06-07)
- **Phases 1–7 are DONE, merged to `main`, and pushed** to `origin`
  (`https://github.com/petarvucetin/claude-explorer.git`). `git log` tip ≈ `db1defe`.
- **`dotnet test` → 229 passing** (205 Core + 24 App). `.NET SDK 10.0.300` is installed.
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
    (pure derivation, fully tested) → `EngineDashboardDataSource` (real Core façades); route stubs
    for all other screens. Plan: `2026-06-07-07-blueprint-ui-shell.md`.
    **Note:** Visual/runtime behavior verified by human via `/run` (Photino opens a native window
    — not observable headless). `dotnet build` + ViewModel/computer tests are the automated gates.
- **Next up: Phase 8 — Per-screen UI**.

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

## To resume: "continue to Phase 8"
1. Finish Phase 7: `finishing-a-development-branch` skill on `phase-7-ui-shell` →
   fast-forward merge to `main` → push `origin main` → delete branch → close Linear issues
   (epic **CLA-31** + task issues → Done, Project → Completed).
   Visual verify: run `/run` to confirm Blueprint shell + Dashboard look matches `03-blueprint.html`.
2. Confirm Linear is on the **CLA** workspace (`list_teams` → "Claude Browser" / CLA).
3. Author `docs/superpowers/plans/2026-06-07-08-per-screen-ui.md` from the roadmap's Phase 8
   outline (full TDD detail). Screens: Effective Config precedence matrix (`04-blueprint-…`),
   Commands & Skills (`05`), Dependencies, Marketplace browse + add-source + install (`07`, `08`),
   Recommended-for-project (`09`), Change Log, multi-project compare.
   **Depends:** Phases 1–7 + per-screen Core engines.
4. Create Phase-8 task issues under "Phase 8 — Per-screen UI" project (epic **CLA-32**).
5. Run the playbook (branch `phase-8-…` → one implementer subagent (sonnet) → spec +
   `feature-dev:code-reviewer` review → fix loop → ff-merge → push → close Linear).
   Phase-7 reference: plan `…-07-blueprint-ui-shell.md`, issues CLA-31 + task issues.
