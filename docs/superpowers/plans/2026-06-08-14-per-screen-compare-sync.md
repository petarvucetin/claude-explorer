# Per-screen Compare & Sync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move Compare/Sync out of the central `/compare` page and into every artifact screen as a reusable "Compare with" overlay. A shared, navigation-persistent `CompareContext` holds the A/B endpoint pair; each screen renders a compare bar (`A ▾ ⇄ B ▾`, off by default) + per-row diff chips (`= ≠ ◑ ○`) + a detail-area diff with Copy →/← / Move / Undo. Core gains recursive directory copy/move (Skills) and a real undo-able delete (so Move works for files & dirs). Diff rows carry each artifact's resolved path + content (the missing piece that blocked Commands/Skills/Agents copy). Add a Memory screen (CLAUDE.md). Retire the central Compare page and relocate "Add project endpoint" into the top-bar environment selector.

**Architecture:** Core stays UI-agnostic. `ConfigCopyService.CopyPlan` is extended with a list of write-ops + a list of delete-ops so a recursive Skills folder copy/move is ONE plan; the existing single-file fields are retained for backward compatibility. The safe-mutation layer gains `ChangeKind.Delete` + `Mutator.ApplyDelete` (backup → delete → record; Undo re-creates). The App `EnvironmentComparer` threads each artifact's resolved file path (+ content for the content diff) into a new `CompareRow.PathA/PathB/ContentA/ContentB`, and the comparer category for subagents is renamed `"Agents"` → `"Subagents"` to match `ConfigCopyService`'s dispatch. A new singleton `CompareContext` (observable A/B) is injected into every screen; a `CompareBar.razor` + `DiffOverlay.razor` component pair renders the overlay and drives `CopyViewModel`. The central `Pages/Compare.razor` and its left-rail entry are deleted; the Add-project affordance moves into `EnvironmentSelector.razor`.

**Tech Stack:** .NET 10, Photino.Blazor, MudBlazor, MVVM, xUnit

**Spec:** `docs/superpowers/specs/2026-06-08-per-screen-compare-design.md` (supersedes the central-compare design).

**Conventions (verified against the repo):**
- xUnit only (`[Fact]`/`[Theory]`, underscore method names). No FluentAssertions. `System.Text.Json.Nodes`. Forward-slash paths. Names matched **case-sensitively (ordinal)**.
- App tests live in `tests/ClaudeExplorer.App.Tests/`, Core in `tests/ClaudeExplorer.Core.Tests/`; both have an `InMemoryFileSystem` fake implementing **both** `IFileSystem` and `IFileWriter` (`AddFile`, `WriteAllText`, `Delete`).
- `IFileWriter` (in `ClaudeExplorer.Core.Io`) already exposes `void WriteAllText(string, string)` and `void Delete(string)`.
- `SafeMutationService(IFileSystem fs, IFileWriter writer, IBackupStore backups, IProcessRunner runner)`; it owns a `ChangeLog`. Existing methods: `PreviewEdit(ResolvedTarget, string, ValidationResult)`, `PreviewSettingsEdit(...)`, `ApplyEdit(EditPreview, string ts, string? desc)`, `Install(...)`, `Undo(ChangeLogEntry)`.
- `ChangeKind { Edit, Install, Uninstall }`; `ChangeLogEntry(Id, Timestamp, Kind, Scope, FilePath, Description, BackupEntry? Backup, IReadOnlyList<string>? UndoCommand, bool IsUndone)`; `ChangeLog.Record`/`MarkUndone`/`Entries`/`ByScope`.
- `IBackupStore.Backup(string originalPath, string? originalContent, bool originalExisted, string timestamp)` → `BackupEntry(OriginalPath, BackupPath, Timestamp, bool OriginalExisted)`; `Read(BackupEntry)`.
- `ResolvedTarget(ScopeKind Scope, string FilePath)`; `ValidationResult.Ok` / `.Fail(...)`; `MutationException`.
- `ScopeKind { Plugin=-1, User=0, Project=1, Local=2, Enterprise=3 }`.
- `ArtifactKind { Command, Skill, Subagent }`; `DiscoveredArtifact(Kind, Name, Summary, Source, FilePath, Frontmatter?, ExtraFileCount)`; `ResolvedArtifact(Winner, Shadowed)`; `ArtifactCatalog.OfKind(kind)`. A **skill's** `Winner.FilePath` is its `…/skills/<name>/SKILL.md` (so the skill **directory** is `Path.GetDirectoryName`).
- `CompareEndpoint(Id, Kind, Label, UserDir, ProjectDir?)` with `ReadUserDir`/`ReadProjectDir`/`Base(...)`/`Project(...)`; `EndpointKind { Base, Project }`.
- `EnvironmentSnapshot(Settings, Artifacts, Mcp, Plugins, Dependencies, Memory)`; `IEnvironmentCompareDataSource.Snapshot(ClaudeEnvironment)` + `.Snapshot(CompareEndpoint)`.
- `EnvironmentComparer.Compare(a, b)` → `EnvironmentComparison(Categories)` with `Find(name)`. Categories today: `Settings, Commands, Skills, Agents, MCP, Memory, Plugins, Dependencies` (this plan renames `Agents`→`Subagents`).
- `CompareRow(Key, DiffStatus, ValueA, ValueB)`; `DiffStatus { Same, Differs, OnlyA, OnlyB }`; `CompareCategory(Name, Rows, bool ViewOnly=false)` with `Same/Differs/OnlyA/OnlyB` counts.
- `EnvironmentService` (singleton): `Environments`, `Active`, `Load/Refresh/SetActive/SetProject/AddCustom/Remove`, `event Changed`. `ProjectRegistry` (singleton): `All`, `Load/Add(name,envId,dir)/Remove(id)`, `event Changed`.
- `IWorkspaceContext { UserDir, ProjectDir, ProjectLabel }` (active-env adapter). `RefreshService { event Requested; Request() }`.
- MVVM base `ObservableObject` (`SetProperty`, `OnPropertyChanged`). Clock seam: DI `Func<string>` returns ISO timestamp.

**Run one filtered (PowerShell — `dotnet` is NOT on the Bash PATH):**
`dotnet test tests/ClaudeExplorer.Core.Tests/ClaudeExplorer.Core.Tests.csproj --filter "FullyQualifiedName~ConfigCopyServiceTests"`
**Run all:** `dotnet test ClaudeExplorer.slnx`
**Build App only:** `dotnet build src/ClaudeExplorer.App/ClaudeExplorer.App.csproj -v quiet -clp:NoSummary`

**Branch:** `feat-per-screen-compare-sync` is already checked out — do NOT create branches; commit per task. Every commit body ends with:
`Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`

---

## File Structure

### Core (created / modified)
| File | Responsibility |
|------|----------------|
| `src/ClaudeExplorer.Core/Sync/ConfigCopyService.cs` (modify) | Add recursive **directory** copy/move for `Skills`; extend `CopyPlan` with `Writes` (multi-file) + `Removals` (multi); keep single-file fields for back-compat. |
| `src/ClaudeExplorer.Core/Mutation/ChangeLog.cs` (modify) | Add `Delete` to `ChangeKind`. |
| `src/ClaudeExplorer.Core/Mutation/Mutator.cs` (modify) | Add `ApplyDelete(ResolvedTarget, ts, desc)` (backup → delete → record `Delete`); `Undo` re-creates the deleted file for a `Delete` entry. |
| `src/ClaudeExplorer.Core/Mutation/SafeMutationService.cs` (modify) | Expose `ApplyDelete(...)` passthrough. |

### App (created / modified)
| File | Responsibility |
|------|----------------|
| `src/ClaudeExplorer.App/Compare/CompareModels.cs` (modify) | `CompareRow` gains `PathA/PathB/ContentA/ContentB`. |
| `src/ClaudeExplorer.App/Compare/CompareEntry.cs` (**new**) | `CompareEntry(Display, Path, Content)` — a comparable map value carrying display + resolved path + content. |
| `src/ClaudeExplorer.App/Compare/EnvironmentComparer.cs` (modify) | Build `CompareEntry`-valued maps; thread path/content into rows; rename `Agents`→`Subagents`. |
| `src/ClaudeExplorer.App/Compare/CompareContext.cs` (**new**) | Observable A/B endpoint selection, persistent across navigation; resolves endpoints from `EnvironmentService` + `ProjectRegistry`; computes a `Comparison` for a given category. |
| `src/ClaudeExplorer.App/Compare/CopyRequestBuilder.cs` (**new**) | Pure: `(category, row, srcEndpoint, tgtEndpoint, local) → CopyRequest` (paths per category, incl. Skills dir + Commands/Subagents file + MCP `.claude.json`/`.mcp.json`). |
| `src/ClaudeExplorer.App/Compare/CopyViewModel.cs` (modify) | Apply multi-write + multi-delete plans through safe-mutation as ONE undo group; drop the "move of files not supported" degrade. |
| `src/ClaudeExplorer.App/Components/CompareBar.razor` (**new**) | `A ▾ ⇄ B ▾` picker bar bound to `CompareContext`; "compare off" until B set. |
| `src/ClaudeExplorer.App/Components/DiffOverlay.razor` (**new**) | Per-row diff chip + selected-row diff detail + Copy/Move/Undo (Settings scope-target picker; Move warning; view-only mode). |
| `src/ClaudeExplorer.App/Screens/Memory/MemoryViewModel.cs` (**new**) | Lists global/project/nested CLAUDE.md for the active workspace. |
| `src/ClaudeExplorer.App/Screens/Memory/MemoryRows.cs` (**new**) | `MemoryRow` + pure `MemoryRowsMapper`. |
| `src/ClaudeExplorer.App/Pages/Memory.razor` (**new**) | Memory screen with the compare overlay. |
| `src/ClaudeExplorer.App/Pages/{Commands,Subagents,Skills,Hooks,Mcp,EffectiveConfig,Plugins,Dependencies}.razor` (modify) | Embed `CompareBar` + `DiffOverlay`. |
| `src/ClaudeExplorer.App/Components/LeftRail.razor` (modify) | Add **Memory** under Config Artifacts; delete the **Compare** nav entry + the now-empty Analyze label. |
| `src/ClaudeExplorer.App/Components/EnvironmentSelector.razor` (modify) | Add an "＋ Add project endpoint…" action + modal that calls `ProjectRegistry.Add`. |
| `src/ClaudeExplorer.App/Pages/Compare.razor` (**delete**) | Central compare page retired. |
| `src/ClaudeExplorer.App/Program.cs` (modify) | Register `CompareContext` (singleton). |
| `wwwroot/css/blueprint.css` (modify) | `.cmpbar`, `.diffchip`, `.diff-overlay` styles. |

### Tests (created / modified)
| File | Covers |
|------|--------|
| `tests/ClaudeExplorer.Core.Tests/Sync/ConfigCopyServiceTests.cs` (modify) | Recursive dir copy/move plan. |
| `tests/ClaudeExplorer.Core.Tests/Mutation/MutatorDeleteTests.cs` (**new**) | Backup → delete → undo re-creates. |
| `tests/ClaudeExplorer.App.Tests/Compare/EnvironmentComparerTests.cs` (modify) | `Subagents` rename + enriched row path/content. |
| `tests/ClaudeExplorer.App.Tests/Compare/CopyRequestBuilderTests.cs` (**new**) | Per-category request paths (incl. Agents↔Subagents). |
| `tests/ClaudeExplorer.App.Tests/Compare/CompareContextTests.cs` (**new**) | A/B selection, off-by-default, persistence, per-category comparison. |
| `tests/ClaudeExplorer.App.Tests/Compare/CopyViewModelTests.cs` (modify) | Dir copy/move applied & undone as one group; file move now removes source. |
| `tests/ClaudeExplorer.App.Tests/Screens/MemoryRowsTests.cs` (**new**) | Memory discovery (global/project/nested). |

---

## PHASE A — Core foundation (dir copy/move · undo-able delete)

### Task A1: `ChangeKind.Delete` + `Mutator.ApplyDelete` + undo

**Files:** Modify `src/ClaudeExplorer.Core/Mutation/ChangeLog.cs` (line 5); Modify `src/ClaudeExplorer.Core/Mutation/Mutator.cs` (add `ApplyDelete` after `ApplyEdit`; add a `Delete` case in `Undo`); Test (new) `tests/ClaudeExplorer.Core.Tests/Mutation/MutatorDeleteTests.cs`.

