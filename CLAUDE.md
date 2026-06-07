# Claude Explorer

## Overview

Claude Explorer is a cross-platform desktop app that helps users **discover**, **safely
edit**, **install**, and get **recommendations** for the settings and tooling that affect
Claude Code's behavior.

It answers the question *"Why does Claude behave this way here — and what can I change?"*
by computing the **effective, merged configuration** for a given scope and showing the
**provenance** of every setting (which file/scope wins, what it overrides, and where it
lives on disk), with deep links back to the source.

Pillars:

1. **Discover** — find and present all Claude config across scopes, merged into an
   effective view with provenance.
2. **Safely edit** — change config with diff preview, backups, undo/restore, and a
   reviewable change log.
3. **Install** — browse a catalog (Claude's marketplaces + user-added sources) and install
   skills/agents/plugins safely.
4. **Recommend** — analyze a project locally and suggest skills/agents that fit it, with
   evidence for *why*.

## Goals & Non-Goals

**Goals (v1)**
- Compute effective/merged config with full source attribution and conflict detection.
- Cover the core Claude config surface (see *Artifact Scope*).
- Verify that the config actually works on this machine (dependency health check).
- Apply edits and installs safely and reversibly.
- Browse/install from Claude's marketplaces **and** user-added sources, with a trust model.
- Recommend skills/agents for a project from local analysis.
- Polished, easy-to-use UI with zero paid component dependencies.

**Non-Goals (v1)**
- Not a broad machine/package inventory tool — dependency discovery is **config-driven only**.
- Not an ecosystem auto-scraper — the catalog is Claude's marketplaces plus sources the
  **user explicitly adds**; we don't crawl the open ecosystem.
- Project recommendation analysis is **local-only** — it never uploads code or project
  contents anywhere (only catalog *metadata* is fetched).
- No telemetry; no cloud backend. The app is fully local.
- No mobile/iOS target. "Desktop" means Windows, macOS, Linux.

## Target Platforms

- Windows, macOS, and Linux from v1 (single codebase, true cross-platform).
- Per-OS handling of Claude config locations (e.g. `~/.claude`, project `.claude/`,
  enterprise/managed settings) and PATH resolution.

## Architecture

### Stack
- **Backend / runtime:** .NET 10.
- **Shell:** Photino (lightweight native window using the OS webview — WebView2 /
  WKWebView / WebKitGTK).
- **UI:** Blazor + **MudBlazor** (free/MIT component library). Fluent UI Blazor or Radzen
  are acceptable free fallbacks if a component is missing.
- **UI pattern:** **MVVM** — observable ViewModels hold view state and commands; Blazor
  components (Views) bind to ViewModels and stay logic-light. ViewModels depend on Core
  services via DI and are unit-testable without rendering.
- **No JS build toolchain and no paid UI controls.**
- The chosen visual direction (**Blueprint**) needs **custom CSS/theming** over MudBlazor;
  budget for it (see *Visual Design*).

### Project structure
- **Core domain library** — UI-agnostic and unit-testable. Houses the engines below plus
  file parsers and the safe-mutation layer.
- **Photino.Blazor UI app** — references Core; owns Views (Blazor components) + ViewModels,
  navigation, and viewers.
- **Test project** — covers the engines and parsers (fixture `.claude` dirs) plus ViewModel
  logic. Deterministic; never touches real machine state.

### Engines (in Core)
- **Discovery** — locate and read every config source across scopes/OSes.
- **Merge / precedence** — deep-merge sources into effective config with provenance; knows
  per-key semantics (scalar last-wins vs list/array merge vs deep-merge).
- **Dependency extraction + health** — pull executables out of hooks/MCP/commands and probe
  them safely.
- **Catalog** — read Claude marketplaces and user-added sources; normalize item metadata
  with a trust level.
- **Project-fit / recommendation** — local signal detection + matcher against catalog,
  minus what's installed, annotated with dependency health.
- **Safe-mutation** — backup, validate, apply (CLI or file write), log, undo.

## Core Concepts

- **Scope** — a configuration source level: enterprise/managed → user global (`~/.claude`)
  → project → project-local. The app always loads user-global + machine context; the user
  can open **multiple projects side-by-side and compare them**.
- **Effective config** — the deep-merged result of all in-scope sources, i.e. what Claude
  actually does.
- **Provenance** — for each effective setting: which scope/file set its value, what it
  overrode, conflicts, and the resolved absolute path + line.
- **Trust level** — every catalog source/item is **verified** (official Anthropic) or
  **community** (user-added). Surfaced everywhere an item appears.
- **Signal** — a locally-detected fact about a project (language, framework, test runner,
  DB, issue-tracker refs, commit patterns) used to drive recommendations.

## Features

### 1. Discover — effective config + provenance
The primary view. Computes the merged config per project and shows, for every setting, the
winning source, overridden values, conflicts, and a link to the source location. Rendered
as a **precedence matrix**: rows = settings, columns = scopes, the winning cell is wired
across to an Effective column, overridden cells struck through, **merge-vs-override
semantics labeled per row**. Expanding a row shows the full provenance trace + a read-only
source preview + edit/open/reveal/copy actions.

**Artifact scope (v1):**
- **settings.json surface** — permissions (allow/deny/ask), env vars, hooks, model,
  statusline, output styles. Densest precedence/deep-merge logic; core of "effective config."
- **CLAUDE.md memory files** — global, project, and nested, shown in load order.
- **Slash commands + skills + subagents** — discovered from user/project/plugin dirs,
  **categorized by source (built-in vs user vs plugin)**, with name-collision/override
  resolution ("shadowed"/"overrides") and a short summary of each. Presented as a
  source-grouped master/detail browser.
- **MCP servers + plugins/marketplaces** — definitions from `.mcp.json`, settings, and
  `~/.claude.json`; installed plugins/marketplaces with scope and enabled/disabled state.

### 2. Dependency health check (config-driven)
Parse every discovered hook / MCP server / command, extract the executables it depends on
(`npx`, `uvx`, `uv`, `node`, `python`, `docker`, `podman`, `git`, `claude`, or any
referenced binary), and verify each is **present, resolvable on PATH, with version**.
Answers *"will this config actually work here, and what's broken?"*

**Probe safety (hard requirement):** resolve executables and run only `--version`-style
probes against a known **allowlist** of runtimes. **Never execute the actual hook/MCP
command or any arbitrary discovered binary.** Report found / missing / version per dep.

### 3. Install — catalog + sources
Browse installable plugins/skills/agents from multiple **sources**:
- **Claude's configured marketplaces** (official Anthropic = *verified*; community
  marketplaces the user added).
