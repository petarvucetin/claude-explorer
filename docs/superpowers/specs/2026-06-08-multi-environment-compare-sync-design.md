# Multi-Environment Compare & Sync — Design

**Status:** Approved (2026-06-08). Decomposes into **Phase 9** (multi-environment + compare) and
**Phase 10** (settings sync). Builds on the completed v1 (Phases 1–8).

## Problem

Claude Code is configured per-OS: a user running both Windows and WSL has **two independent
standard folders** — `C:\Users\<you>\.claude` (Windows) and `~/.claude` inside each WSL distro
(`\\wsl.localhost\<distro>\home\<you>\.claude`). Today Claude Explorer reads exactly one
user-global root. The user wants to:

1. **See each environment separately** — view any environment's full config surface.
2. **Compare** Windows ⇄ WSL across the whole surface: settings, commands, skills, agents, MCP,
   plugins, dependencies.
3. **Sync** values between environments — per-attribute and whole-config, both directions.

## Goals / Non-Goals

**Goals**
- Model multiple Claude environments (Windows, WSL distro(s), user-added custom roots).
- Auto-discover Windows + WSL environments; allow manual add.
- Make every existing screen reflect a selectable **active environment** (each with its own project).
- A **Compare** screen (user-global only) diffing two environments per category.
- **Sync** of `settings.json` between environments (attribute-level + whole-file), via the existing
  safe-mutation layer (diff → validate → backup → undo → change log).

**Non-Goals (deferred, tracked)**
- **Artifact/MCP/plugin file sync** — copying a skill/command/agent's files, or installing a
  plugin/MCP, across the Win↔WSL filesystem boundary. Compare *shows* these diffs; sync of them is a
  later extension.
- Multi-**project** compare within one environment (already deferred, CLA-86).
- Full MCP & Plugins screen (already deferred, CLA-87).
- Editing config inside compare beyond sync (regular edits stay on the per-environment screens).

## Concepts

```
enum EnvironmentKind { Windows, Wsl, Custom }

record ClaudeEnvironment(
    string Id,            // stable key, e.g. "windows", "wsl:Ubuntu", "custom:<hash>"
    string Name,          // display, e.g. "Windows", "WSL · Ubuntu"
    EnvironmentKind Kind,
    string UserDir,       // home dir holding .claude (Windows path or \\wsl.localhost\... UNC)
    string? ProjectDir);  // per-environment active project (null = user-global only)
```

- **Windows** — always present. `UserDir = %USERPROFILE%`.
- **WSL** — one per distro that has a `.claude`. `UserDir = \\wsl.localhost\<distro>\home\<you>`.
- **Custom** — user-added root (any path containing `.claude`).

## 1 — Discovery

`EnvironmentDiscovery` (Core-or-App service, tested seam over `IProcessRunner` + `IFileSystem`):

- **Windows:** emit a Windows environment from `%USERPROFILE%` (only if `%USERPROFILE%\.claude` exists,
  else still emit it as the default).
- **WSL:** run `wsl.exe -l -q` to list distro names. For each distro, resolve the home via
  `wsl.exe -d <distro> -- wslpath -w "$HOME/.claude"` (returns the `\\wsl.localhost\...` Windows path);
  include the environment when that path's directory exists. Skip silently if `wsl.exe` is absent or
  errors (no WSL installed → just Windows).
- **Custom:** persisted user-added roots (see persistence below).

**Technical notes / risks (validate with a spike in Phase 9):**
- `wsl.exe -l -q` historically emits **UTF-16LE** with NUL padding on some Windows builds — the
  process-output decoder must handle this (decode UTF-16, strip NULs/CR, trim blanks). `IProcessRunner`
  returns a decoded string; add a tolerant parse (also accept UTF-8).
- This reuses `IProcessRunner` for a non-`--version` command. That allowlist was a *DependencyChecker*
  policy, not a global `IProcessRunner` rule (installs already run the `claude` CLI through it). Running
  `wsl` for environment discovery is an explicit, app-level use — acceptable.
- **UNC path handling:** `PhysicalFileSystem`/`SettingsLocator` build `{UserDir}/.claude/...`. With a
  backslash UNC `UserDir` (`\\wsl.localhost\Ubuntu\home\you`) this yields a mixed-separator path; .NET
  on Windows accepts mixed separators, but the spike must confirm `File.Exists`/`GetFiles` work against
  `\\wsl.localhost\...`. Keep WSL `UserDir` as a backslash UNC string; do NOT forward-slash-normalize the
  `\\` prefix. If forward-slash UNC fails, add a targeted normalization that preserves the `\\` prefix.