- [ ] **Step 1: Failing test** — create `tests/ClaudeExplorer.Core.Tests/Mutation/MutatorDeleteTests.cs`:
```csharp
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Mutation;

public class MutatorDeleteTests
{
    private const string Ts = "2026-06-08T00:00:00Z";

    private static (Mutator m, ChangeLog log, InMemoryFileSystem fs) Build()
    {
        var fs = new InMemoryFileSystem();
        var backups = new InMemoryFileSystem();
        var log = new ChangeLog();
        var m = new Mutator(fs, fs, new FileBackupStore(backups, backups, "/bk"), log, new FakeProcessRunner());
        return (m, log, fs);
    }

    [Fact]
    public void ApplyDelete_backs_up_then_removes_the_file()
    {
        var (m, log, fs) = Build();
        fs.AddFile("/proj/.claude/commands/deploy.md", "# deploy");

        var entry = m.ApplyDelete(new ResolvedTarget(ScopeKind.Project, "/proj/.claude/commands/deploy.md"), Ts, "Delete deploy");

        Assert.False(fs.FileExists("/proj/.claude/commands/deploy.md"));
        Assert.Equal(ChangeKind.Delete, entry.Kind);
        Assert.NotNull(entry.Backup);
        Assert.True(entry.Backup!.OriginalExisted);
        Assert.Single(log.Entries);
    }

    [Fact]
    public void Undo_of_a_delete_recreates_the_file_with_original_content()
    {
        var (m, log, fs) = Build();
        fs.AddFile("/proj/.claude/commands/deploy.md", "# deploy v1");

        var entry = m.ApplyDelete(new ResolvedTarget(ScopeKind.Project, "/proj/.claude/commands/deploy.md"), Ts, "Delete deploy");
        m.Undo(entry);

        Assert.True(fs.FileExists("/proj/.claude/commands/deploy.md"));
        Assert.Equal("# deploy v1", fs.ReadAllText("/proj/.claude/commands/deploy.md"));
        Assert.True(log.Entries.Single().IsUndone);
    }

    [Fact]
    public void ApplyDelete_of_a_missing_file_is_a_noop_delete_that_records_an_entry()
    {
        var (m, log, fs) = Build();

        var entry = m.ApplyDelete(new ResolvedTarget(ScopeKind.Project, "/proj/.claude/commands/ghost.md"), Ts, "Delete ghost");

        Assert.False(fs.FileExists("/proj/.claude/commands/ghost.md"));
        Assert.NotNull(entry.Backup);
        Assert.False(entry.Backup!.OriginalExisted);
        // Undo of a delete whose original never existed must not recreate anything.
        m.Undo(entry);
        Assert.False(fs.FileExists("/proj/.claude/commands/ghost.md"));
    }
}
```
- [ ] **Step 2: Run → FAIL** (`ApplyDelete` missing, `ChangeKind.Delete` missing). `dotnet test tests/ClaudeExplorer.Core.Tests/ClaudeExplorer.Core.Tests.csproj --filter "FullyQualifiedName~MutatorDeleteTests"`
- [ ] **Step 3a: Add `Delete` to the enum** — in `ChangeLog.cs` change line 5:
```csharp
public enum ChangeKind { Edit, Install, Uninstall, Delete }
```
- [ ] **Step 3b: Add `ApplyDelete`** — in `Mutator.cs`, immediately after the `ApplyEdit` method body (before `Install`), insert:
```csharp
    /// <summary>Delete a file safely: back it up (recording the absence if it does not exist), remove
    /// it, and record a reversible <see cref="ChangeKind.Delete"/> entry. <see cref="Undo"/> re-creates
    /// the original content (or, when the file never existed, leaves nothing behind).</summary>
    public ChangeLogEntry ApplyDelete(ResolvedTarget target, string timestamp, string? description = null)
    {
        var existed = _fs.FileExists(target.FilePath);
        var backup = _backups.Backup(target.FilePath, existed ? _fs.ReadAllText(target.FilePath) : null, existed, timestamp);
        _writer.Delete(target.FilePath);

        return _log.Record(new ChangeLogEntry(
            Id: "",
            Timestamp: timestamp,
            Kind: ChangeKind.Delete,
            Scope: target.Scope,
            FilePath: target.FilePath,
            Description: description ?? $"Delete {target.FilePath}",
            Backup: backup,
            UndoCommand: null,
            IsUndone: false));
    }
```
- [ ] **Step 3c: Undo a delete** — in `Mutator.Undo`, add a `Delete` case alongside the existing `Edit` case (the `switch (current.Kind)`). Add **after** the `case ChangeKind.Edit:` block:
```csharp
            case ChangeKind.Delete:
                if (current.Backup is null)
                    throw new MutationException($"Change '{current.Id}' has no backup to restore.");
                // A delete of a previously-existing file is reversed by re-creating it; a delete whose
                // original never existed has nothing to restore.
                if (current.Backup.OriginalExisted)
                    _writer.WriteAllText(current.Backup.OriginalPath, _backups.Read(current.Backup));
                break;
```
- [ ] **Step 4: Run → PASS.** filter `MutatorDeleteTests`.
- [ ] **Step 5: Commit** `feat(core): undo-able delete in the safe-mutation layer`.

---

### Task A2: `SafeMutationService.ApplyDelete` passthrough

**Files:** Modify `src/ClaudeExplorer.Core/Mutation/SafeMutationService.cs` (add a method after `ApplyEdit`); Test (new) `tests/ClaudeExplorer.Core.Tests/Mutation/SafeMutationServiceDeleteTests.cs`.

- [ ] **Step 1: Failing test** — create `tests/ClaudeExplorer.Core.Tests/Mutation/SafeMutationServiceDeleteTests.cs`:
```csharp
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Mutation;

public class SafeMutationServiceDeleteTests
{
    [Fact]
    public void ApplyDelete_removes_file_records_entry_and_undo_restores()
    {
        var fs = new InMemoryFileSystem();
        var backups = new InMemoryFileSystem();
        fs.AddFile("/proj/CLAUDE.md", "# rules");
        var svc = new SafeMutationService(fs, fs, new FileBackupStore(backups, backups, "/bk"), new FakeProcessRunner());

        var entry = svc.ApplyDelete(new ResolvedTarget(ScopeKind.Project, "/proj/CLAUDE.md"), "2026-06-08T00:00:00Z", "Delete CLAUDE.md");
        Assert.False(fs.FileExists("/proj/CLAUDE.md"));
        Assert.Single(svc.ChangeLog.Entries);

        svc.Undo(entry);
        Assert.True(fs.FileExists("/proj/CLAUDE.md"));
        Assert.Equal("# rules", fs.ReadAllText("/proj/CLAUDE.md"));
    }
}
```
- [ ] **Step 2: Run → FAIL.** filter `SafeMutationServiceDeleteTests`.
- [ ] **Step 3: Implement** — in `SafeMutationService.cs`, add after the `ApplyEdit` method:
```csharp
    /// <summary>Safely delete a file (backup → delete → change-log record). Reversible via
    /// <see cref="Undo"/>, which re-creates the original content.</summary>
    public ChangeLogEntry ApplyDelete(ResolvedTarget target, string timestamp, string? description = null)
        => _mutator.ApplyDelete(target, timestamp, description);
```
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5: Commit** `feat(core): SafeMutationService.ApplyDelete passthrough`.

---

### Task A3: `ConfigCopyService` — recursive directory copy/move (Skills) + multi-op `CopyPlan`

**Files:** Modify `src/ClaudeExplorer.Core/Sync/ConfigCopyService.cs` (extend `CopyPlan`; change `Skills` dispatch to a directory copy; keep `Commands/Subagents/Memory` as single-file copy); Test: extend `tests/ClaudeExplorer.Core.Tests/Sync/ConfigCopyServiceTests.cs`.

This task makes `CopyPlan` describe **N writes + N deletes** so a recursive Skills folder copy/move is one plan. The existing single-file fields (`TargetPath`, `NewTargetContent`, `TargetIsJson`, `SourceRemoval`) are **retained** and stay populated for the single-file/JSON categories (existing tests assert them). Two new lists default to "the single op derived from those fields", so the apply layer can always iterate `plan.Writes` / `plan.Removals` uniformly (Task C-side).