- **User-added sources** — a marketplace URL, a GitHub repo (plugins or a bare skills
  folder), or `owner/repo`. The tool **detects** the source type, validates the manifest,
  and adds it with a **community** trust level.

**Hard rules:**
- **Metadata-only until install** — adding/browsing a source fetches only catalog metadata;
  nothing is downloaded or executed until the user explicitly installs an item.
- Community sources are clearly marked and carry a trust warning.
- Install shows **contents**, an **install-to scope** choice, a **dependency check** (warn
  if a required runtime is missing), and routes through the safe-mutation layer.

### 4. Recommend — project fit
A Marketplace tab: **"Recommended for <project>."** Locally analyzes the open project into
**signals**, matches them against catalog metadata, removes anything already installed, and
ranks results with a **match-confidence** score. Sections: *Strong matches / Worth
considering / Already covered.*

**Requirements:**
- Every recommendation must state **why** with **evidence** (e.g. `playwright.config.ts`,
  `9 × migrations/*.sql`) — evidence chips link back to the source file (reuses the
  provenance/source-link pattern). Recommendations without a traceable reason are not shown.
- Analysis is **local-only**; only catalog metadata leaves the machine.
- Dependency health annotates recommendations (e.g. "needs `uvx`, currently missing").

## Mutation & Safety Model

All changes (edits and installs) go through one safe-mutation layer.

**Mechanism (hybrid):**
- **Installs / marketplace ops** → delegate to the `claude` CLI (authoritative). Detected by
  the health check; if absent, discovery/editing still work and install degrades gracefully.
