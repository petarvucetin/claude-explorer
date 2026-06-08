# Artifact Split + Real MCP/Plugins Screens — Design

**Status:** Approved 2026-06-08. Mockup: `ux-explorations/11-blueprint-artifact-split.html`
(screenshots `shot-11-*.png`).

## Goal

Replace the two combined left-rail items — **Commands & Skills** and **MCP & Plugins** (the latter
a placeholder stub) — with **five separate nav items, each with its own bespoke right-hand view**:
Commands, Skills, Subagents, MCP, Plugins. MCP and Plugins become real, data-backed screens.

## Motivation / real-data findings (this machine)

- **Everything is plugin-sourced.** No user/project commands or subagents exist on disk; they come
  from plugins (e.g. `feature-dev` → 1 command + 3 subagents, `code-review` → 1 command). 22 skills
  (2 user: graphify, grill-me; 20 plugin). This is why the recent plugin-layer discovery fix
  ([[CLA-98]]) was a prerequisite.
- **MCP servers are plugin-provided** via each plugin's `.mcp.json`, in a **name-at-root** shape
  (`{ "linear": { "type":"http", "url":… } }`) — *not* the `mcpServers`-wrapped shape the current
  `McpServerReader` expects. Real servers: `linear` (http), `playwright` (stdio `npx @playwright/mcp`),
  `unifi-network` (stdio).
- **Remote MCP connectors** (`claude.ai_*`: Figma, MS Learn, Gmail, …) are account-managed and not in
  any local file → **out of scope**; the MCP screen shows locally-configured servers only and says so.
- **Plugins** carry rich data: `installed_plugins.json` (version, scope, installPath), 3 known
  marketplaces (`claude-plugins-official` = Verified; `unifi-plugins`, `context-mode` = Community),
  and per-plugin "provides" counts (superpowers → 14 skills + 4 hooks, etc.).

## Information architecture (left rail)

`LeftRail.razor` regroups into labeled sections; `Commands & Skills` and `MCP & Plugins` items are
removed:

- **Workspace** — Dashboard, Effective Config, Dependencies
- **Config Artifacts** — Commands, Skills, Subagents
- **Extensions** — MCP, Plugins
- **Analyze** — Compare
- **Discover** — Marketplace, Recommended, Change Log

Routes: `/commands`, `/skills`, `/subagents`, `/mcp`, `/plugins`. The old `/commands`
(CommandsSkills) and `McpStub` are removed.

## The five views (bespoke per type)

All reuse the Blueprint vocabulary (corner-tick panels, source/trust badges, dark code viewer,
open/reveal/copy actions). Each is read-only (no edits/installs this phase).

1. **Commands** — source-grouped master list + detail. Hero = mono `/invocation`; detail shows
   `argument-hint`, source, file path, and the command markdown body.
2. **Skills** — master/detail. Hero = the **description (recall trigger)**; a `/invocable` badge marks
   user-triggerable skills; detail shows source, extra-files count, and the SKILL.md body.
3. **Subagents** — master/detail. Hero = **tool chips** (which tools the agent may use, with
   denied ones shown struck) + model; detail shows the system-prompt body.
4. **MCP** — server rows: transport badge (HTTP/STDIO/SSE), endpoint (url or command+args), source
   plugin, and a **health pill** (stdio runtime found/missing, reusing the dependency check; http/sse
   shown `remote · n/a`). Detail = full config + resolved runtime + source file.
5. **Plugins** — a **marketplaces strip** (Verified/Community trust) + plugin cards: version, trust,
   enabled state (from `enabledPlugins`), and a **"provides" line** (e.g. `14 skills · 4 hooks`).

## Core changes

### 1. Enrich discovered artifacts with frontmatter (`Artifacts/`)
The bespoke detail panes need fields beyond name/summary. Extend `DiscoveredArtifact` with the parsed
frontmatter so mappers can surface type-specific fields:
- Commands: `argument-hint`
- Subagents: `tools` (comma/space list → chips), `model`
- Skills: user-invocable detection (a command-style trigger) + count of sibling files in the skill dir.