- [ ] **Step 1: Failing test** — add to `ConfigCopyServiceTests.cs` (the source for a skill is its directory; the request carries the **SKILL.md** path as `SourceFilePath` and the target SKILL.md as `TargetFilePath`, mirroring how the App resolves a skill's `Winner.FilePath`):
```csharp
    [Fact]
    public void Copy_skill_directory_enumerates_every_file_under_the_skill_folder()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/base/.claude/skills/lint/SKILL.md", "# lint");
        fs.AddFile("/base/.claude/skills/lint/scripts/run.sh", "echo hi");
        fs.AddFile("/base/.claude/skills/lint/references/notes.md", "notes");
        var svc = new ConfigCopyService(fs);

        var plan = svc.PlanCopy(new CopyRequest("Skills", "lint",
            SourceFilePath: "/base/.claude/skills/lint/SKILL.md",
            TargetFilePath: "/proj/.claude/skills/lint/SKILL.md"));

        // Three writes, each rebased under the target skill dir; no removals on copy.
        Assert.Equal(3, plan.Writes.Count);
        Assert.Contains(plan.Writes, w => w.Path == "/proj/.claude/skills/lint/SKILL.md" && w.Content == "# lint");
        Assert.Contains(plan.Writes, w => w.Path == "/proj/.claude/skills/lint/scripts/run.sh" && w.Content == "echo hi");
        Assert.Contains(plan.Writes, w => w.Path == "/proj/.claude/skills/lint/references/notes.md" && w.Content == "notes");
        Assert.Empty(plan.Removals);
        Assert.False(plan.Writes[0].IsJson);
    }

    [Fact]
    public void Move_skill_directory_removes_every_source_file()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/base/.claude/skills/lint/SKILL.md", "# lint");
        fs.AddFile("/base/.claude/skills/lint/scripts/run.sh", "echo hi");
        var svc = new ConfigCopyService(fs);

        var plan = svc.PlanMove(new CopyRequest("Skills", "lint",
            SourceFilePath: "/base/.claude/skills/lint/SKILL.md",
            TargetFilePath: "/proj/.claude/skills/lint/SKILL.md"));

        Assert.Equal(2, plan.Writes.Count);
        Assert.Equal(2, plan.Removals.Count);
        Assert.Contains(plan.Removals, r => r.Path == "/base/.claude/skills/lint/SKILL.md" && r.IsDelete);
        Assert.Contains(plan.Removals, r => r.Path == "/base/.claude/skills/lint/scripts/run.sh" && r.IsDelete);
    }

    [Fact]
    public void Copy_command_file_exposes_a_single_write_and_keeps_single_file_fields()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/base/.claude/commands/deploy.md", "# deploy");
        var svc = new ConfigCopyService(fs);

        var plan = svc.PlanCopy(new CopyRequest("Commands", "deploy",
            SourceFilePath: "/base/.claude/commands/deploy.md",
            TargetFilePath: "/proj/.claude/commands/deploy.md"));

        Assert.Equal("/proj/.claude/commands/deploy.md", plan.TargetPath);
        Assert.Equal("# deploy", plan.NewTargetContent);
        Assert.Single(plan.Writes);
        Assert.Equal("/proj/.claude/commands/deploy.md", plan.Writes[0].Path);
    }

    [Fact]
    public void Move_command_file_removes_source_as_a_delete()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/base/.claude/commands/deploy.md", "# deploy");
        var svc = new ConfigCopyService(fs);

        var plan = svc.PlanMove(new CopyRequest("Commands", "deploy",
            SourceFilePath: "/base/.claude/commands/deploy.md",
            TargetFilePath: "/proj/.claude/commands/deploy.md"));

        var removal = Assert.Single(plan.Removals);
        Assert.Equal("/base/.claude/commands/deploy.md", removal.Path);
        Assert.True(removal.IsDelete);
    }
```
(Keep the three existing tests in this file unchanged — they assert `plan.TargetPath` / `plan.NewTargetContent` / `plan.SourceRemoval`, which the implementation preserves.)
- [ ] **Step 2: Run → FAIL.** filter `ConfigCopyServiceTests`.
- [ ] **Step 3a: Extend the records** — in `ConfigCopyService.cs`, replace the `SourceRemoval` + `CopyPlan` record declarations (the `// ── Records ──` block) with:
```csharp
/// <summary>One file to write at the target (content + whether it should be JSON-validated).</summary>
public sealed record CopyWrite(string Path, string Content, bool IsJson);

/// <summary>The source edit that removes/clears the item (for Move operations). When
/// <see cref="IsDelete"/> is true the whole file is deleted; otherwise <see cref="NewContent"/>
/// replaces the source file (e.g. a settings key or hook group spliced out).</summary>
public sealed record SourceRemoval(string Path, string NewContent, bool IsDelete = false);

/// <summary>The resulting plan. <see cref="Writes"/> lists every target file to write (one for
/// scalar/JSON/single-file categories, many for a recursive Skills folder). <see cref="Removals"/>
/// lists the source edits/deletes for a Move. <see cref="TargetPath"/>/<see cref="NewTargetContent"/>/
/// <see cref="TargetIsJson"/>/<see cref="SourceRemoval"/> mirror the FIRST write/removal so existing
/// single-file/JSON callers keep working.</summary>
public sealed record CopyPlan(
    IReadOnlyList<CopyWrite> Writes,
    IReadOnlyList<SourceRemoval> Removals)
{
    public string TargetPath => Writes.Count > 0 ? Writes[0].Path : "";
    public string NewTargetContent => Writes.Count > 0 ? Writes[0].Content : "";
    public bool TargetIsJson => Writes.Count > 0 && Writes[0].IsJson;
    public SourceRemoval? SourceRemoval => Removals.Count > 0 ? Removals[0] : null;
}
```
- [ ] **Step 3b: Update the JSON/settings/MCP/hooks branches to the new shape.** These four branches currently build a `new CopyPlan(targetPath, newTarget, TargetIsJson: true, removal)`. Change each to build the new list-based plan. Replace the **return** of each of `CopySettings`, `CopyMcp`, `CopyHooks` with the list form. Concretely:

In `CopySettings` (its return — it currently ends building `newTarget` and `removal`):
```csharp
        return new CopyPlan(
            new[] { new CopyWrite(targetPath, newTarget, IsJson: true) },
            removal is null ? Array.Empty<SourceRemoval>() : new[] { removal });
```
In `CopyMcp`, change the final `return new CopyPlan(targetPath, newTarget, TargetIsJson: true, removal);` to:
```csharp
        return new CopyPlan(
            new[] { new CopyWrite(targetPath, newTarget, IsJson: true) },
            removal is null ? Array.Empty<SourceRemoval>() : new[] { removal });
```
In `CopyHooks`, change the final `return new CopyPlan(targetPath, newTarget, TargetIsJson: true, removal);` to the identical list form (same three lines).
> The `SourceRemoval` constructions inside those branches already pass `(path, newContent)` — the new record's `IsDelete` defaults to `false`, so JSON source-removals stay "replace content", exactly as before.
- [ ] **Step 3c: Split the file-copy dispatch — Skills = directory, others = single file.** Replace the `Dispatch` mapping line `"Memory" or "Commands" or "Skills" or "Subagents" => CopyFile(req, move),` with:
```csharp
            "Memory" or "Commands" or "Subagents" => CopyFile(req, move),
            "Skills"    => CopySkillDirectory(req, move),
```
Then replace the existing `CopyFile` method body with the single-file list form and add the new directory method:
```csharp
    // ── Memory / Commands / Subagents (single-file copy) ─────────────────────

    private CopyPlan CopyFile(CopyRequest req, bool move)
    {
        var sourcePath = req.SourceFilePath!;
        var targetPath = req.TargetFilePath!;
        var content    = ReadText(sourcePath, "");

        var writes = new[] { new CopyWrite(targetPath, content, IsJson: false) };
        var removals = move ? new[] { new SourceRemoval(sourcePath, "", IsDelete: true) } : Array.Empty<SourceRemoval>();
        return new CopyPlan(writes, removals);
    }

    // ── Skills (recursive directory copy/move) ───────────────────────────────

    /// <summary>Copy a whole skill folder. <c>SourceFilePath</c>/<c>TargetFilePath</c> are the
    /// source/target <c>SKILL.md</c> paths; the skill DIRECTORY is each file's parent. Every file under
    /// the source dir (recursively) becomes a write rebased under the target dir; Move adds a delete per
    /// source file.</summary>
    private CopyPlan CopySkillDirectory(CopyRequest req, bool move)
    {
        var sourceDir = DirOf(req.SourceFilePath!);
        var targetDir = DirOf(req.TargetFilePath!);

        var files = _fs.GetFiles(sourceDir, "*", recurse: true);
        if (files.Count == 0)
            throw new MutationException($"Skill folder is empty or missing: {sourceDir}");

        var writes = new List<CopyWrite>();
        var removals = new List<SourceRemoval>();
        foreach (var file in files)
        {
            var rel = file.Substring(sourceDir.Length).TrimStart('/');
            var targetFile = $"{targetDir}/{rel}";
            writes.Add(new CopyWrite(targetFile, _fs.ReadAllText(file), IsJson: false));
            if (move) removals.Add(new SourceRemoval(file, "", IsDelete: true));
        }
        return new CopyPlan(writes, removals);
    }

    private static string DirOf(string filePath)
    {
        var p = filePath.Replace('\\', '/');
        var i = p.LastIndexOf('/');
        return i >= 0 ? p.Substring(0, i) : p;
    }
```
> `GetFiles(dir, "*", recurse:true)` returns forward-slash paths under `sourceDir`; the `rel` rebasing preserves nested `scripts/`, `references/`, etc.
- [ ] **Step 4: Run → PASS** (new dir tests + the three pre-existing single-file/settings tests, which still read `plan.TargetPath`/`plan.NewTargetContent`/`plan.SourceRemoval`).
- [ ] **Step 5: Commit** `feat(core): recursive Skills dir copy/move + multi-op CopyPlan`.

---

## PHASE B — Enriched diff rows (path + content) · Subagents naming

### Task B1: `CompareEntry` + enrich `CompareRow` with path/content

**Files:** Create `src/ClaudeExplorer.App/Compare/CompareEntry.cs`; Modify `src/ClaudeExplorer.App/Compare/CompareModels.cs` (lines 6 — `CompareRow`); Modify `src/ClaudeExplorer.App/Compare/EnvironmentComparer.cs` (maps + `BuildCategory` + rename `Agents`→`Subagents`); Test: extend `tests/ClaudeExplorer.App.Tests/Compare/EnvironmentComparerTests.cs`.

- [ ] **Step 1: Failing test** — edit `EnvironmentComparerTests.cs`:
  - Update the `Art` helper to take a file path and content so rows can carry them:
```csharp
    private static ResolvedArtifact Art(ArtifactKind kind, string name, string? summary, string path = "") =>
        new(new DiscoveredArtifact(kind, name, summary, new ArtifactSource(ArtifactSourceKind.User),
            string.IsNullOrEmpty(path) ? $"/{name}" : path),
            Array.Empty<DiscoveredArtifact>());
```
  - In `Produces_seven_categories`, change the expected category names array `"Agents"` → `"Subagents"`:
```csharp
        Assert.Equal(new[] { "Settings", "Commands", "Skills", "Subagents", "MCP", "Memory", "Plugins", "Dependencies" },
            c.Categories.Select(x => x.Name).ToArray());
```
  - In `Commands_skills_agents_compare_by_name_and_summary`, change the assertion category `Cat(c, "Agents")` → `Cat(c, "Subagents")`.
  - Add two new tests:
```csharp
    [Fact]
    public void Command_row_carries_each_sides_resolved_file_path_and_content()
    {
        var a = Snap(artifacts: new[] { Art(ArtifactKind.Command, "deploy", "v1", "/a/.claude/commands/deploy.md") });
        var b = Snap(artifacts: new[] { Art(ArtifactKind.Command, "deploy", "v2", "/b/.claude/commands/deploy.md") });

        var row = EnvironmentComparer.Compare(a, b).Find("Commands")!.Rows.Single(r => r.Key == "deploy");

        Assert.Equal("/a/.claude/commands/deploy.md", row.PathA);
        Assert.Equal("/b/.claude/commands/deploy.md", row.PathB);
    }

    [Fact]
    public void OnlyA_command_row_has_null_path_on_the_B_side()
    {
        var a = Snap(artifacts: new[] { Art(ArtifactKind.Command, "deploy", "v1", "/a/.claude/commands/deploy.md") });
        var b = Snap();

        var row = EnvironmentComparer.Compare(a, b).Find("Commands")!.Rows.Single(r => r.Key == "deploy");

        Assert.Equal(DiffStatus.OnlyA, row.Status);
        Assert.Equal("/a/.claude/commands/deploy.md", row.PathA);
        Assert.Null(row.PathB);
    }
```
- [ ] **Step 2: Run → FAIL.** filter `EnvironmentComparerTests`.
- [ ] **Step 3a: `CompareEntry`** — create `src/ClaudeExplorer.App/Compare/CompareEntry.cs`:
```csharp
namespace ClaudeExplorer.App.Compare;

/// <summary>A comparable map value: the canonical <see cref="Display"/> string (used for diff
/// classification AND shown in the table) plus the resolved <see cref="Path"/> on disk and the
/// file <see cref="Content"/> when applicable. Path/content are what a copy/move needs to build a
/// <c>CopyRequest</c>; they are empty for value-only categories (Settings/MCP/Plugins/Deps).</summary>
public sealed record CompareEntry(string Display, string Path = "", string Content = "");
```
- [ ] **Step 3b: Enrich `CompareRow`** — in `CompareModels.cs` replace line 6:
```csharp
public sealed record CompareRow(
    string Key, DiffStatus Status, string? ValueA, string? ValueB,
    string? PathA = null, string? PathB = null, string? ContentA = null, string? ContentB = null);
```
- [ ] **Step 3c: Comparer threads path/content + renames Subagents** — rewrite `EnvironmentComparer.cs` to build `CompareEntry`-valued maps and carry the side's path/content into each row. Replace the whole file with:
```csharp
using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.App.Compare;

/// <summary>Pure diff of two environment snapshots into per-category rows. No IO — tested by
/// constructing Core records directly. Map values are <see cref="CompareEntry"/> so each row keeps
/// the resolved on-disk path (+ content where relevant) for copy/move.</summary>
public static class EnvironmentComparer
{
    public static EnvironmentComparison Compare(EnvironmentSnapshot a, EnvironmentSnapshot b)
        => new(new List<CompareCategory>
        {
            BuildCategory("Settings", SettingsMap(a), SettingsMap(b)),
            BuildCategory("Commands", ArtifactMap(a, ArtifactKind.Command), ArtifactMap(b, ArtifactKind.Command)),
            BuildCategory("Skills", ArtifactMap(a, ArtifactKind.Skill), ArtifactMap(b, ArtifactKind.Skill)),
            BuildCategory("Subagents", ArtifactMap(a, ArtifactKind.Subagent), ArtifactMap(b, ArtifactKind.Subagent)),
            BuildCategory("MCP", McpMap(a), McpMap(b)),
            BuildCategory("Memory", MemoryMap(a), MemoryMap(b)),
            BuildCategory("Plugins", PluginMap(a), PluginMap(b), viewOnly: true),
            BuildCategory("Dependencies", DepMap(a), DepMap(b), viewOnly: true),
        });

    private static CompareCategory BuildCategory(
        string name, IReadOnlyDictionary<string, CompareEntry> a, IReadOnlyDictionary<string, CompareEntry> b,
        bool viewOnly = false)
    {
        var rows = new List<CompareRow>();
        foreach (var key in a.Keys.Union(b.Keys).OrderBy(k => k, StringComparer.Ordinal))
        {
            var hasA = a.TryGetValue(key, out var ea);
            var hasB = b.TryGetValue(key, out var eb);
            var status = (hasA, hasB) switch
            {
                (true, true) => ea!.Display == eb!.Display ? DiffStatus.Same : DiffStatus.Differs,
                (true, false) => DiffStatus.OnlyA,
                _ => DiffStatus.OnlyB,
            };
            rows.Add(new CompareRow(
                key, status,
                hasA ? ea!.Display : null, hasB ? eb!.Display : null,
                hasA ? NullIfEmpty(ea!.Path) : null, hasB ? NullIfEmpty(eb!.Path) : null,
                hasA ? NullIfEmpty(ea!.Content) : null, hasB ? NullIfEmpty(eb!.Content) : null));
        }
        return new CompareCategory(name, rows, viewOnly);
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;

    private static Dictionary<string, CompareEntry> SettingsMap(EnvironmentSnapshot s)
        => s.Settings.ToDictionary(x => x.Key, x => new CompareEntry(Canonical(x.Value)), StringComparer.Ordinal);

    private static Dictionary<string, CompareEntry> ArtifactMap(EnvironmentSnapshot s, ArtifactKind kind)
        => s.Artifacts.OfKind(kind).ToDictionary(
            a => a.Winner.Name,
            a => new CompareEntry(a.Winner.Summary ?? "", a.Winner.FilePath),
            StringComparer.Ordinal);

    private static Dictionary<string, CompareEntry> McpMap(EnvironmentSnapshot s)
        => s.Mcp.GroupBy(m => m.Name, StringComparer.Ordinal)
               .ToDictionary(g => g.Key,
                   g => new CompareEntry($"{g.First().Command} {string.Join(" ", g.First().Args)}".Trim()),
                   StringComparer.Ordinal);

    private static Dictionary<string, CompareEntry> PluginMap(EnvironmentSnapshot s)
        => s.Plugins.Distinct(StringComparer.Ordinal).ToDictionary(p => p, _ => new CompareEntry("installed"), StringComparer.Ordinal);

    private static Dictionary<string, CompareEntry> DepMap(EnvironmentSnapshot s)
        => s.Dependencies.Results.GroupBy(r => r.Ref.Name, StringComparer.Ordinal)
               .ToDictionary(g => g.Key, g => new CompareEntry(g.First().Status.Kind.ToString()), StringComparer.Ordinal);

    private static Dictionary<string, CompareEntry> MemoryMap(EnvironmentSnapshot s)
        => s.Memory.ToDictionary(kv => kv.Key, kv => new CompareEntry(Descriptor(kv.Value), Content: kv.Value), StringComparer.Ordinal);

    private static string Descriptor(string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))[..8];
        return $"present · {bytes.Length} B · {hash}";
    }

    /// <summary>Canonical comparable form of a setting value; arrays compare as sorted sets.</summary>
    private static string Canonical(JsonNode? node)
    {
        if (node is null) return "";
        if (node is JsonArray arr)
            return "[" + string.Join(",", arr.Select(e => e?.ToJsonString() ?? "null").OrderBy(x => x, StringComparer.Ordinal)) + "]";
        return node.ToJsonString();
    }
}
```
> Memory rows now carry full `Content` so the content-diff in `DiffOverlay` shows the file body. Artifact rows carry the `Winner.FilePath` (for skills that is the SKILL.md path — the App resolves the skill dir from it). Settings/MCP/Plugins/Deps remain value-only (no path/content), matching the per-screen behavior table.
- [ ] **Step 4: Run → PASS** (incl. the existing comparer tests that now use `"Subagents"`).
- [ ] **Step 5: Commit** `feat(app): enrich compare rows with path/content; rename Agents→Subagents`.

---

### Task B2: `CopyRequestBuilder` — per-category source/target paths

**Files:** Create `src/ClaudeExplorer.App/Compare/CopyRequestBuilder.cs`; Test (new) `tests/ClaudeExplorer.App.Tests/Compare/CopyRequestBuilderTests.cs`. (Extracts and generalizes the `BuildRequest` logic from the old `Compare.razor`, and adds Commands/Subagents/Skills.)

- [ ] **Step 1: Failing test** — create `tests/ClaudeExplorer.App.Tests/Compare/CopyRequestBuilderTests.cs`:
```csharp
using ClaudeExplorer.App.Compare;

namespace ClaudeExplorer.App.Tests.Compare;

public class CopyRequestBuilderTests
{
    private static CompareEndpoint Base => CompareEndpoint.Base("win", "Base · Windows", "C:/Users/me");
    private static CompareEndpoint Proj => CompareEndpoint.Project("p1", "Project A", "D:/work/a");

    [Fact]
    public void Settings_targets_shared_settings_json_by_default()
    {
        var row = new CompareRow("model", DiffStatus.Differs, "\"opus\"", "\"sonnet\"");
        var req = CopyRequestBuilder.Build("Settings", row, src: Base, tgt: Proj, local: false);

        Assert.Equal("Settings", req.Category);
        Assert.Equal("C:/Users/me/.claude/settings.json", req.SourceSettingsPath);
        Assert.Equal("D:/work/a/.claude/settings.json", req.TargetSettingsPath);
    }

    [Fact]
    public void Settings_targets_local_settings_when_local_is_true()
    {
        var row = new CompareRow("model", DiffStatus.Differs, "\"opus\"", "\"sonnet\"");
        var req = CopyRequestBuilder.Build("Settings", row, src: Base, tgt: Proj, local: true);
        Assert.Equal("D:/work/a/.claude/settings.local.json", req.TargetSettingsPath);
    }

    [Fact]
    public void Memory_base_resolves_under_dot_claude_project_resolves_at_root()
    {
        var row = new CompareRow("CLAUDE.md", DiffStatus.Differs, "a", "b");
        var req = CopyRequestBuilder.Build("Memory", row, src: Base, tgt: Proj, local: false);
        Assert.Equal("C:/Users/me/.claude/CLAUDE.md", req.SourceFilePath);
        Assert.Equal("D:/work/a/CLAUDE.md", req.TargetFilePath);
    }

    [Fact]
    public void Mcp_base_uses_dot_claude_json_project_uses_dot_mcp_json()
    {
        var row = new CompareRow("ctx7", DiffStatus.Differs, "uvx ctx7", "npx ctx7");
        var req = CopyRequestBuilder.Build("MCP", row, src: Base, tgt: Proj, local: false);
        Assert.Equal("C:/Users/me/.claude.json", req.SourceMcpPath);
        Assert.Equal("D:/work/a/.mcp.json", req.TargetMcpPath);
    }

    [Fact]
    public void Commands_uses_the_rows_resolved_source_path_and_rebases_to_the_target_commands_dir()
    {
        var row = new CompareRow("deploy", DiffStatus.OnlyA, "v1", null,
            PathA: "C:/Users/me/.claude/commands/deploy.md");
        var req = CopyRequestBuilder.Build("Commands", row, src: Base, tgt: Proj, local: false);

        Assert.Equal("C:/Users/me/.claude/commands/deploy.md", req.SourceFilePath);
        Assert.Equal("D:/work/a/.claude/commands/deploy.md", req.TargetFilePath);
    }

    [Fact]
    public void Subagents_rebases_into_the_target_agents_dir()
    {
        var row = new CompareRow("review", DiffStatus.OnlyA, "x", null,
            PathA: "C:/Users/me/.claude/agents/review.md");
        var req = CopyRequestBuilder.Build("Subagents", row, src: Base, tgt: Proj, local: false);

        Assert.Equal("Subagents", req.Category);
        Assert.Equal("C:/Users/me/.claude/agents/review.md", req.SourceFilePath);
        Assert.Equal("D:/work/a/.claude/agents/review.md", req.TargetFilePath);
    }

    [Fact]
    public void Skills_carries_the_source_skill_md_and_target_skill_md()
    {
        var row = new CompareRow("lint", DiffStatus.OnlyA, "x", null,
            PathA: "C:/Users/me/.claude/skills/lint/SKILL.md");
        var req = CopyRequestBuilder.Build("Skills", row, src: Base, tgt: Proj, local: false);

        Assert.Equal("Skills", req.Category);
        Assert.Equal("C:/Users/me/.claude/skills/lint/SKILL.md", req.SourceFilePath);
        Assert.Equal("D:/work/a/.claude/skills/lint/SKILL.md", req.TargetFilePath);
    }
}
```
- [ ] **Step 2: Run → FAIL.** filter `CopyRequestBuilderTests`.
- [ ] **Step 3: Implement** — create `src/ClaudeExplorer.App/Compare/CopyRequestBuilder.cs`:
```csharp
using ClaudeExplorer.Core.Sync;

namespace ClaudeExplorer.App.Compare;

/// <summary>Pure builder that turns a diff row + the source/target endpoints into a Core
/// <see cref="CopyRequest"/>. Encapsulates the per-category on-disk layout: a Base reads/writes
/// under its <c>~/.claude</c>; a Project reads/writes under its folder (<c>.claude/</c> for
/// settings/commands/skills/agents, <c>.mcp.json</c> for MCP, root for CLAUDE.md). For file-based
/// categories (Commands/Subagents/Skills) the SOURCE path comes from the row's resolved path; the
/// TARGET is the same file/skill name rebased into the target endpoint's matching dir.</summary>
public static class CopyRequestBuilder
{
    public static CopyRequest Build(string category, CompareRow row, CompareEndpoint src, CompareEndpoint tgt, bool local)
    {
        var key = row.Key;
        // The overlay orients (src, tgt) for the chosen direction and sets row.SourcePath to the
        // source side's resolved path; file categories read that, value categories ignore it.
        switch (category)
        {
            case "Settings":
            {
                var srcPath = $"{ClaudeRoot(src)}/settings.json";
                var tgtPath = local ? $"{ClaudeRoot(tgt)}/settings.local.json" : $"{ClaudeRoot(tgt)}/settings.json";
                return new CopyRequest("Settings", key, SourceSettingsPath: srcPath, TargetSettingsPath: tgtPath);
            }
            case "Memory":
            {
                var srcPath = MemoryPath(src, key);
                var tgtPath = MemoryPath(tgt, key);
                return new CopyRequest("Memory", key, SourceFilePath: srcPath, TargetFilePath: tgtPath);
            }
            case "MCP":
            {
                var srcPath = McpPath(src);
                var tgtPath = McpPath(tgt);
                return new CopyRequest("MCP", key, SourceMcpPath: srcPath, TargetMcpPath: tgtPath);
            }
            case "Commands":
            case "Subagents":
            {
                var sub = category == "Commands" ? "commands" : "agents";
                var srcPath = row.SourcePath ?? $"{ClaudeRoot(src)}/{sub}/{key}.md";
                var tgtPath = $"{ClaudeRoot(tgt)}/{sub}/{key}.md";
                return new CopyRequest(category, key, SourceFilePath: srcPath, TargetFilePath: tgtPath);
            }
            case "Skills":
            {
                var srcPath = row.SourcePath ?? $"{ClaudeRoot(src)}/skills/{key}/SKILL.md";
                var tgtPath = $"{ClaudeRoot(tgt)}/skills/{key}/SKILL.md";
                return new CopyRequest("Skills", key, SourceFilePath: srcPath, TargetFilePath: tgtPath);
            }
            default:
                throw new System.InvalidOperationException($"Copy is not supported for category '{category}'.");
        }
    }

    /// <summary>The <c>.claude</c> dir of an endpoint (a Base's <c>~/.claude</c> or a Project's
    /// <c>&lt;projectDir&gt;/.claude</c>).</summary>
    private static string ClaudeRoot(CompareEndpoint e) =>
        e.Kind == EndpointKind.Base ? $"{e.UserDir}/.claude" : $"{e.ProjectDir}/.claude";

    private static string MemoryPath(CompareEndpoint e, string fileName) =>
        e.Kind == EndpointKind.Base ? $"{e.UserDir}/.claude/{fileName}" : $"{e.ProjectDir}/{fileName}";

    private static string McpPath(CompareEndpoint e) =>
        e.Kind == EndpointKind.Base ? $"{e.UserDir}/.claude.json" : $"{e.ProjectDir}/.mcp.json";
}
```
> **Source-path resolution:** `Build` reads the source side's resolved path from `row.SourcePath`, which the overlay sets per direction (Task C3). Add `SourcePath` as a trailing optional positional param on `CompareRow`. In `CompareModels.cs` replace the `CompareRow` declaration (the one from B1) with:
```csharp
public sealed record CompareRow(
    string Key, DiffStatus Status, string? ValueA, string? ValueB,
    string? PathA = null, string? PathB = null, string? ContentA = null, string? ContentB = null,
    string? SourcePath = null);
```
The B2 tests construct `CompareRow` with `PathA:` named args and no `SourcePath`, so `row.SourcePath` is null and `Build` falls back to the conventional `ClaudeRoot(src)/<sub>/<key>.md` path — exactly what those assertions expect for `src == Base`.
- [ ] **Step 4: Run → PASS.** filter `CopyRequestBuilderTests`.
- [ ] **Step 5: Commit** `feat(app): CopyRequestBuilder — per-category source/target paths`.

---

## PHASE C — Shared CompareContext · CopyViewModel · overlay component

### Task C1: `CompareContext` — persistent observable A/B + per-category comparison

**Files:** Create `src/ClaudeExplorer.App/Compare/CompareContext.cs`; Test (new) `tests/ClaudeExplorer.App.Tests/Compare/CompareContextTests.cs`.

- [ ] **Step 1: Failing test** — create `tests/ClaudeExplorer.App.Tests/Compare/CompareContextTests.cs`:
```csharp
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
```
- [ ] **Step 2: Run → FAIL.** filter `CompareContextTests`.
- [ ] **Step 3: Implement** — create `src/ClaudeExplorer.App/Compare/CompareContext.cs`:
```csharp
using ClaudeExplorer.App.Environments;

namespace ClaudeExplorer.App.Compare;

/// <summary>
/// App-wide, navigation-persistent selection of a compare pair (A and optional B). Lives as a DI
/// singleton so the A/B chosen on one artifact screen still applies on the next. Compare is OFF until
/// <see cref="EndpointB"/> is set; A defaults to the active environment's base. Screens read
/// <see cref="Comparison"/> for their own category and subscribe to <see cref="Changed"/>.
/// </summary>
public sealed class CompareContext
{
    private readonly EnvironmentService _environments;
    private readonly ProjectRegistry _projects;
    private readonly IEnvironmentCompareDataSource _source;

    private string? _aId;
    private string? _bId;
    private EnvironmentComparison? _comparison;

    public event Action? Changed;

    public CompareContext(EnvironmentService environments, ProjectRegistry projects, IEnvironmentCompareDataSource source)
    {
        _environments = environments;
        _projects = projects;
        _source = source;
        _environments.Changed += OnEndpointsChanged;
        _projects.Changed += OnEndpointsChanged;
    }

    /// <summary>All selectable endpoints: every environment base + every registered project.</summary>
    public IReadOnlyList<CompareEndpoint> Endpoints =>
        _environments.Environments.Select(e => CompareEndpoint.Base(e.Id, e.Name, e.UserDir))
            .Concat(_projects.All.Select(p => CompareEndpoint.Project(p.Id, p.Name, p.ProjectDir)))
            .ToList();

    public CompareEndpoint? EndpointA => Resolve(_aId) ?? DefaultA();
    public CompareEndpoint? EndpointB => Resolve(_bId);

    public bool IsComparing => EndpointA is not null && EndpointB is not null;

    public void SetA(string id) { _aId = id; Rebuild(); }

    public void SetB(string id) { _bId = id; Rebuild(); }

    public void ClearB() { _bId = null; _comparison = null; Changed?.Invoke(); }

    public void Swap()
    {
        if (EndpointA is null || EndpointB is null) return;
        (_aId, _bId) = (EndpointB.Id, EndpointA.Id);
        Rebuild();
    }

    /// <summary>The diff for one category (e.g. "Commands"), or null when compare is off.</summary>
    public CompareCategory? Comparison(string category) => _comparison?.Find(category);

    private CompareEndpoint? DefaultA()
    {
        var active = _environments.Active;
        return CompareEndpoint.Base(active.Id, active.Name, active.UserDir);
    }

    private CompareEndpoint? Resolve(string? id) =>
        id is null ? null : Endpoints.FirstOrDefault(e => e.Id == id);

    private void Rebuild()
    {
        var a = EndpointA;
        var b = EndpointB;
        _comparison = (a is not null && b is not null)
            ? EnvironmentComparer.Compare(_source.Snapshot(a), _source.Snapshot(b))
            : null;
        Changed?.Invoke();
    }

    // Endpoints changed (env added/removed, project added): if a selected id disappeared, drop it.
    private void OnEndpointsChanged()
    {
        if (_aId is not null && Resolve(_aId) is null) _aId = null;
        if (_bId is not null && Resolve(_bId) is null) _bId = null;
        Rebuild();
    }
}
```
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5: Commit** `feat(app): CompareContext — persistent A/B selection + per-category diff`.

---

### Task C2: `CopyViewModel` — multi-write/delete apply as one undo group; drop the move-degrade

**Files:** Modify `src/ClaudeExplorer.App/Compare/CopyViewModel.cs` (rewrite the apply internals); Modify `tests/ClaudeExplorer.App.Tests/Compare/CopyViewModelTests.cs` (replace the "file move not supported" test with a real file-move + add dir copy/move tests).

- [ ] **Step 1: Failing test** — in `CopyViewModelTests.cs`:
  - **Replace** `Move_file_category_copies_but_reports_unsupported_error` with a real file move:
```csharp
    [Fact]
    public void Move_memory_file_copies_target_and_deletes_source()
    {
        var (svc, fs, vm) = Build();
        fs.AddFile("/base/.claude/CLAUDE.md", "# notes");

        vm.Move(new CopyRequest("Memory", "CLAUDE.md",
            SourceFilePath: "/base/.claude/CLAUDE.md",
            TargetFilePath: "/proj/.claude/CLAUDE.md"));

        Assert.Null(vm.Error);
        Assert.True(fs.FileExists("/proj/.claude/CLAUDE.md"));
        Assert.Equal("# notes", fs.ReadAllText("/proj/.claude/CLAUDE.md"));
        Assert.False(fs.FileExists("/base/.claude/CLAUDE.md")); // source deleted
        Assert.Equal(2, svc.ChangeLog.Entries.Count);            // write + delete
    }

    [Fact]
    public void Move_memory_file_undo_restores_source_and_reverts_target()
    {
        var (_, fs, vm) = Build();
        fs.AddFile("/base/.claude/CLAUDE.md", "# notes");

        vm.Move(new CopyRequest("Memory", "CLAUDE.md",
            SourceFilePath: "/base/.claude/CLAUDE.md",
            TargetFilePath: "/proj/.claude/CLAUDE.md"));
        Assert.False(fs.FileExists("/base/.claude/CLAUDE.md"));

        vm.Undo();

        Assert.Null(vm.Error);
        Assert.True(fs.FileExists("/base/.claude/CLAUDE.md"));   // delete undone
        Assert.Equal("# notes", fs.ReadAllText("/base/.claude/CLAUDE.md"));
        Assert.False(fs.FileExists("/proj/.claude/CLAUDE.md"));  // target write undone
    }

    [Fact]
    public void Copy_skill_directory_writes_every_file_and_logs_each_write()
    {
        var (svc, fs, vm) = Build();
        fs.AddFile("/base/.claude/skills/lint/SKILL.md", "# lint");
        fs.AddFile("/base/.claude/skills/lint/scripts/run.sh", "echo hi");

        vm.Copy(new CopyRequest("Skills", "lint",
            SourceFilePath: "/base/.claude/skills/lint/SKILL.md",
            TargetFilePath: "/proj/.claude/skills/lint/SKILL.md"));

        Assert.Null(vm.Error);
        Assert.Equal("# lint", fs.ReadAllText("/proj/.claude/skills/lint/SKILL.md"));
        Assert.Equal("echo hi", fs.ReadAllText("/proj/.claude/skills/lint/scripts/run.sh"));
        Assert.Equal(2, svc.ChangeLog.Entries.Count);
    }

    [Fact]
    public void Move_skill_directory_undo_restores_all_source_files_and_removes_target()
    {
        var (_, fs, vm) = Build();
        fs.AddFile("/base/.claude/skills/lint/SKILL.md", "# lint");
        fs.AddFile("/base/.claude/skills/lint/scripts/run.sh", "echo hi");

        vm.Move(new CopyRequest("Skills", "lint",
            SourceFilePath: "/base/.claude/skills/lint/SKILL.md",
            TargetFilePath: "/proj/.claude/skills/lint/SKILL.md"));
        Assert.False(fs.FileExists("/base/.claude/skills/lint/SKILL.md"));
        Assert.True(fs.FileExists("/proj/.claude/skills/lint/SKILL.md"));

        vm.Undo();

        Assert.Null(vm.Error);
        Assert.True(fs.FileExists("/base/.claude/skills/lint/SKILL.md"));
        Assert.True(fs.FileExists("/base/.claude/skills/lint/scripts/run.sh"));
        Assert.False(fs.FileExists("/proj/.claude/skills/lint/SKILL.md"));
        Assert.False(fs.FileExists("/proj/.claude/skills/lint/scripts/run.sh"));
    }
```
  - Keep the Settings copy/move/undo tests and the unknown-category test unchanged (they still hold).
- [ ] **Step 2: Run → FAIL.** filter `CopyViewModelTests`.
- [ ] **Step 3: Implement** — replace the whole `src/ClaudeExplorer.App/Compare/CopyViewModel.cs` with:
```csharp
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Mutation;
using ClaudeExplorer.Core.Sync;

namespace ClaudeExplorer.App.Compare;

/// <summary>
/// Applies a <see cref="CopyPlan"/> (from <see cref="ConfigCopyService"/>) through
/// <see cref="SafeMutationService"/>: each target write is preview → backup → write → change-log;
/// each Move removal is either a JSON content edit (settings/MCP/hooks) or a real undo-able delete
/// (files/dirs). A whole copy/move is applied as ONE logical group: <see cref="Undo"/> reverts every
/// recorded entry (writes + removals) so a recursive folder copy reverts atomically from the user's
/// perspective. The target scope is recorded as <see cref="ScopeKind.Project"/> (a reasonable
/// change-log grouping for cross-endpoint copies); the file path determines what is written.
/// </summary>
public sealed class CopyViewModel
{
    private readonly SafeMutationService _svc;
    private readonly ConfigCopyService _copier;
    private readonly Func<string> _nowIso;

    private readonly List<ChangeLogEntry> _applied = new();

    /// <summary>The change-log entry for the last applied target write (the first write of the plan),
    /// or null when nothing has been applied.</summary>
    public ChangeLogEntry? Applied => _applied.Count > 0 ? _applied[0] : null;

    /// <summary>A human-readable error from the last operation, or null on success.</summary>
    public string? Error { get; private set; }

    public CopyViewModel(SafeMutationService svc, ConfigCopyService copier, Func<string> nowIso)
    {
        _svc = svc;
        _copier = copier;
        _nowIso = nowIso;
    }

    public void Copy(CopyRequest req) => Run(() => _copier.PlanCopy(req), req);

    public void Move(CopyRequest req) => Run(() => _copier.PlanMove(req), req);

    private void Run(Func<CopyPlan> plan, CopyRequest req)
    {
        Error = null;
        _applied.Clear();
        try
        {
            var p = plan();
            // Writes first.
            foreach (var w in p.Writes)
            {
                var target = new ResolvedTarget(ScopeKind.Project, w.Path);
                var validation = w.IsJson ? new SettingsValidator().Validate(w.Content) : ValidationResult.Ok;
                var preview = _svc.PreviewEdit(target, w.Content, validation);
                _applied.Add(_svc.ApplyEdit(preview, _nowIso(), $"Copy {req.Category} {req.Key}"));
            }
            // Then source removals (delete files/dirs, or splice JSON).
            foreach (var r in p.Removals)
            {
                if (r.IsDelete)
                {
                    _applied.Add(_svc.ApplyDelete(
                        new ResolvedTarget(ScopeKind.User, r.Path), _nowIso(),
                        $"Move {req.Category} {req.Key} (remove source)"));
                }
                else
                {
                    var srcTarget = new ResolvedTarget(ScopeKind.User, r.Path);
                    var validation = new SettingsValidator().Validate(r.NewContent);
                    var preview = _svc.PreviewEdit(srcTarget, r.NewContent, validation);
                    _applied.Add(_svc.ApplyEdit(preview, _nowIso(), $"Move {req.Category} {req.Key} (remove source)"));
                }
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    /// <summary>Undo the whole group (every write + removal), in reverse order so re-creations and
    /// restores apply cleanly.</summary>
    public void Undo()
    {
        if (_applied.Count == 0) return;
        try
        {
            for (int i = _applied.Count - 1; i >= 0; i--)
                _svc.Undo(_applied[i]);
            _applied.Clear();
            Error = null;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }
}
```
> Note: JSON source removals (Settings/MCP/Hooks Move) carry the spliced content and `IsDelete == false`, so they go through `PreviewEdit`/`ApplyEdit` exactly as before (validated by `SettingsValidator`). The `Move_settings_key_*` tests still pass: a settings move now produces ONE write + ONE non-delete removal = 2 change-log entries (unchanged from before).
- [ ] **Step 4: Run → PASS.** filter `CopyViewModelTests`.
- [ ] **Step 5: Commit** `feat(app): CopyViewModel applies multi-file copy/move as one undo group`.

---

### Task C3: `CompareBar` + `DiffOverlay` components

**Files:** Create `src/ClaudeExplorer.App/Components/CompareBar.razor`; Create `src/ClaudeExplorer.App/Components/DiffOverlay.razor`; Modify `wwwroot/css/blueprint.css`. No unit test — these are render-only Blazor (Photino is headless-unverifiable); gate = build + `/run`. Logic (diff classification, request building, apply) is already unit-tested in B1/B2/C1/C2.

- [ ] **Step 1: Create `CompareBar.razor`** — `src/ClaudeExplorer.App/Components/CompareBar.razor`:
```razor
@using ClaudeExplorer.App.Compare
@inject CompareContext Compare
@implements IDisposable

<div class="cmpbar">
    <span class="cmpbar-lbl">Compare with</span>

    <select class="cmpbar-pick" value="@Compare.EndpointA?.Id" @onchange="OnAChanged" title="Endpoint A">
        @foreach (var ep in Compare.Endpoints)
        {
            <option value="@ep.Id" selected="@(ep.Id == Compare.EndpointA?.Id)">@ep.Label</option>
        }
    </select>

    <button class="cmpbar-swap" @onclick="() => Compare.Swap()" disabled="@(!Compare.IsComparing)" title="Swap A ⇄ B">⇄</button>

    <select class="cmpbar-pick" value="@Compare.EndpointB?.Id" @onchange="OnBChanged" title="Endpoint B (pick to compare)">
        <option value="" selected="@(Compare.EndpointB is null)">— off —</option>
        @foreach (var ep in Compare.Endpoints)
        {
            <option value="@ep.Id" selected="@(ep.Id == Compare.EndpointB?.Id)">@ep.Label</option>
        }
    </select>

    @if (Compare.IsComparing)
    {
        <span class="cmpbar-on">comparing</span>
    }
</div>

@code {
    protected override void OnInitialized() => Compare.Changed += OnChanged;

    private void OnChanged() => InvokeAsync(StateHasChanged);

    private void OnAChanged(ChangeEventArgs e)
    {
        var id = e.Value?.ToString();
        if (!string.IsNullOrEmpty(id)) Compare.SetA(id);
    }

    private void OnBChanged(ChangeEventArgs e)
    {
        var id = e.Value?.ToString();
        if (string.IsNullOrEmpty(id)) Compare.ClearB();
        else Compare.SetB(id);
    }

    public void Dispose() => Compare.Changed -= OnChanged;
}
```
- [ ] **Step 2: Create `DiffOverlay.razor`** — `src/ClaudeExplorer.App/Components/DiffOverlay.razor`. It takes the screen's `Category` name; reads `Compare.Comparison(Category)`; renders one diff chip per row, a selected-row detail with side values/content, and Copy →/← / Move / Undo (hidden when `ViewOnly` or for value-only categories where copy is unsupported). Settings projects get a shared/local target radio; Move from a Base shows the global warning:
```razor
@using ClaudeExplorer.App.Compare
@using ClaudeExplorer.App.Services
@inject CompareContext Compare
@inject CopyViewModel CopyVm
@inject RefreshService Refresh
@implements IDisposable

@if (Compare.IsComparing && Compare.Comparison(Category) is { } cat)
{
    <div class="diff-overlay">
        <div class="do-head">
            <span class="do-title">@Category diff · A @(Compare.EndpointA?.Label) ⇄ B @(Compare.EndpointB?.Label)</span>
            <span class="do-counts">
                <span class="dc same">@cat.Same =</span>
                <span class="dc diff">@cat.Differs ≠</span>
                <span class="dc onlya">@cat.OnlyA ◑</span>
                <span class="dc onlyb">@cat.OnlyB ○</span>
            </span>
            @if (cat.ViewOnly) { <span class="do-ro">view only</span> }
        </div>

        @if (cat.Rows.Count == 0)
        {
            <div class="do-empty">No items in this category on either side.</div>
        }

        @foreach (var row in cat.Rows)
        {
            var r = row;
            var selected = _selKey == row.Key;
            <div class="do-row @(selected ? "sel" : "")" @onclick="() => Toggle(r)">
                <span class="diffchip @ChipClass(row.Status)">@Chip(row.Status)</span>
                <span class="do-key">@row.Key</span>
            </div>
            @if (selected)
            {
                <div class="do-detail">
                    <div class="do-sides">
                        <div class="do-side">
                            <div class="do-side-h">A · @(Compare.EndpointA?.Label)</div>
                            <pre class="do-val">@(row.ContentA ?? row.ValueA ?? "— not set —")</pre>
                        </div>
                        <div class="do-side">
                            <div class="do-side-h">B · @(Compare.EndpointB?.Label)</div>
                            <pre class="do-val">@(row.ContentB ?? row.ValueB ?? "— not set —")</pre>
                        </div>
                    </div>

                    @if (!cat.ViewOnly && IsCopyable(Category))
                    {
                        @if (row.Status == DiffStatus.Same)
                        {
                            <div class="do-synced">✓ in sync</div>
                        }
                        else
                        {
                            <div class="do-acts">
                                <button class="cbtn" disabled="@(row.Status == DiffStatus.OnlyB)" title="Copy A → B"
                                        @onclick="() => OpenConfirm(r, aToB: true)">→ A to B</button>
                                <button class="cbtn" disabled="@(row.Status == DiffStatus.OnlyA)" title="Copy B → A"
                                        @onclick="() => OpenConfirm(r, aToB: false)">← B to A</button>
                            </div>
                        }

                        @if (_confirmKey == row.Key)
                        {
                            var srcEp = _aToB ? Compare.EndpointA : Compare.EndpointB;
                            var tgtEp = _aToB ? Compare.EndpointB : Compare.EndpointA;
                            <div class="do-confirm">
                                <div class="do-dir">@(srcEp?.Label) → @(tgtEp?.Label)</div>
                                @if (Category == "Settings" && tgtEp?.Kind == EndpointKind.Project)
                                {
                                    <div class="do-scope">
                                        <label><input type="radio" name="do-tgt-@row.Key" checked="@(!_local)" @onchange="() => _local = false" /> settings.json (shared)</label>
                                        <label><input type="radio" name="do-tgt-@row.Key" checked="@(_local)" @onchange="() => _local = true" /> settings.local.json</label>
                                    </div>
                                }
                                @if (srcEp?.Kind == EndpointKind.Base)
                                {
                                    <div class="do-warn">⚠ Move removes from the global base — affects all projects.</div>
                                }
                                @if (_done)
                                {
                                    <div class="do-ok">✓ Applied. <button class="cbtn sm" @onclick="UndoLast">Undo</button> <button class="cbtn sm" @onclick="CloseConfirm">Close</button></div>
                                }
                                else if (CopyVm.Error is { } err)
                                {
                                    <div class="do-err">@err <button class="cbtn sm" @onclick="CloseConfirm">Close</button></div>
                                }
                                else
                                {
                                    <div class="do-bar">
                                        <button class="btn-primary" @onclick="() => Execute(r, move: false)">Copy</button>
                                        <button class="btn-secondary" @onclick="() => Execute(r, move: true)">Move</button>
                                        <button class="btn-secondary" @onclick="CloseConfirm">Cancel</button>
                                    </div>
                                }
                            </div>
                        }
                    }
                    else if (!cat.ViewOnly)
                    {
                        <div class="do-soon">copy not available for this category</div>
                    }
                </div>
            }
        }
    </div>
}

@code {
    [Parameter, EditorRequired] public string Category { get; set; } = "";

    private string? _selKey;
    private string? _confirmKey;
    private bool _aToB;
    private bool _local;
    private bool _done;

    protected override void OnInitialized()
    {
        Compare.Changed += OnChanged;
    }

    private void OnChanged() => InvokeAsync(StateHasChanged);

    private void Toggle(CompareRow row)
    {
        _selKey = _selKey == row.Key ? null : row.Key;
        CloseConfirm();
    }

    private static bool IsCopyable(string category) =>
        category is "Settings" or "Memory" or "MCP" or "Hooks" or "Commands" or "Subagents" or "Skills";

    private void OpenConfirm(CompareRow row, bool aToB)
    {
        _confirmKey = row.Key;
        _aToB = aToB;
        _local = false;
        _done = false;
    }

    private void CloseConfirm()
    {
        _confirmKey = null;
        _done = false;
    }

    private void Execute(CompareRow row, bool move)
    {
        var srcEp = _aToB ? Compare.EndpointA : Compare.EndpointB;
        var tgtEp = _aToB ? Compare.EndpointB : Compare.EndpointA;
        if (srcEp is null || tgtEp is null) return;

        // Hand the builder the SOURCE side's resolved path for the chosen direction.
        var srcPath = _aToB ? row.PathA : row.PathB;
        var orientedRow = row with { SourcePath = srcPath };

        var req = CopyRequestBuilder.Build(Category, orientedRow, srcEp, tgtEp, _local);
        if (move) CopyVm.Move(req); else CopyVm.Copy(req);

        if (CopyVm.Error is null)
        {
            _done = true;
            // Re-diff so chips reflect the new state (CompareContext rebuilds from fresh snapshots).
            Compare.SetB(Compare.EndpointB!.Id);
            Refresh.Request();
        }
    }

    private void UndoLast()
    {
        CopyVm.Undo();
        if (CopyVm.Error is null)
        {
            _done = false;
            CloseConfirm();
            Compare.SetB(Compare.EndpointB!.Id);
            Refresh.Request();
        }
    }

    private static string Chip(DiffStatus s) => s switch
    {
        DiffStatus.Same => "=",
        DiffStatus.Differs => "≠",
        DiffStatus.OnlyA => "◑",
        _ => "○",
    };

    private static string ChipClass(DiffStatus s) => s switch
    {
        DiffStatus.Same => "same",
        DiffStatus.Differs => "diff",
        DiffStatus.OnlyA => "onlya",
        _ => "onlyb",
    };

    public void Dispose() => Compare.Changed -= OnChanged;
}
```
- [ ] **Step 3: CSS** — append to `wwwroot/css/blueprint.css` (reuses existing tokens `--blue`, `--ink-faint`, `--edge`, `--panel`, status colors):
```css
/* ── Per-screen compare overlay ───────────────────────────────────────── */
.cmpbar { display:flex; align-items:center; gap:9px; margin:10px 0 14px; font-family:'Spline Sans Mono',monospace; font-size:11px; }
.cmpbar-lbl { text-transform:uppercase; letter-spacing:.06em; color:var(--ink-faint); }
.cmpbar-pick { font-family:'Spline Sans Mono',monospace; font-size:11px; padding:4px 8px; border:1.4px solid var(--edge-2); border-radius:5px; background:var(--panel); }
.cmpbar-swap { width:26px; height:26px; border:1.4px solid var(--edge-2); border-radius:5px; background:var(--panel); cursor:pointer; }
.cmpbar-swap:disabled { opacity:.4; cursor:default; }
.cmpbar-on { color:var(--blue); font-weight:700; }
.diff-overlay { border:1.4px dashed var(--edge-2); border-radius:8px; padding:12px; margin-bottom:16px; }
.do-head { display:flex; align-items:center; gap:12px; margin-bottom:8px; font-family:'Spline Sans Mono',monospace; font-size:11px; }
.do-title { font-weight:700; }
.do-counts { display:flex; gap:10px; }
.do-counts .dc { color:var(--ink-faint); }
.do-counts .dc.diff { color:var(--amber); }
.do-ro { margin-left:auto; color:var(--ink-faint); }
.do-row { display:flex; align-items:center; gap:9px; padding:5px 8px; cursor:pointer; border-radius:5px; }
.do-row:hover, .do-row.sel { background:rgba(31,71,214,.06); }
.diffchip { width:18px; height:18px; display:inline-flex; align-items:center; justify-content:center; border-radius:4px; font-size:11px; font-weight:800; }
.diffchip.same { color:var(--green); } .diffchip.diff { color:var(--amber); } .diffchip.onlya, .diffchip.onlyb { color:var(--blue); }
.do-key { font-family:'Spline Sans Mono',monospace; font-size:12px; }
.do-detail { padding:8px 8px 12px 35px; }
.do-sides { display:flex; gap:12px; }
.do-side { flex:1; min-width:0; }
.do-side-h { font-family:'Spline Sans Mono',monospace; font-size:10px; color:var(--ink-faint); margin-bottom:3px; }
.do-val { white-space:pre-wrap; word-break:break-word; font-family:'Spline Sans Mono',monospace; font-size:11px; background:var(--panel); border:1px solid var(--edge); border-radius:5px; padding:7px; max-height:240px; overflow:auto; margin:0; }
.do-acts, .do-bar { display:flex; gap:8px; margin-top:9px; }
.cbtn { font-family:'Spline Sans Mono',monospace; font-size:11px; padding:3px 9px; border:1.4px solid var(--edge-2); border-radius:5px; background:var(--panel); cursor:pointer; }
.cbtn.sm { font-size:10px; padding:1px 7px; }
.cbtn:disabled { opacity:.4; cursor:default; }
.do-confirm { margin-top:9px; padding:9px; border:1.2px solid var(--edge-2); border-radius:6px; }
.do-dir { font-family:'Spline Sans Mono',monospace; font-size:11px; margin-bottom:6px; }
.do-scope { display:flex; gap:14px; font-size:11px; margin-bottom:6px; }
.do-warn { color:var(--amber); font-size:11px; margin-bottom:6px; }
.do-ok { color:var(--green); font-size:11px; display:flex; gap:8px; align-items:center; }
.do-err { color:var(--red); font-size:11px; display:flex; gap:8px; align-items:center; }
.do-synced { color:var(--green); font-size:11px; margin-top:8px; }
.do-soon, .do-empty { color:var(--ink-faint); font-size:11px; font-family:'Spline Sans Mono',monospace; }
```
> If a token (e.g. `--amber`, `--green`, `--red`, `--edge-2`) is named differently in the current `blueprint.css`, reuse the exact existing names — grep the file first and substitute.
- [ ] **Step 4: Build** → `dotnet build src/ClaudeExplorer.App/ClaudeExplorer.App.csproj -v quiet -clp:NoSummary` → 0 errors. (`CompareContext` + `CopyViewModel` are registered in DI in Task C4; the components compile against them now, but the page won't render until wired in Phase D — that's fine for a build gate.)
- [ ] **Step 5: Commit** `feat(app): CompareBar + DiffOverlay reusable compare components`.

---

### Task C4: DI registration for `CompareContext`

**Files:** Modify `src/ClaudeExplorer.App/Program.cs` (the `// Compare.` block, ~lines 129-137). No unit test — DI wiring; gate = build.

- [ ] **Step 1: Register `CompareContext` as a singleton.** In `Program.cs`, in the `// Compare.` block, after the `IEnvironmentCompareDataSource` registration add:
```csharp
        builder.Services.AddSingleton(sp => new CompareContext(
            sp.GetRequiredService<EnvironmentService>(),
            sp.GetRequiredService<ProjectRegistry>(),
            sp.GetRequiredService<IEnvironmentCompareDataSource>()));
```
Keep the existing `ConfigCopyService` (singleton) and `CopyViewModel` (transient) registrations, and leave `CompareViewModel` (both the class file and its registration) untouched — Task F1 deletes it together with the central Compare page. Do nothing else to DI in this task.
- [ ] **Step 2: Build** → `dotnet build src/ClaudeExplorer.App/ClaudeExplorer.App.csproj -v quiet -clp:NoSummary` → 0 errors.
- [ ] **Step 3: Commit** `feat(app): register CompareContext singleton in DI`.

---

## PHASE D — Per-screen wiring

Each task embeds `<CompareBar />` (top of the content) + `<DiffOverlay Category="..." />` (below the page head, above the existing content). The screens keep their existing single-endpoint behavior unchanged when compare is off. No unit tests (render-only); each task is build + a stated `/run` visual check. **Add `@using ClaudeExplorer.App.Compare` where needed** (the components are global once placed in `Components/`, but ensure the project's `_Imports.razor` already imports `ClaudeExplorer.App.Components`; if not, add `@using ClaudeExplorer.App.Components` per page).

### Task D1: Commands + Subagents screens

**Files:** Modify `src/ClaudeExplorer.App/Pages/Commands.razor`; `src/ClaudeExplorer.App/Pages/Subagents.razor`.

- [ ] **Step 1: Commands.** In `Commands.razor`, immediately after the closing `</div>` of `<div class="pagehead">…</div>` (after line 12), insert:
```razor
<CompareBar />
<DiffOverlay Category="Commands" />
```
- [ ] **Step 2: Subagents.** In `Subagents.razor`, after the `pagehead` block (after line 12), insert:
```razor
<CompareBar />
<DiffOverlay Category="Subagents" />
```
- [ ] **Step 3: Build** → 0 errors.
- [ ] **Step 4: `/run` visual-verify** (deferred — note in handoff): On Commands, pick a B endpoint in the compare bar → the overlay lists each command with a `= ≠ ◑ ○` chip; selecting a differing/only row shows both sides' file content and Copy →/← / Move; copying an `OnlyA` command creates the file under B's `commands/`; Undo reverts. Repeat on Subagents (writes under `agents/`). With B = "off" the screens look exactly as before.
- [ ] **Step 5: Commit** `feat(app): Commands + Subagents per-screen compare overlay`.

---

### Task D2: Skills screen

**Files:** Modify `src/ClaudeExplorer.App/Pages/Skills.razor`.

- [ ] **Step 1:** In `Skills.razor`, after the `pagehead` block (after line 12), insert:
```razor
<CompareBar />
<DiffOverlay Category="Skills" />
```
- [ ] **Step 2: Build** → 0 errors.
- [ ] **Step 3: `/run` visual-verify** (deferred): pick a B endpoint; an `OnlyA` skill shows Copy → A to B; copying writes the **whole skill folder** (SKILL.md + scripts/ + references/) under B's `skills/<name>/`; Move also deletes the source folder's files; a single Undo restores every file and removes the target copy.
- [ ] **Step 4: Commit** `feat(app): Skills per-screen compare overlay (recursive folder copy/move)`.

---

### Task D3: Hooks + MCP screens

**Files:** Modify `src/ClaudeExplorer.App/Pages/Hooks.razor`; `src/ClaudeExplorer.App/Pages/Mcp.razor`.

- [ ] **Step 1: Hooks.** In `Hooks.razor`, after the `pagehead` block (after line 17), insert:
```razor
<CompareBar />
<DiffOverlay Category="Hooks" />
```
> Hooks copy keys are `"<event>#<index>"` — the comparer does not currently emit per-hook-group rows for the **Hooks** category (it has no Hooks category today). For v1 the Hooks overlay reuses the existing MCP/Settings-style behavior via `CompareContext.Comparison("Hooks")`, which returns null because `EnvironmentComparer` produces no "Hooks" category. **Therefore Hooks shows the compare bar but the overlay renders nothing** (compare-off semantics) until a Hooks category is added. This matches the spec's "Core already supports" note for copy but defers the per-hook-group diff. Add a Hooks category in Task D3a below so the overlay is populated.
- [ ] **Step 1a (D3a): Add a Hooks category to the comparer.** This is a small Core-adjacent change in `EnvironmentComparer.cs`. Hooks live inside settings; expose them as rows keyed `"<event>#<index>"` with the hook-group JSON as the display/content. Add to the category list (after `"MCP"`):
```csharp
            BuildCategory("Hooks", HookMap(a), HookMap(b)),
```
and add the map (reads each snapshot's Settings for `hooks.*`):
```csharp
    private static Dictionary<string, CompareEntry> HookMap(EnvironmentSnapshot s)
    {
        var map = new Dictionary<string, CompareEntry>(StringComparer.Ordinal);
        foreach (var setting in s.Settings)
        {
            if (!setting.Key.StartsWith("hooks.", StringComparison.Ordinal)) continue;
            var evt = setting.Key.Substring("hooks.".Length);
            if (setting.Value is not System.Text.Json.Nodes.JsonArray groups) continue;
            for (var i = 0; i < groups.Count; i++)
                map[$"{evt}#{i}"] = new CompareEntry(groups[i]?.ToJsonString() ?? "null");
        }
        return map;
    }
```
> Add a test for this in `EnvironmentComparerTests.cs`:
```csharp
    [Fact]
    public void Hooks_category_keys_groups_by_event_and_index()
    {
        var hooks = System.Text.Json.Nodes.JsonNode.Parse(
            """[ { "matcher":"Bash", "hooks":[ {"type":"command","command":"echo a"} ] } ]""");
        var a = Snap(settings: new[] { new EffectiveSetting("hooks.PreToolUse", MergeStrategy.ArrayConcat, hooks, null, Array.Empty<SettingContribution>(), false) });
        var b = Snap();

        var cat = EnvironmentComparer.Compare(a, b).Find("Hooks")!;
        Assert.Equal(DiffStatus.OnlyA, cat.Rows.Single(r => r.Key == "PreToolUse#0").Status);
    }
```
And update the `Produces_seven_categories` expected array to include `"Hooks"` after `"MCP"`:
```csharp
        Assert.Equal(new[] { "Settings", "Commands", "Skills", "Subagents", "MCP", "Hooks", "Memory", "Plugins", "Dependencies" },
            c.Categories.Select(x => x.Name).ToArray());
```
Run `dotnet test --filter "FullyQualifiedName~EnvironmentComparerTests"` → PASS. The Hooks `CopyRequest` source/target paths default to settings.json — extend `CopyRequestBuilder.Build` with a `"Hooks"` case:
```csharp
            case "Hooks":
            {
                var srcPath = $"{ClaudeRoot(src)}/settings.json";
                var tgtPath = local ? $"{ClaudeRoot(tgt)}/settings.local.json" : $"{ClaudeRoot(tgt)}/settings.json";
                return new CopyRequest("Hooks", key, SourceSettingsPath: srcPath, TargetSettingsPath: tgtPath);
            }
```
Add a `CopyRequestBuilderTests` case:
```csharp
    [Fact]
    public void Hooks_uses_settings_json_paths_and_keeps_the_event_index_key()
    {
        var row = new CompareRow("PreToolUse#0", DiffStatus.OnlyA, "{...}", null);
        var req = CopyRequestBuilder.Build("Hooks", row, src: Base, tgt: Proj, local: false);
        Assert.Equal("Hooks", req.Category);
        Assert.Equal("PreToolUse#0", req.Key);
        Assert.Equal("C:/Users/me/.claude/settings.json", req.SourceSettingsPath);
        Assert.Equal("D:/work/a/.claude/settings.json", req.TargetSettingsPath);
    }
```
Run `--filter "FullyQualifiedName~CopyRequestBuilderTests"` → PASS.
- [ ] **Step 2: MCP.** In `Mcp.razor`, after the `pagehead` block (after line 18), insert:
```razor
<CompareBar />
<DiffOverlay Category="MCP" />
```
- [ ] **Step 3: Build** → 0 errors. Run `dotnet test ClaudeExplorer.slnx --filter "FullyQualifiedName~Comparer|FullyQualifiedName~CopyRequestBuilder"` → all green.
- [ ] **Step 4: `/run` visual-verify** (deferred): MCP overlay diffs server defs (key = server name), copies a server entry between `.claude.json`/`.mcp.json`; Hooks overlay diffs hook groups by `event#idx` and copies a group into the target settings.json.
- [ ] **Step 5: Commit** `feat(app): Hooks + MCP per-screen compare overlay (+ Hooks compare category)`.

---

### Task D4: Effective Config screen (settings)

**Files:** Modify `src/ClaudeExplorer.App/Pages/EffectiveConfig.razor`.

- [ ] **Step 1:** In `EffectiveConfig.razor`, after the `pagehead` block (after line 20), insert:
```razor
<CompareBar />
<DiffOverlay Category="Settings" />
```
- [ ] **Step 2: Build** → 0 errors.
- [ ] **Step 3: `/run` visual-verify** (deferred): pick a B endpoint; the overlay lists settings keys with chips; a differing/only key offers Copy →/← / Move; when the **target** endpoint is a Project, the confirm shows the shared-vs-local radio (settings.json vs settings.local.json); Move from a Base shows the "affects all projects" warning; copy writes into the target settings file; Undo reverts. The existing per-row provenance matrix + safe-edit panel are unaffected when compare is off.
- [ ] **Step 4: Commit** `feat(app): Effective Config per-screen settings compare overlay`.

---

### Task D5: Plugins + Dependencies screens (view-only)

**Files:** Modify `src/ClaudeExplorer.App/Pages/Plugins.razor`; `src/ClaudeExplorer.App/Pages/Dependencies.razor`.

- [ ] **Step 1: Plugins.** In `Plugins.razor`, after the `pagehead` block (after line 18), insert:
```razor
<CompareBar />
<DiffOverlay Category="Plugins" />
```
- [ ] **Step 2: Dependencies.** In `Dependencies.razor`, after the `pagehead` block (after line 12), insert:
```razor
<CompareBar />
<DiffOverlay Category="Dependencies" />
```
- [ ] **Step 3: Build** → 0 errors.
- [ ] **Step 4: `/run` visual-verify** (deferred): both overlays show presence/status diff chips (Plugins: installed-on-one-side; Dependencies: present/version diff) and display the "view only" tag; **no** Copy/Move buttons render (`ViewOnly == true`).
- [ ] **Step 5: Commit** `feat(app): Plugins + Dependencies view-only compare overlay`.

---

## PHASE E — Memory screen

### Task E1: `MemoryRows` mapper + `MemoryViewModel`

**Files:** Create `src/ClaudeExplorer.App/Screens/Memory/MemoryRows.cs`; Create `src/ClaudeExplorer.App/Screens/Memory/MemoryViewModel.cs`; Test (new) `tests/ClaudeExplorer.App.Tests/Screens/MemoryRowsTests.cs`.

The Memory screen lists CLAUDE.md files for the active workspace: the global `~/.claude/CLAUDE.md`, the project `<projectDir>/CLAUDE.md` and `<projectDir>/CLAUDE.local.md`, and nested `**/CLAUDE.md` under the project. Discovery is a pure mapper over `IFileSystem` so it is unit-tested.

- [ ] **Step 1: Failing test** — create `tests/ClaudeExplorer.App.Tests/Screens/MemoryRowsTests.cs`:
```csharp
using ClaudeExplorer.App.Screens.Memory;
using ClaudeExplorer.App.Tests.Fakes;

namespace ClaudeExplorer.App.Tests.Screens;

public class MemoryRowsTests
{
    [Fact]
    public void Discovers_global_then_project_then_nested_in_load_order()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("C:/Users/me/.claude/CLAUDE.md", "# global");
        fs.AddFile("D:/work/a/CLAUDE.md", "# project");
        fs.AddFile("D:/work/a/CLAUDE.local.md", "# local");
        fs.AddFile("D:/work/a/packages/api/CLAUDE.md", "# nested");

        var rows = MemoryRowsMapper.Discover(fs, userDir: "C:/Users/me", projectDir: "D:/work/a");

        Assert.Collection(rows.Select(r => r.Scope),
            s => Assert.Equal(MemoryScope.Global, s),
            s => Assert.Equal(MemoryScope.Project, s),
            s => Assert.Equal(MemoryScope.Local, s),
            s => Assert.Equal(MemoryScope.Nested, s));
        Assert.Equal("D:/work/a/packages/api/CLAUDE.md", rows.Last().Path);
        Assert.Equal("# global", rows.First().Content);
    }

    [Fact]
    public void Omits_absent_files()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("C:/Users/me/.claude/CLAUDE.md", "# global");
        var rows = MemoryRowsMapper.Discover(fs, "C:/Users/me", projectDir: "");
        Assert.Single(rows);
        Assert.Equal(MemoryScope.Global, rows[0].Scope);
    }

    [Fact]
    public void Nested_excludes_the_top_level_project_claude_md()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("D:/work/a/CLAUDE.md", "# project");
        fs.AddFile("D:/work/a/sub/CLAUDE.md", "# nested");
        var rows = MemoryRowsMapper.Discover(fs, "C:/Users/me", "D:/work/a");

        Assert.Single(rows, r => r.Scope == MemoryScope.Project);
        Assert.Single(rows, r => r.Scope == MemoryScope.Nested && r.Path == "D:/work/a/sub/CLAUDE.md");
    }
}
```
- [ ] **Step 2: Run → FAIL.** filter `MemoryRowsTests`.
- [ ] **Step 3a: Implement the mapper** — create `src/ClaudeExplorer.App/Screens/Memory/MemoryRows.cs`:
```csharp
using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.App.Screens.Memory;

public enum MemoryScope { Global, Project, Local, Nested }

/// <summary>One discovered CLAUDE.md memory file.</summary>
public sealed record MemoryRow(MemoryScope Scope, string Name, string Path, string Content);

/// <summary>Pure discovery of CLAUDE.md files in load order: global (~/.claude), project root
/// (CLAUDE.md then CLAUDE.local.md), then nested project CLAUDE.md (excluding the root). Absent files
/// are omitted. No writes — read-only over <see cref="IFileSystem"/>.</summary>
public static class MemoryRowsMapper
{
    public static IReadOnlyList<MemoryRow> Discover(IFileSystem fs, string userDir, string projectDir)
    {
        var rows = new List<MemoryRow>();

        var global = $"{userDir.Replace('\\', '/').TrimEnd('/')}/.claude/CLAUDE.md";
        if (!string.IsNullOrEmpty(userDir) && fs.FileExists(global))
            rows.Add(new MemoryRow(MemoryScope.Global, "CLAUDE.md", global, fs.ReadAllText(global)));

        var proj = projectDir.Replace('\\', '/').TrimEnd('/');
        if (!string.IsNullOrEmpty(proj))
        {
            var rootMd = $"{proj}/CLAUDE.md";
            if (fs.FileExists(rootMd))
                rows.Add(new MemoryRow(MemoryScope.Project, "CLAUDE.md", rootMd, fs.ReadAllText(rootMd)));

            var localMd = $"{proj}/CLAUDE.local.md";
            if (fs.FileExists(localMd))
                rows.Add(new MemoryRow(MemoryScope.Local, "CLAUDE.local.md", localMd, fs.ReadAllText(localMd)));

            foreach (var f in fs.GetFiles(proj, "CLAUDE.md", recurse: true))
            {
                if (f == rootMd) continue; // already added as Project
                rows.Add(new MemoryRow(MemoryScope.Nested, "CLAUDE.md", f, fs.ReadAllText(f)));
            }
        }

        return rows;
    }
}
```
- [ ] **Step 3b: Implement the ViewModel** — create `src/ClaudeExplorer.App/Screens/Memory/MemoryViewModel.cs`:
```csharp
using ClaudeExplorer.App.Mvvm;
using ClaudeExplorer.App.Services;
using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.App.Screens.Memory;

/// <summary>Loads the CLAUDE.md memory files for the active workspace (global + project + nested) and
/// exposes them as rows with a selected file for the detail/viewer pane.</summary>
public sealed class MemoryViewModel : ObservableObject
{
    private readonly IFileSystem _fs;
    private readonly IWorkspaceContext _workspace;

    private IReadOnlyList<MemoryRow> _rows = Array.Empty<MemoryRow>();
    private MemoryRow? _selected;
    private bool _isLoading;
    private string? _errorMessage;

    public MemoryViewModel(IFileSystem fs, IWorkspaceContext workspace)
    {
        _fs = fs;
        _workspace = workspace;
    }

    public IReadOnlyList<MemoryRow> Rows { get => _rows; private set => SetProperty(ref _rows, value); }
    public MemoryRow? Selected { get => _selected; set => SetProperty(ref _selected, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

    public void Load()
    {
        IsLoading = true;
        try
        {
            Rows = MemoryRowsMapper.Discover(_fs, _workspace.UserDir, _workspace.ProjectDir);
            if (_selected is not null)
                Selected = Rows.FirstOrDefault(r => r.Path == _selected.Path);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
```
- [ ] **Step 4: Run → PASS.** filter `MemoryRowsTests`.
- [ ] **Step 5: Commit** `feat(app): Memory discovery (MemoryRows) + MemoryViewModel`.

---

### Task E2: Memory page + DI + left-rail entry

**Files:** Create `src/ClaudeExplorer.App/Pages/Memory.razor`; Modify `src/ClaudeExplorer.App/Program.cs` (register `MemoryViewModel`); Modify `src/ClaudeExplorer.App/Components/LeftRail.razor` (add Memory nav under Config Artifacts). No unit test — render-only; gate = build + `/run`.

- [ ] **Step 1: DI.** In `Program.cs`, with the other Batch-A transient ViewModels (near `builder.Services.AddTransient<HooksViewModel>();`), add:
```csharp
        builder.Services.AddTransient<ClaudeExplorer.App.Screens.Memory.MemoryViewModel>();
```
- [ ] **Step 2: Create `Memory.razor`** — `src/ClaudeExplorer.App/Pages/Memory.razor` (mirrors the Skills master/detail + CodeViewer pattern, plus the compare overlay):
```razor
@page "/memory"
@using ClaudeExplorer.App.Screens.Memory
@inject MemoryViewModel Vm
@inject RefreshService Refresh
@implements IDisposable

<div class="pagehead">
    <div><div class="k">Project Memory</div><h1>Memory</h1></div>
    <div class="scope">@Vm.Rows.Count CLAUDE.md FILES</div>
</div>

<CompareBar />
<DiffOverlay Category="Memory" />

@if (Vm.IsLoading)
{
    <CornerTickPanel Class="card"><div class="sub" style="padding:18px">Loading…</div></CornerTickPanel>
}
else if (Vm.ErrorMessage is not null)
{
    <CornerTickPanel Class="card error-panel"><div class="rowx"><div class="glyph bad"></div>
        <div class="body"><div class="ttl">Failed to load memory</div><div class="meta">@Vm.ErrorMessage</div></div></div></CornerTickPanel>
}
else
{
    <div class="browser-layout">
        <CornerTickPanel Class="card master-panel">
            @if (Vm.Rows.Count == 0) { <div class="sub" style="padding:16px">No CLAUDE.md files found for this workspace.</div> }
            @foreach (var row in Vm.Rows)
            {
                var sel = row;
                <div class="master-item @(Vm.Selected == row ? "selected" : "")" @onclick="() => Vm.Selected = sel">
                    <span class="master-item-name">@row.Name</span>
                    <span class="scope-tag s-@row.Scope.ToString().ToLowerInvariant()">@row.Scope</span>
                </div>
            }
        </CornerTickPanel>
        <div class="detail-panel">
            @if (Vm.Selected is { } s)
            {
                <CornerTickPanel Class="card" style="padding:18px">
                    <h2 style="margin:0 0 6px;font-size:19px">@s.Name</h2>
                    <div class="kv">
                        <span class="kvk">scope</span><span class="kvv">@s.Scope</span>
                        <span class="kvk">file</span><span class="kvv">@s.Path</span>
                    </div>
                    <div style="margin-top:12px"><CodeViewer Title="@s.Path" Content="@s.Content" Language="markdown" /></div>
                </CornerTickPanel>
            }
            else
            {
                <CornerTickPanel Class="card"><div class="sub" style="padding:24px;text-align:center;color:var(--ink-faint)">Select a memory file to see its contents.</div></CornerTickPanel>
            }
        </div>
    </div>
}

@code {
    protected override void OnInitialized()
    {
        Vm.PropertyChanged += OnVmChanged;
        Refresh.Requested += OnRefreshRequested;
        Vm.Load();
    }

    private void OnVmChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => InvokeAsync(StateHasChanged);
    private void OnRefreshRequested() => Vm.Load();

    public void Dispose()
    {
        Vm.PropertyChanged -= OnVmChanged;
        Refresh.Requested -= OnRefreshRequested;
    }
}
```
> `CodeViewer` already accepts `Language` (used in Hooks/Skills); `"markdown"` is fine for highlight.js. If markdown is not bundled, omit the `Language="markdown"` attribute (defaults to plain).
- [ ] **Step 3: Left-rail entry.** In `LeftRail.razor`, in the **Config Artifacts** group, after the `subagents` NavLink (line ~29-32) and before `hooks`, add a Memory link:
```razor
    <NavLink class="nav" href="memory">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M4 4h13l3 3v13H4z"/><path d="M8 4v6h8"/><path d="M8 14h8M8 17h5"/></svg>
        Memory
    </NavLink>
```
- [ ] **Step 4: Build** → 0 errors.
- [ ] **Step 5: `/run` visual-verify** (deferred): Left rail shows **Memory** under Config Artifacts; the Memory page lists global/project/nested CLAUDE.md with scope tags and renders the selected file read-only; the compare bar/overlay diffs CLAUDE.md across endpoints and supports Copy/Move (writes the file to the target endpoint).
- [ ] **Step 6: Commit** `feat(app): Memory screen (page + DI + left-rail entry)`.

---

## PHASE F — Retire central Compare + relocate Add-project

### Task F1: Delete `Compare.razor`, the `CompareViewModel`, and the Compare left-rail entry

**Files:** Delete `src/ClaudeExplorer.App/Pages/Compare.razor`; Delete `src/ClaudeExplorer.App/Compare/CompareViewModel.cs`; Delete `tests/ClaudeExplorer.App.Tests/Compare/CompareViewModelTests.cs`; Modify `src/ClaudeExplorer.App/Program.cs` (remove the `CompareViewModel` registration); Modify `src/ClaudeExplorer.App/Components/LeftRail.razor` (remove the Compare nav + the now-empty Analyze label).

- [ ] **Step 1: Delete files.** Remove the three files:
```powershell
Remove-Item src/ClaudeExplorer.App/Pages/Compare.razor
Remove-Item src/ClaudeExplorer.App/Compare/CompareViewModel.cs
Remove-Item tests/ClaudeExplorer.App.Tests/Compare/CompareViewModelTests.cs
```
- [ ] **Step 2: Remove DI registration.** In `Program.cs` delete the line:
```csharp
        builder.Services.AddTransient<CompareViewModel>();
```
- [ ] **Step 3: Trim the left rail.** In `LeftRail.razor` remove the entire **Analyze** block (the `<div class="lbl">Analyze</div>` label and the `compare` `<NavLink>…</NavLink>`, lines ~46-50). Leave **Discover** (Marketplace/Recommended/Change Log) and everything else intact.
- [ ] **Step 4: Build** → `dotnet build src/ClaudeExplorer.App/ClaudeExplorer.App.csproj -v quiet -clp:NoSummary` → 0 errors. If the build reports an unused `using ClaudeExplorer.App.Compare;` is now needed elsewhere, ignore (the namespace is still used by `CompareContext`/`CopyViewModel`/components). Confirm no remaining reference to `CompareViewModel` (grep): `Grep pattern="CompareViewModel"` → only docs/plan hits, no `.cs`/`.razor`.
- [ ] **Step 5: Commit** `refactor(app): retire central Compare page + CompareViewModel`.

---

### Task F2: Relocate "Add project endpoint" into the environment selector

**Files:** Modify `src/ClaudeExplorer.App/Components/EnvironmentSelector.razor` (inject `ProjectRegistry`; add an action + modal). No unit test — render-only (the `ProjectRegistry.Add` logic is already covered by `ProjectRegistryTests`); gate = build + `/run`.

- [ ] **Step 1: Inject `ProjectRegistry`.** At the top of `EnvironmentSelector.razor`, after `@inject EnvironmentService EnvService` add:
```razor
@inject ClaudeExplorer.App.Environments.ProjectRegistry Projects
```
- [ ] **Step 2: Add the dropdown action.** In the `Actions` section of the dropdown (after the `"＋ Add custom root…"` item, line ~25), add:
```razor
                <div class="env-dropdown-item" @onclick="AddProject">＋ Add project endpoint…</div>
```
- [ ] **Step 3: Add the project modal.** After the existing custom-env modal (`@if (_addOpen) { … }`, ends ~line 53), add a second modal:
```razor
@if (_addProjOpen)
{
    <div style="position:fixed;top:0;left:0;right:0;bottom:0;z-index:200;background:rgba(0,0,0,.18);" @onclick="CancelAddProject"></div>
    <div style="position:fixed;top:50%;left:50%;transform:translate(-50%,-50%);z-index:201;background:var(--panel);border:1.5px solid var(--edge-2);border-radius:8px;padding:20px 24px;min-width:360px;box-shadow:6px 6px 0 var(--edge);">
        <div style="font-weight:800;font-size:13px;text-transform:uppercase;margin-bottom:12px;">Add Project Endpoint</div>
        <label style="font-family:'Spline Sans Mono',monospace;font-size:11px;color:var(--ink-faint);">Project folder path</label>
        <input class="search-box" style="width:100%;margin:6px 0;" @bind="_projPath" placeholder="D:/work/myproject" />
        <label style="font-family:'Spline Sans Mono',monospace;font-size:11px;color:var(--ink-faint);">Display name</label>
        <input class="search-box" style="width:100%;margin:6px 0;" @bind="_projName" placeholder="My Project" />
        <div style="display:flex;gap:9px;margin-top:14px;">
            <button class="btn-primary" @onclick="ConfirmAddProject">Add</button>
            <button class="btn-secondary" @onclick="CancelAddProject">Cancel</button>
        </div>
    </div>
}
```
- [ ] **Step 4: Add the handlers.** In the `@code` block, add fields + methods:
```csharp
    private bool _addProjOpen;
    private string _projPath = "";
    private string _projName = "";

    private void AddProject()
    {
        _leftOpen = false;
        _addProjOpen = true;
        _projPath = "";
        _projName = "";
    }

    private void ConfirmAddProject()
    {
        if (!string.IsNullOrWhiteSpace(_projPath))
        {
            var name = string.IsNullOrWhiteSpace(_projName)
                ? System.IO.Path.GetFileName(_projPath.TrimEnd('/', '\\'))
                : _projName;
            Projects.Add(name, EnvService.Active.Id, _projPath.Trim());
        }
        _addProjOpen = false;
    }

    private void CancelAddProject() => _addProjOpen = false;
```
- [ ] **Step 5: Build** → 0 errors.
- [ ] **Step 6: `/run` visual-verify** (deferred): the environment-selector dropdown now has both "＋ Add custom root…" and "＋ Add project endpoint…"; adding a project makes it appear in every screen's compare bar B/A dropdown (and persists across restarts via `ProjectRegistry`).
- [ ] **Step 7: Commit** `feat(app): add-project endpoint action in the environment selector`.

---

## PHASE G — Verification

### Task G1: Full test + build + handoff note

- [ ] **Step 1: Full suite.** `dotnet test ClaudeExplorer.slnx` → all green. Must include: `MutatorDeleteTests`, `SafeMutationServiceDeleteTests`, `ConfigCopyServiceTests` (dir copy/move), `EnvironmentComparerTests` (Subagents rename + path/content + Hooks category), `CopyRequestBuilderTests`, `CompareContextTests`, `CopyViewModelTests` (file/dir move + undo), `MemoryRowsTests`. Confirm no test still references the deleted `CompareViewModel`/`Compare.razor`.
- [ ] **Step 2: Build App.** `dotnet build src/ClaudeExplorer.App/ClaudeExplorer.App.csproj -v quiet -clp:NoSummary` → 0 warnings/errors.
- [ ] **Step 3: Update `docs/superpowers/HANDOFF.md`** (Latest section): per-screen Compare/Sync shipped — compare lives inside each artifact screen via a shared `CompareContext` + `CompareBar`/`DiffOverlay`; recursive Skills dir copy/move + undo-able delete; Commands/Subagents/Skills/Memory copy now work (enriched diff rows carry path/content; Agents↔Subagents reconciled); new Memory screen; central `/compare` page retired and Add-project moved to the env selector. Record the new total test count (run `dotnet test` and read the summary) and the tip commit hash.
- [ ] **Step 4: Commit** `docs: note per-screen Compare/Sync in HANDOFF`.

---

## Self-review

- **Spec coverage:**
  1. Compare-with overlay inside each artifact screen (bar `A ▾ ⇄ B ▾`, off by default, per-row `= ≠ ◑ ○`, detail diff + Copy →/← / Move / Undo): `CompareBar`/`DiffOverlay` (C3) wired per screen (D1–D5, E2). ✓
  2. Shared persistent `CompareContext` + reusable component: C1, C3. ✓
  3. Recursive directory copy/move for Skills (new multi-op `CopyPlan`, consistent with the existing single-file/JSON branches): A3. ✓
  4. Undo-able delete in safe-mutation; Move fully works for files/dirs; degrade removed: A1/A2 (+ C2). ✓
  5. Diff rows carry resolved path (+ content); Agents↔Subagents reconciled: B1/B2 (+ D3a Hooks). ✓
  6. New Memory screen (left-rail + page + ViewModel + mapper), reuses the snapshot Memory map for compare: E1/E2. ✓
  7. Retire central Compare page + left-rail entry; relocate Add-project to env selector: F1/F2. ✓
  8. View-only Plugins & Dependencies (status/presence diff only, no copy): enforced by `CompareCategory.ViewOnly` + `DiffOverlay` (D5). ✓
  - Out of scope honored: no plugin/dep copy; no N-way matrix.
- **Type consistency across tasks:** `ChangeKind.Delete`, `Mutator.ApplyDelete`, `SafeMutationService.ApplyDelete`; `CopyWrite`/`SourceRemoval(IsDelete)`/`CopyPlan(Writes,Removals)` (+ back-compat props `TargetPath`/`NewTargetContent`/`TargetIsJson`/`SourceRemoval`); `CompareEntry`; `CompareRow(...,PathA,PathB,ContentA,ContentB,SourcePath)`; `EnvironmentComparer` categories `Settings/Commands/Skills/Subagents/MCP/Hooks/Memory/Plugins/Dependencies`; `CopyRequestBuilder.Build`; `CompareContext` (`Endpoints/EndpointA/EndpointB/IsComparing/SetA/SetB/ClearB/Swap/Comparison/Changed`); `CopyViewModel(Copy/Move/Undo/Applied/Error)`; `MemoryRowsMapper.Discover` / `MemoryRow` / `MemoryScope` / `MemoryViewModel`. Names consistent everywhere. ✓
- **Safety contract preserved** for every write/delete (validate → backup → write/delete → change-log → undo); a recursive copy/move records every op and `CopyViewModel.Undo` reverts the whole group in reverse order. ✓
- **UI-only tasks** (C3, D1–D5, E2, F1–F2) are build + `/run` (Photino is headless-unverifiable); all logic (diff classification, path building, plan apply, memory discovery, delete) is unit-tested.

## Out of scope (this plan)
Plugin/Dependency copy (compare-only); N-way (3+) matrix; effective-merged compare toggle; bulk copy-all; comparing credentials/sessions/cache.