- **Performance:** discovery runs at startup and on manual refresh, not per-render.

## 2 — App integration (active environment)

`EnvironmentService` (observable singleton):
- `IReadOnlyList<ClaudeEnvironment> Environments`, `ClaudeEnvironment Active`, events on change.
- `SetActive(id)`, `AddCustom(path)`, `Remove(id)`, `Refresh()` (re-run discovery).
- Each environment carries its own `ProjectDir`; `SetProject(envId, dir)` updates it.
- **Persistence:** custom environments + per-environment active project + last active environment are
  saved to `%USERPROFILE%/.claude/.claude-explorer/environments.json` (via `IFileWriter`; tolerant read).

`IWorkspaceContext` becomes a **thin adapter** over `EnvironmentService.Active` — `UserDir`,
`ProjectDir`, `ProjectLabel` (label = active env name, plus project segment when a project is set). All
existing screens depend on `IWorkspaceContext` and therefore reflect the active environment with **no
change to those screens**. The Phase-7 `RefreshService` already re-loads screens; switching the active
environment raises a refresh.

**Top bar:** the project chip is replaced by an **environment selector** (active env + dropdown to
switch / add). Switching env → `EnvironmentService.SetActive` → refresh.

## 3 — Compare screen (read-only)

New `Compare` page (`/compare`) under an "Analyze" rail group. **User-global only** (no project overlay).
Picks two environments (default: Windows vs first WSL); the top bar shows both with a `vs` separator.

Pure, tested **`EnvironmentComparer`** with one mapper per category. Each produces rows of:

```
enum DiffStatus { Same, Differs, OnlyA, OnlyB }   // A = left env, B = right env
record CompareRow(string Key, DiffStatus Status, string? ValueA, string? ValueB, /* category extras */);
record CompareCategory(string Name, int Same, int Differs, int OnlyA, int OnlyB, IReadOnlyList<CompareRow> Rows);
```

Per-category source data (each read for env A and env B at their `UserDir`, no project):
- **Settings** — user-scope `settings.json` keys (flattened: `model`, `permissions.allow`, `env.*`,
  `hooks.*`, …). Compare values; list/array keys compared as sets. Rows expand to each side's raw value.
- **Commands / Skills / Agents** — `ArtifactCatalogService.Build(userDir)` filtered by kind; compare by
  artifact name; `Differs` when the summary/source differs.
- **MCP** — `McpServerReader.Read(userDir)`; compare by server name (command+args).
- **Plugins** — installed plugin names (`InstalledPluginsReader` / `CatalogService.BuildInstalledCatalog`).
- **Dependencies** — `DependencyHealthService.Check(userDir)`; compare by dep name + status.

**View (Approach A, mockup `ux-explorations/10-blueprint-compare.html`):** category tabs with per-tab
counts; a diff table `Key | <EnvA> | <EnvB> | Status` with status pills and row accent colors
(differs=amber, only-A/only-B=env color); a summary bar (same / differs / only-A / only-B). Reuses
Phase-7/8 components (`CornerTickPanel`, `Pill`, `ScopeTag`, `CodeViewer`).

`CompareViewModel`: loads both environments' category data, runs `EnvironmentComparer`, exposes the
selected category + the diff model; `IsLoading` / `ErrorMessage` like the other screens.

## 4 — Sync (Phase 10, settings.json only)

In Compare's **Settings** category, each row gains directional actions and there are top-level
**Sync all → \<env\>** actions. Sync **always routes through `SafeMutationService`** so it inherits diff
preview, schema validation, timestamped backup, undo, and change-log entries.

The sync target is always the **user-scope `settings.json` of the target environment** — a concrete
`ResolvedTarget(ScopeKind.User, "{targetUserDir}/.claude/settings.json")`. The Phase-6 `Mutator` already
exposes `PreviewEdit(ResolvedTarget, newContent, ValidationResult)` / `PreviewSettingsEdit(ResolvedTarget,
newContent)`; Phase 10 adds a thin `SafeMutationService.PreviewSettingsEdit(ResolvedTarget, newContent)`
passthrough (plus a `ResolvedTargetFor(env)` factory) so sync is just a new *caller* of the existing
layer — no new mutation primitives.