Add `IReadOnlyDictionary<string,string> Frontmatter` to `DiscoveredArtifact` (filled by
`ArtifactDiscoverer` from `Frontmatter.Parse`), plus a `int ExtraFileCount` for skills (count of
non-`SKILL.md` files in the skill dir). Existing call sites keep working (new fields are additive).

### 2. Real MCP reader (`Dependencies/` or new `Mcp/`)
Introduce a richer model + reader for the MCP **screen** (the existing minimal `McpServer` stays for
dependency health, or is widened — see plan):
- `McpServerInfo(Name, Transport {Stdio|Http|Sse}, Command?, Args, Url?, Env, McpSource)` where
  `McpSource` carries scope or plugin name + the defining file path.
- Reader scans: located settings files (`mcpServers` wrapper), project `.mcp.json` (wrapper),
  `~/.claude.json` (wrapper, if present), **and each installed plugin's `.mcp.json` (name-at-root)**
  via `InstalledPluginLocator`. Malformed/missing skipped.
- Health: reuse `DependencyHealthService`/`DependencyChecker` for stdio servers; http/sse are `n/a`.

### 3. Plugin inventory reader (`Catalog/` or new `Plugins/`)
`PluginInventoryReader.Read(userDir)` composing:
- `installed_plugins.json` → name, marketplace (`name@marketplace` key), version, scope, installPath.
- `known_marketplaces.json` + `InstalledMarketplaceReader` → marketplace source/trust (Verified/Community).
- Per-plugin "provides": reuse `InstalledPluginLocator` + `ArtifactDiscoverer` (+ hooks/.mcp.json
  presence) to count commands/skills/subagents/hooks/mcp.
- `enabledPlugins` from user `settings.json` → enabled flag.
Returns `PluginInventory(IReadOnlyList<InstalledPluginInfo>, IReadOnlyList<MarketplaceInfo>)`.

### 4. ViewModels + views + Shell
- New screens: `CommandsViewModel`/`Commands.razor`, `SkillsViewModel`/`Skills.razor`,
  `SubagentsViewModel`/`Subagents.razor`, `McpViewModel`/`Mcp.razor`, `PluginsViewModel`/`Plugins.razor`.
  Commands/Skills/Subagents reuse `ArtifactCatalogService` (already plugin-aware) filtered by kind via
  a pure mapper; MCP/Plugins use the new readers. Each = pure tested mapper + `ObservableObject` VM +
  Blueprint view; all follow `IWorkspaceContext` (active environment).
- `ShellViewModel`: replace `CommandsAndSkills` with per-type counts (`Commands`, `Skills`,
  `Subagents`, `Mcp`, `Plugins`) + `HasMcpProblem`/`HasDependencyProblem` retained.
- `LeftRail.razor`: regroup + 5 items; remove old two. Remove `McpStub.razor` + old `CommandsSkills.razor`.
- DI in `Program.cs`: register the new readers + VMs.

## Testing

Deterministic Core tests (fixture `.claude` trees) for: artifact frontmatter enrichment, the MCP
reader (all four sources incl. plugin name-at-root format + http/stdio), and the plugin inventory
reader (provides counts, trust classification, enabled flag). App tests for each screen's pure mapper
+ VM load/filter/select, and the updated `ShellViewModel` counts. No rendering tests (Photino is
headless-unverifiable; visual fidelity needs a human `/run`).

## Out of scope (this phase)

- Enable/disable plugins, install/uninstall, edit MCP config — read-only screens now; mutation is a
  later phase via the safe-mutation layer / `claude` CLI.
- Remote (account-managed) MCP connectors — not on disk, can't be read locally.
- Built-in (CLI-baked) commands/subagents — not enumerable.