- **Config edits** (settings.json, CLAUDE.md, etc.) → direct file writes.

**Safety contract (all required for v1):**
- **Scope-target picker** — when editing a value, the user explicitly chooses *where* the
  write lands: edit the **winning** source, or **override** at Project / Local. The UI warns
  that editing a global/winning value affects all projects. (Prevents the most common and
  most damaging mistake in a multi-scope editor.)
- **Diff preview + explicit confirm** — show the exact before/after; no silent writes.
- **Schema/syntax validation** — validate settings JSON (schema) and markdown frontmatter
  before writing; never corrupt a config file.
- **Automatic timestamped backups** before every write/install.
- **One-click undo / restore** (uninstall for installs), referencing the exact backup.
- **Reviewable, scope-aware change/install log** — every mutation recorded and grouped by
  scope, easy to review and roll back at the right level.

## Visual Design — "Blueprint"

The selected direction is **Blueprint**: a cool graph-paper schematic aesthetic that makes
the product's core idea — **tracing where configuration comes from** — literal. Working
prototypes live in `ux-explorations/` (open the HTML; screenshots `shot-*.png`).

- **Backdrop:** faint graph-paper grid; panels carry drafting **corner ticks**.
- **Type:** `Archivo` (display/UI, heavy uppercase headings) + `Spline Sans Mono` (values,
  paths, metadata). Bundle fonts in the app (don't depend on a CDN at runtime).
- **Color:** ink `#16202E` on paper `#EEF1F5`; electric blue `#1F47D6` accent; status amber
  `#B5710C` / red `#C2362B` / green `#2E7D4F`. (Full tokens in the prototypes.)
- **Provenance as wiring:** winning values are visually "wired" to their effective result;
  arrows trace scope→scope resolution.
- **Reusable component vocabulary:** corner-tick panel, source card, dark read-only
  code/diff viewer, type/trust badges, scope tags, action-button row, match-confidence bar,
  3-step flow filmstrip (compose → preview/confirm → applied/undo).
- Quality bar: **super polished and easy to use** with free components + custom styling.
- *Note:* Blueprint is the furthest from MudBlazor defaults of the directions explored — it
  is achievable but is real CSS work.

## UX / Information Architecture

- **Left rail = feature areas:** Dashboard, Effective Config, Commands & Skills,
  MCP & Plugins, Dependencies, Marketplace (Browse / Recommended / Installed), Change Log.
- **Top bar = persistent project/scope selector** (global + open projects, compare) + refresh.
- **Landing = health dashboard:** environment health, counts, conflicts/warnings, recent changes.
- **Source links:** clicking a discovered item opens an in-app, syntax-highlighted,
  read-only viewer at the exact line, plus **Open in editor**, **Reveal in file manager**,
  **Copy path**.
- **Live refresh:** watch config files and refresh automatically; manual refresh always available.

**Validated screens/flows (prototyped in `ux-explorations/`):**
- `01–03` — three design directions (Atelier / Observatory / Blueprint). **Blueprint chosen.**
- `04` — Effective Config precedence matrix + provenance trace.
- `05` — Commands & Skills source-grouped browser (with override/shadow resolution).
- `06` — Safe-edit & undo flow (scope target → diff/backup → applied + undo).
- `07` — Marketplace browse with multi-source + trust badges.
- `08` — Add Source & Install flow (detect → trust warning → install w/ dep check → undo).
- `09` — Recommended-for-project (signals → why+evidence → install).

## Packaging & Distribution

- **v1:** portable per-OS artifacts (zip/folder, no installer, no signing required —
  nothing installs, so no "unknown developer" warnings).
- Build pipeline is structured so **code signing/notarization and store publishing
  (Microsoft Store / Mac App Store) can be added later** without rework.

## Privacy

Fully local. Network access only for: catalog metadata, installs (via `claude` CLI), and
version/update checks. Project analysis is local-only — code is never uploaded. No telemetry.

## Testing

Deterministic unit tests for the merge/precedence engine, all parsers, the recommendation
matcher, and ViewModel logic — driven by fixture `.claude` directories and fixture project
trees. Core must be testable without touching the real user machine.