`EnvironmentSync` (tested helper):
- **Attribute sync** `BuildKeyEdit(sourceEnv, targetEnv, key)`: read target's `settings.json` (or `{}`),
  set `key` to source's value (or remove it when source lacks the key, for an exact match), serialize →
  return the `(ResolvedTarget, newContent)` for the target env. The Compare row previews it
  (`SafeMutationService` → diff + validation), the user confirms, then `ApplyEdit` writes it with backup
  + change-log + undo.
- **Whole-config sync** `BuildFullEdit(sourceEnv, targetEnv)`: proposed target content = source's entire
  `settings.json` (overwrite). Same preview → validate → backup → apply path. (Deep-merge instead of
  overwrite is a future option, out of scope now.)
- Direction is explicit per action (→ / ←); the change log records each sync with both env names so it
  is reviewable and one-click reversible from the Change Log screen.

## Components & boundaries (new)

| Unit | Responsibility | Depends on | Tested |
|------|----------------|------------|--------|
| `ClaudeEnvironment`, `EnvironmentKind` | model | — | — (record) |
| `EnvironmentDiscovery` | enumerate Windows/WSL/custom envs | `IProcessRunner`, `IFileSystem` | yes (fakes) |
| `EnvironmentStore` | persist custom envs + active selections | `IFileSystem`, `IFileWriter` | yes |
| `EnvironmentService` | active env + list + events (observable) | discovery, store | yes |
| `IWorkspaceContext` adapter | active env → UserDir/ProjectDir/Label | `EnvironmentService` | yes |
| `EnvironmentComparer` (+ per-category mappers) | diff two envs → `CompareCategory` rows | Core records only | yes (pure) |
| `CompareViewModel` | load both envs, run comparer, expose model | services + comparer | yes (fake source) |
| `Compare.razor` + env selector | view | components | no (visual `/run`) |
| `EnvironmentSync` | build target settings + route to safe-mutation | `SafeMutationService` | yes |
| `SyncViewModel` / Compare sync actions | preview + apply sync | `EnvironmentSync` | yes |

## Testing

- Discovery: fake `IProcessRunner` returning canned `wsl -l`/`wslpath` output (incl. UTF-16 + no-WSL +
  error cases) + in-memory FS → expected environment list.
- Comparer: construct Core records for two envs directly → assert per-category statuses + counts.
- ViewModels: fake data sources; assert category selection, loading, error.
- Sync: in-memory `SafeMutationService` over fake FS → attribute/whole sync produces correct target
  content, backup, change-log entry, and undo restores.
- A Phase-9 **spike test/script** validates real WSL discovery + UNC `.claude` reads on this machine
  (documented, not a committed unit test).
- No test touches the real machine; visual fidelity verified by human `/run`.

## Build decomposition

**Phase 9 — Multi-environment + Compare (read-only)**
Model; `EnvironmentDiscovery` (+ WSL/UNC spike); `EnvironmentStore`; `EnvironmentService`;
`IWorkspaceContext` adapter (existing screens now active-env-aware); top-bar environment selector;
`EnvironmentComparer` (all 7 categories); `CompareViewModel` + `Compare.razor` + rail "Analyze" group;
DI/nav wiring; docs. Delivers *see separately + compare*.

**Phase 10 — Environment sync (settings.json)**
`EnvironmentSync` (attribute + whole), `ResolvedTarget`-for-env factory; Compare Settings row + bulk
sync actions wired to `SafeMutationService` (preview/validate/backup/undo/change-log); `SyncViewModel`;
docs. Delivers *sync*.

Each phase: full TDD plan → Linear issues (CLA, "Phase 9/10" projects/epics) → branch → implementer →
two-stage review → ff-merge → push → close Linear — the established playbook.

## Open risk to resolve first (Phase 9, task 1)
The WSL discovery + `\\wsl.localhost\...` `.claude` read must be proven on a real machine before the rest
of Phase 9 is built. If forward-slash/UNC path building fails in `SettingsLocator`/`PhysicalFileSystem`,
add a minimal, well-tested path-normalization that preserves the `\\` UNC prefix (a small Core tweak),
rather than reworking every engine.
