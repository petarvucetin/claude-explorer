# Hooks Row Redesign + Inline JSON Editor + Syntax Highlighting — Design

**Status:** Proposed 2026-06-08. Mockups (Blueprint, real tokens):
`ux-explorations/hooks-row-final.html` (row),
`ux-explorations/hooks-inline-panel.html` (inline panel),
`ux-explorations/hooks-header-options.html` (section header, already shipped).

## Goal

On the **Hooks** screen:

1. **Redesign the row** so a long pipe-delimited matcher
   (`Bash|Read|Write|Edit|NotebookEdit|Glob|Grep|…`) no longer blows out the layout.
2. **Click a row → open an inline accordion panel** directly beneath that row containing:
   - the hook's matcher-group as **nicely-formatted, editable JSON**, and
   - the **actual script file** the command runs, **read-only and syntax-highlighted**.
3. **Save routes through the existing safe-mutation layer** (diff → backup → validate →
   change-log → undo).
4. Add **syntax highlighting** (highlight.js) to read-only code views **app-wide**.

## Decisions (from brainstorming)

- **Row = Option A.** Every tool chip is **visible** (no "+N more"); chips wrap to as many lines
  as needed. Scope tag + health pill pinned top-right; the command sits on its own line below a
  hairline. A `*`/empty matcher renders as a single `✶ any tool` chip. A token that isn't a plain
  tool name (regex like `Notebook.*`, `mcp__.*`) is shown as-is in one chip.
- **Edit unit = the matcher-group block** (`{ "matcher": …, "hooks": [ … ] }`), edited as
  formatted JSON and **spliced back into the defining source `settings.json`**. The whole file is
  what actually gets written (so diff/backup/undo operate on the real file); the user only edits
  the one block.
- **Edit target = the defining file only (EditWinner) for v1.** If the winner is the global/user
  source, show the "affects every project" warning. **Project/Local override is deferred**: Claude
  *unions* hook arrays across scopes, so an "override at Project" would **duplicate** the hook
  rather than replace it — wrong mental model, needs its own design.
- **Plugin- and enterprise/managed-sourced hooks are read-only.** The panel still opens (JSON +
  file shown read-only) with a short reason; no Save.
- **Highlighter = bundled highlight.js** (prebuilt MIT asset) + a minimal Blazor JS-interop call,
  applied to **read-only** code views only. The **editable** JSON box is a formatted `<textarea>`
  (live token-coloring while typing is deferred — that needs CodeMirror).
- **Both code surfaces have a capped height with vertical scroll** when content overflows.

## UI changes (`ClaudeExplorer.App`)

### Hooks row (`Hooks.razor` + `blueprint.css`)
Replace the current `.mcprow` reuse with a dedicated two-line hook row:
- **Line 1:** wrapping tool chips (left, `flex:1`) + a fixed right cluster (scope tag, health
  pill, and a `read-only` tag for plugin/enterprise rows).
- **Line 2:** a `command` type tag + the command (mono, ellipsis) below a dashed hairline.
- Matcher → chips is a **pure mapper helper** (`MatcherChips(matcher)`): `*`/empty → `["∗ any"]`
  (flagged `any`); else split on `|`; each token a chip.

### Inline accordion (move detail from page-bottom to under the row)
- Selection becomes **index-based** (`(eventGroupIndex, rowIndex)` of the rendered list) instead
  of `HookRow` record equality, so identical rows can't both expand. (Distinct from
  `SourceGroupIndex` below, which addresses the JSON block on disk.)
- Render the panel inside the `@foreach` immediately after the selected row. It contains:
  1. **Warning** band when the editable target is the global/user source.
  2. **Hook · JSON** segment — editable `<textarea>` pre-filled with the pretty-printed block
     (read-only highlighted block instead, when not editable).
  3. **Runs file** segment — read-only highlighted view of the resolved script (`CodeViewer`
     with a `Language`), **or** a note ("runs an inline command / script not found on disk") when
     nothing resolves.
  4. **Action bar** — `↻ logged + backup + undo` note, `Preview diff`, `Cancel`, `Save`
     (Save hidden for read-only rows).
- Both code surfaces: `max-height` + `overflow:auto`.

### `CodeViewer.razor` upgrade (used app-wide)
- New optional `Language` parameter → renders `<code class="language-xxx">` and calls
  `hljs.highlightElement` in `OnAfterRenderAsync` via `IJSRuntime`. JSON pretty-print retained.
- Add a capped-height variant (param or CSS class) for scrollable embeds.

### highlight.js wiring
- `wwwroot/lib/highlight/highlight.min.js` (subset bundle: json, javascript, typescript,
  python, bash/shell, powershell, yaml) + `blueprint-dark.css` (theme tuned to the `#0F1722`
  code surface). Referenced from `wwwroot/index.html`.
