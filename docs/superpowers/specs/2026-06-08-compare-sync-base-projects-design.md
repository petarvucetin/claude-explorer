# Compare / Sync — Base + Projects (Phases A & B) — Design

**Status:** Proposed 2026-06-08. Mockups (Blueprint, real tokens):
`ux-explorations/nav-reorg.html` (left-rail IA), `ux-explorations/compare-sync.html`
(generalized Compare screen). Config-surface reference:
`inventivehq.com/knowledge-base/claude/where-configuration-files-are-stored`.

## Goal

Let the user **compare any two config endpoints** — a **base** (a global `~/.claude` root, i.e. an
environment: Windows / WSL / custom) **or a project** (a folder with `.claude/`) — across the **whole
config surface**, and (Phase C, separate spec) **copy/move** individual settings or artifacts between
them in any direction. This generalizes the existing env-vs-env **Compare** screen rather than adding
a new one.

This spec covers **Phase A** (nav reorg), **Phase B** (project endpoints + generalized **read-only**
Compare), and **Phase C** (copy/move via safe-mutation). They build in order — B is shippable read-only
before C adds the actions.

## Decisions (from brainstorming)

- **One screen, generalized.** Compare's endpoints become a unified list: **bases** (environments —
  already modeled by `EnvironmentService`) **+ projects** (newly addable folders). Any pair compares:
  Base↔Project, Project↔Project, Base↔Base (the existing env-vs-env case).
- **Endpoints compare OWNED config** — the files that actually live at that root (`~/.claude/*` for a
  base, `{project}/.claude/*` + project `CLAUDE.md`/`.mcp.json` for a project) — *not* the merged
  effective view, because Phase C's copy acts on owned files. (An "effective (merged with base)"
  toggle is a possible later add, out of scope here.)
- **Categories** (Phase B compares all; Phase C copy semantics noted for later):
  | Category | Source files | Copy unit (Phase C) |
  |---|---|---|
  | Settings | `settings.json`, `settings.local.json` | per key |
  | **Memory** *(new)* | `CLAUDE.md`, `CLAUDE.local.md` | whole file |
  | Commands / Skills / Subagents | `commands/`, `skills/`, `agents/` | file / dir |
  | MCP | `.mcp.json`, `~/.claude.json`, settings `mcpServers` | per server |
  | Hooks | `hooks.*` in settings | per group |
  | Plugins / Dependencies | (host/marketplace state) | **compare-only** |
- **Excluded entirely:** `.credentials.json` (secrets), `projects/` (session history), `statsig/`
  (cache).
- **Phase B is read-only** (no copy buttons yet) — it delivers the comparison; Phase C adds copy/move.

## Phase A — Left-rail IA reorg

Pure nav change. Files: `Components/LeftRail.razor`, `ViewModels/ShellViewModel.cs` (only if its
section grouping is referenced; counts are unchanged).

- **Config Artifacts** section now lists: Commands, Skills, Subagents, **Hooks**, **MCP**, **Plugins**.
- **Extensions** section is removed (MCP, Plugins fold into Config Artifacts).
- **Hooks** moves out of **Workspace**; Workspace becomes: Dashboard, Effective Config, Dependencies.
- Routes, pages, and counts are unchanged — only the grouping/order of `NavLink`s in `LeftRail.razor`
  moves. No new nav item (Compare is enhanced in place by Phase B).

## Phase B — Project endpoints + generalized read-only Compare

### B1. Endpoint model (`Compare/` or `Environments/`)
Introduce a unified **`CompareEndpoint`** the picker and comparer use:

```
enum EndpointKind { Base, Project }
record CompareEndpoint(string Id, EndpointKind Kind, string Label, string EnvId,
                       string UserDir, string? ProjectDir);
```
- **Base** endpoint: one per environment (`EnvironmentService.Environments`); `ProjectDir = null`;
  owned root = `{UserDir}/.claude`.
- **Project** endpoint: a registered folder; owned root = `{ProjectDir}/.claude` (+ project-root
  `CLAUDE.md`/`CLAUDE.local.md`/`.mcp.json`). Carries its `EnvId` (which filesystem it lives on).

