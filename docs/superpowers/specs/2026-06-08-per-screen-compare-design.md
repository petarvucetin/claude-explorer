# Per-screen Compare & Sync — Design

**Date:** 2026-06-08
**Status:** Approved (brainstorming) — ready for implementation plan
**Supersedes:** the central Compare page (`2026-06-08-compare-sync-base-projects-design.md`),
which this design **retires** in favor of compare living inside every artifact screen.

## Problem

Compare/Sync exists only as one central `/compare` page with category tabs, and copy/move
is wired for just Settings, Memory, and MCP. The user wants to compare a given artifact
across **any pair of endpoints** (base ↔ WSL, base ↔ Project A, Project A ↔ Project B, …)
**from within that artifact's own screen**, for **all** artifacts.

## Decisions (locked)

| # | Decision |
|---|----------|
| 1 | **Affordance = "Compare with" overlay** on each artifact screen (master/detail list + diff chips + detail-pane diff + copy/move). Not a separate page, not an N-way matrix. |
| 2 | **Both sides pickable** — compare bar `A ▾ ⇄ B ▾`; A defaults to active endpoint. Enables Project A ↔ Project B without changing the global active env. |
| 3 | **Skills = recursive folder copy/move** (a skill is a directory: `SKILL.md` + resources/scripts). |
| 4 | **Move = real undo-able delete** in the safe-mutation layer (replaces today's "move of files not supported" degrade). |
| 5 | **Add a Memory screen** (left rail, under Config Artifacts) for CLAUDE.md — it had no screen, only a Compare tab. |
| 6 | **Retire the central Compare page** and its left-rail entry. |
| 7 | **View-only** for Plugins & Dependencies (status/presence diff, no copy). |

## Interaction model

- Each artifact screen gains a **compare bar**: `A ▾   ⇄   B ▾`.
- **Off by default**: when B is unset, the screen behaves exactly as today (single endpoint
  = active env). Selecting a B endpoint enters compare mode.
- **Endpoints**: bases (Windows/WSL/custom `~/.claude`) + project folders — the existing
  `CompareEndpoint` set (`EnvironmentService` bases + `ProjectRegistry` projects).
- **Shared compare context**: the chosen A/B persists across screen navigation (set once,
  applies to Commands, Skills, Hooks, … until changed).
- **Per-row diff chip**: `=` same · `≠` differs · `◑` A-only · `○` B-only.
- **Detail area** for the selected row shows the diff with **Copy →/←**, **Move**, **Undo**.

### Defaults chosen
- Compare is **off** until B is picked.
- A/B **persists** across screens.
- **"Add project endpoint"** moves from the (retired) Compare page into the **top-bar
  environment selector** (alongside Add Custom env).

## Per-screen behavior (by data shape)

| Screen | Shape | Diff rendering | Copy/Move |
|--------|-------|----------------|-----------|
| Effective Config (settings) | value | row-value diff | Copy + Move; reuse **scope-target picker** (settings.json vs settings.local.json) |
| Commands | file (`.md`) | side-by-side content diff | Copy + Move |
| Subagents | file (`.md`) | side-by-side content diff | Copy + Move |
| Memory (CLAUDE.md) | file | side-by-side content diff | Copy + Move |
| Skills | directory | per-file diff inside the folder | Copy + Move **whole folder** (recursive) |
| Hooks | json group | hook-group JSON diff (key `event#idx`) | Copy + Move (Core already supports) |
| MCP | json entry | server-def JSON diff | Copy + Move (Core already supports) |
| Plugins | view-only | installed/enabled status diff | none (install via CLI/Marketplace) |
| Dependencies | view-only | runtime present/version diff | none (machine probe, not config) |

## Architecture

### Reused (no change)
`EnvironmentComparer` (per-category diff), `CompareEndpoint` / `ProjectRegistry`,
`IEnvironmentCompareDataSource` / `EngineEnvironmentCompareDataSource` (snapshots),
`ConfigCopyService`, `CopyViewModel`, `SafeMutationService`, `DiffGenerator`, `CodeViewer`.

### New / changed

1. **Core — recursive directory copy/move plan** (`ConfigCopyService`): for Skills, enumerate
   the source folder tree → a multi-file plan. Current `CopyFile` handles a single file only.
2. **Core — undo-able delete** in safe-mutation (`Mutator` / `IFileWriter` + backup/undo):
   forward delete of a file or directory, backed up, so **Move** completes and **Undo**
   re-creates. Removes the current `CopyViewModel` "move of files not supported" path.
3. **Enrich diff rows with the artifact's resolved path (+ content)** — the missing piece
   that blocked Commands/Skills/Agents copy (the row had no path to build a `CopyRequest`).
   Reconcile the category-name mismatch: comparer `"Agents"` vs `ConfigCopyService`
   `"Subagents"`.
4. **App — shared `CompareContext`** (observable A/B endpoint selection) + a reusable
   **compare-bar + diff-overlay** component bound into each existing screen ViewModel/view.
5. **App — Memory screen**: left-rail entry + page + ViewModel; discovers global/project/
   nested CLAUDE.md (reuse the snapshot's `Memory` map).
6. **App — retire** `/compare` page + left-rail "Compare" entry; **relocate Add-project**
   into the top-bar environment selector.

## Safety

Unchanged contract: diff preview + explicit confirm, automatic timestamped backup,
schema/frontmatter validation, scope-aware change log, one-click undo. Additions:

- New **delete** is backed up before removal; Undo restores from backup.
- A **recursive copy** is recorded as one change-log **group** so a single Undo reverts the
  whole folder atomically (from the user's perspective).

## Testing

Deterministic unit tests (fixture `.claude` dirs, in-memory FS — never touch real machine):
- recursive directory-copy planner (files, nested, resources);
- delete mutation: backup created, undo re-creates file/dir;
- enriched-row path resolution per category (incl. Agents↔Subagents naming);
- per-screen compare mapping (diff chips, A/B selection, off-by-default);
- Memory discovery (global/project/nested load order).

Visual/runtime behavior verified by human via `/run` (Photino is not observable headless).

## Out of scope (v1)

- Plugin and Dependency **copy** (compare only; a "Install on B" deep-link to Marketplace
  is a later nicety).
- N-way (3+ endpoint) matrix view — the A/B overlay was chosen instead.

## Affected files (indicative)

- `src/ClaudeExplorer.Core/Sync/ConfigCopyService.cs` — recursive dir plan; Agents naming.
- `src/ClaudeExplorer.Core/Mutation/*` — undo-able delete (`Mutator`, `IFileWriter`,
  `FileBackupStore`, `SafeMutationService`).
- `src/ClaudeExplorer.App/Compare/*` — `CompareContext`; compare-bar + diff-overlay
  component; enriched rows; `CopyViewModel` (drop the move-degrade path).
- Each screen view + ViewModel: `Pages/{Commands,Skills,Subagents,Hooks,Mcp,Plugins,
  EffectiveConfig,Dependencies}.razor` + `Screens/*`.
- **New** `Pages/Memory.razor` + `Screens/Memory/*` + left-rail entry.
- **Remove** `Pages/Compare.razor` + its left-rail entry; move Add-project to the env selector.
- Tests under `tests/ClaudeExplorer.Core.Tests/Sync`, `…/Mutation`,
  `tests/ClaudeExplorer.App.Tests/Compare`, plus a Memory test.