- Tiny interop shim (`wwwroot/js/codeview.js`): `window.cx.highlight(el)`.

### `HooksViewModel`
Add edit state + actions, backed by the existing singleton `SafeMutationService`:
- `BeginEdit(row)` → extract block text (`HookBlockEditor.ExtractBlock`), compute editability +
  resolved script ref.
- `PreviewSave()` / `Save(timestamp)` → `HookBlockEditor.SpliceBlock` → whole-file content →
  `SafeMutationService.PreviewSettingsEdit(EditWinner, …)` → `ApplyEdit` with description
  `Edit <Event> hook (<matcher preview>)`; surfaces validation errors inline.
- `Undo(entry)` → `SafeMutationService.Undo`. Timestamp via the same source other VMs use.

## Core changes (`ClaudeExplorer.Core`)

### 1. `HookBlockEditor` (new, `Core/Hooks/` or `Core/Mutation/`)
Pure, fully tested. Operates on **raw source-file text** (not the merged view):
- `ExtractBlock(sourceText, event, sourceGroupIndex) → string` — returns
  `hooks.<event>[sourceGroupIndex]` pretty-printed (2-space).
- `SpliceBlock(sourceText, event, sourceGroupIndex, editedBlockJson) → string` — validate
  `editedBlockJson` parses and is a matcher-group object (`matcher` string + `hooks` array);
  replace that element; re-serialize the **whole** file pretty. Throws `MutationException` on
  invalid JSON / wrong shape / index out of range.
- `SourceGroupIndex` = the matcher-group's position within **that one source file's**
  `hooks.<event>` array. `HookRow` gains `SourceGroupIndex` (the mapper records it as it iterates
  each contribution's matcher-groups); combined with `SourceFile` it uniquely addresses the block
  even when duplicate groups exist. Read the file fresh at edit time so the index stays valid.

### 2. `HookScriptResolver` (new, `Core/Hooks/`)
`Resolve(command, sourceFileDir, projectDir, userDir) → ScriptRef?` where
`ScriptRef(AbsolutePath, Language, Exists)`:
- Strip a known runtime prefix (reuse `ExecutableExtractor`); take the first argument that is a
  path with a known script extension (`.js .mjs .cjs .ts .sh .bash .zsh .py .ps1 .cmd .bat .rb
  .pl`) **or** that resolves to an existing file.
- Resolve against candidate bases in order: as-is (if rooted), `sourceFileDir`, `projectDir`,
  `userDir`. Expand `${CLAUDE_PLUGIN_ROOT}` / `%VAR%` only when known; otherwise return `null`.
- Map extension → highlight.js language id.
- Returns `null` for inline commands, bare PATH binaries, or unresolved templated paths → UI
  shows the note instead of a file view.

### 3. Editability helper
`HookEditability(scope, sourceFile) → Editable | ReadOnly(reason)`: editable when
`scope ∈ {User, Project, Local}` and `sourceFile` is a writable `settings*.json` not under a
plugin/managed path; otherwise read-only with a reason ("plugin-provided", "managed/enterprise").

## Testing

Deterministic, no real machine state.
- **Core (TDD):** `HookBlockEditor` extract/splice — multi-hook group, duplicate groups by
  index, invalid-JSON refusal, wrong-shape refusal, index-out-of-range, whole-file preservation.
  `HookScriptResolver` — node/python/bash scripts resolved against each base, plugin-templated →
  null, inline command → null, PATH binary → null, extension→language mapping. Editability rule
  across scopes/paths.
- **App:** `MatcherChips` (`*`→any, pipe list→tokens, regex token passthrough); `HooksViewModel`
  edit flow (begin/preview/apply/undo, read-only path blocks save, invalid JSON surfaces error,
  index-based selection).
- **No render tests** (Photino headless-unverifiable); visual fidelity via `/run` + the mockups.

## Build order (suggested)
1. highlight.js wiring + `CodeViewer` `Language` + scroll cap (app-wide, low risk).
2. Row redesign (Option A) + `MatcherChips`.
3. Inline accordion, **read-only** (JSON block + resolved file view) — `HookScriptResolver`.
4. Editing path — `HookBlockEditor` + VM save/undo + safe-mutation wiring.

## Out of scope (v1)
- Live token-coloring **while editing** (needs CodeMirror) — formatted textarea only.
- Project/Local **override** for hooks (union-merge duplicate semantics) — edit-defining-file only.
- Editing plugin / enterprise hooks (read-only).
- Adding / deleting hooks (edit existing only).
- The broader **markdown editor** for CLAUDE.md / skills / commands / subagents — separate
  follow-up; this phase lays the highlight.js groundwork it will reuse.