### B2. Project registry (persisted)
Projects are first-class, multiple, and named — unlike the current single per-env active project
(`EnvironmentService._projects: envId→dir`). Add a registry:
- `record ProjectEndpointDef(string Id, string Name, string EnvId, string ProjectDir)`.
- Persist a `List<ProjectEndpointDef>` in the existing `environments.json` state via a **new field**
  on `EnvironmentStore.EnvironmentState` (e.g. `ComparedProjects`). This is **additive and independent**
  of the current per-env active-project map (`EnvironmentState.Projects`, which drives
  `IWorkspaceContext.ProjectDir` for the other screens). Phase B does **not** change the active
  workspace — registering a Compare endpoint never repoints Effective Config/Hooks/etc.
- A small service (a new `ProjectRegistry`, mirroring `EnvironmentService`'s shape) exposing
  `Add(name, envId, dir)`, `Remove(id)`, `All`, with a `Changed` event.
- **Validation on add:** the dir must exist; warn (don't block) if it has no `.claude/` yet (created on
  first copy in Phase C). Reuse `WorkspaceResolver.IsClaudeProject`.

### B3. Owned-config snapshot per endpoint (data source)
Generalize the snapshot to read **owned** config at an endpoint root. Rename/extend
`EnvironmentSnapshot` → **`EndpointSnapshot`** and `IEnvironmentCompareDataSource` →
`IEndpointCompareDataSource` (or add an overload), with `EngineEndpointCompareDataSource.Snapshot(endpoint)`:
- **Settings:** read the endpoint's own `settings.json` (+ `settings.local.json`) as a flat key set —
  for a base that's `~/.claude/settings.json`; for a project that's `{project}/.claude/settings.json`.
  Use `SettingsReader`/the existing flatten, scoped to the endpoint's files (not the cross-scope merge).
- **Memory (new):** presence + content of `CLAUDE.md` and `CLAUDE.local.md` at the endpoint root.
- **Commands / Skills / Subagents:** `ArtifactDiscoverer` scoped to the endpoint root only (its
  `commands/`, `skills/`, `agents/`), not the plugin/user/global union.
- **MCP:** the endpoint's `.mcp.json` / `~/.claude.json` / settings `mcpServers`.
- **Hooks:** `hooks.*` from the endpoint's settings.
- **Plugins / Dependencies:** same readers as today (compare-only).

### B4. Comparer (`Compare/EnvironmentComparer.cs`)
- Add a **Memory** category to the existing 7 (`BuildCategory("Memory", MemoryMap(a), MemoryMap(b))`).
  Rows are keyed by file (`CLAUDE.md`, `CLAUDE.local.md`); `Same`/`Differs` decided by content equality,
  and the row value is a short descriptor (e.g. `present · 1.2 KB`) — full content is a Phase-C/viewer
  concern, not stored in the diff row.
- The diff model is unchanged: `DiffStatus {Same, Differs, OnlyA, OnlyB}`, `CompareRow(Key, Status,
  ValueA, ValueB)`, `CompareCategory(Name, Rows + counts)`. Stays pure + fully tested.
- Mark Plugins/Dependencies categories as **view-only** (a flag on `CompareCategory`, used by the UI to
  hide Phase-C copy actions).

### B5. UI (`Pages/Compare.razor`, `Components/EnvironmentSelector.razor` or a new picker)
- **Endpoint pickers** (left **A** / right **B**): each a dropdown over **all endpoints** — bases
  (grouped, kind-colored) + projects — plus an **"＋ Add project…"** action opening a modal (manual
  path + name entry, matching the existing "Add custom root" modal). A **⇄ swap** control.
- **Category tabs** (incl. Memory); Plugins/Dependencies tabs marked `view`.
- **Summary bar** (Same / Differs / Only-A / Only-B) and the **diff table** (key, A value, B value,
  status accent) — reusing the existing Compare table styling. **No copy buttons in Phase B.**
- `CompareViewModel`: replace the env-pair selection with endpoint-pair selection
  (`SetEndpoints(aId, bId)`), default A = active base, B = first project (or first other base).

### Existing-code touch-ups (kept focused)
- `EnvironmentComparer` already does category diffing — extend, don't rewrite.
- `EnvironmentSelector.razor` modal pattern is reused for "Add project".
- If `EnvironmentSnapshot`/data-source rename ripples into `CompareViewModel` + tests, update call
  sites in the same change.

## Testing (Phase B)

Deterministic, fixture-driven, no real machine state:
- **Project registry:** add/remove/persist round-trip (fixture `environments.json`), validation
  (missing dir, no-`.claude` warning).
- **Owned-config snapshot:** for a base vs a project fixture tree — settings keys, Memory presence,
  scoped artifact discovery (project `commands/` only), MCP, hooks; confirms it reads OWNED (not merged)
  config.
- **Comparer:** Memory category diff (Same/Differs/OnlyA/OnlyB); existing category tests still pass;
  Plugins/Deps flagged view-only.
- **`CompareViewModel`:** endpoint-pair selection, swap, default endpoints, category selection.
- **No render tests** (Photino headless) — Compare screen verified via `/run` + the mockups.

## Phase C — Copy / Move

Per-row copy in either direction on the diff, routed through `SafeMutationService` (preview → backup →
write → change-log → undo). Each category has its own **copy operation**; all share the same
preview/confirm/apply/undo flow.

### C1. Core copy operations (`Core/Sync/`, pure + tested)
A `ConfigCopier` dispatching by category (or one small operation per category), each taking a source
endpoint root + a target endpoint root + the row key:
- **Settings (per key):** read the source settings file, take that top-level key's value, splice it into
  the target settings file (create if missing). A reusable `SettingsKeyEditor.SetKey(text, key, value)` /
  `.RemoveKey(text, key)` (the top-level analogue of `HookBlockEditor`).
- **Hooks (per group):** reuse `HookBlockEditor.ExtractBlock` from source + a `SpliceBlock`/append into
  the target's `hooks.<event>` array.
- **MCP (per server):** copy the named server object from source `.mcp.json`/`~/.claude.json`/settings
  into the target's MCP file (create if missing).
- **Memory (whole file):** copy `CLAUDE.md` / `CLAUDE.local.md` content from source root to target root
  (overwrite; diff-previewed).
- **Commands / Skills / Subagents (file/dir):** copy the artifact's file(s) from the source root's
  `commands/`·`skills/`·`agents/` to the target's (a skill is its dir: `SKILL.md` + sibling files).
- Each returns new target content (for settings/MCP/hooks → an `EditPreview` via the existing `Mutator`;
  for files → a target write). **Move** = the copy op **plus** a remove-from-source op (RemoveKey /
  delete file / drop server entry), recorded as a grouped pair of change-log entries.

### C2. Safe-mutation extensions (`Core/Mutation/`)
- `Mutator.PreviewEdit(target, newContent, validation)` already exists for arbitrary file writes — reuse
  for settings/MCP/memory/file targets (validation = JSON for settings/MCP, none for markdown/files).
- Add a **remove-key** path for settings move (via `SettingsKeyEditor.RemoveKey` → preview/apply).
- File-artifact copy: write target file (backup if it exists) + change-log + undo (restore/delete);
  `IFileWriter` already supports `WriteAllText`/`Delete`. Skill dirs copy each file.

### C3. UI (Phase C additions to `Pages/Compare.razor`)
- Each diff row gains **`→`** (copy A→B) and **`←`** (copy B→A) buttons; disabled where N/A (e.g. the
  `→` on an Only-B row), hidden for **view-only** categories (Plugins/Dependencies).
- Clicking opens a small confirm: for settings, a **shared vs local** target picker (reuses
  `EditMode.OverrideAtProject`/`OverrideAtLocal` when the target is a project); a **diff preview**; and
  **Copy / Move / Cancel**. Move shows a warning (esp. when the **source is a base/global** — affects
  every consumer). Apply → safe-mutation → applied banner + **Undo**, then the compare re-snapshots so
  the row updates.

### C4. Testing (Phase C)
- Each Core copy op (settings key, hook group, MCP server, memory file, command/skill file): copy
  creates/updates the target correctly; **move** also removes from source; preview/validate/backup/undo.
- `SettingsKeyEditor` set/remove (round-trip, preserves siblings, refuses invalid JSON).
- ViewModel: copy/move flow, direction enable/disable per status, re-snapshot after apply, view-only
  categories blocked, shared/local target routing.

## Out of scope (A–C)
- "Effective (merged)" compare toggle — endpoints compare owned config only.
- Comparing `.credentials.json`, `projects/`, `statsig/`.
- Plugin install-state copy and Dependencies copy (both stay compare-only).
- Bulk "copy all differences" — copy/move is per-row in v1.
