# Artifact Discovery (Commands / Skills / Subagents) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend `ClaudeExplorer.Core` to discover file-based Claude artifacts — slash commands, skills, and subagents — across User / Project / Plugin sources, parse their frontmatter, extract a short summary, and resolve name-collision shadowing into a single categorized catalog.

**Architecture:** Builds on the Phase 1 Core library. Adds an `Artifacts` namespace: a frontmatter parser, a summary extractor, a domain model, an `ArtifactDiscoverer` (walks the filesystem via the existing `IFileSystem` seam, now extended with directory enumeration), an `ArtifactResolver` (groups by kind+name, applies source precedence Project>User>Plugin), and an `ArtifactCatalogService` façade. Fully fixture-driven via `InMemoryFileSystem`.

**Tech Stack:** .NET 10, C#, `System.Text.Json` (already referenced), xUnit. No new NuGet dependencies (frontmatter parsed by hand — no YAML library).

---

## Scope & decisions

- **Sources discovered:** `User` (`~/.claude/...`), `Project` (`<proj>/.claude/...`), `Plugin` (caller supplies plugin name + root dir; no `.claude` prefix). **Built-in** commands are NOT files on disk → out of scope (a bundled list is future work).
- **Locations per scope:** `commands/**/*.md` (recursive), `skills/<name>/SKILL.md`, `agents/*.md`.
- **Name:** frontmatter `name:` if present, else the file/dir basename (without extension).
- **Summary:** frontmatter `description:` if present, else the first non-empty, non-heading body line, else null.
- **Precedence (for shadowing):** `Project (2) > User (1) > Plugin (0)`. Per `(Kind, Name)` group, highest-precedence wins; the rest are `Shadowed`. (Modeled precedence — refine against real Claude behavior later; pinned by tests.)
- **Frontmatter parser** handles a leading `---\n … \n---` block of `key: value` lines (quotes stripped); no nested YAML/lists. Good enough for `name`/`description`; documented limitation.

## File structure

- `src/ClaudeExplorer.Core/Io/IFileSystem.cs` — **modify**: add `DirectoryExists`, `GetDirectories`, `GetFiles`; implement in `PhysicalFileSystem`.
- `tests/ClaudeExplorer.Core.Tests/Fakes/InMemoryFileSystem.cs` — **modify**: implement the new members.
- `src/ClaudeExplorer.Core/Artifacts/Frontmatter.cs` — parser + `FrontmatterResult`.
- `src/ClaudeExplorer.Core/Artifacts/ArtifactSummary.cs` — summary extractor.
- `src/ClaudeExplorer.Core/Artifacts/ArtifactModel.cs` — enums + records + catalog.
- `src/ClaudeExplorer.Core/Artifacts/ArtifactDiscoverer.cs` — discovery + `PluginLocation`.
- `src/ClaudeExplorer.Core/Artifacts/ArtifactResolver.cs` — shadow resolution.
- `src/ClaudeExplorer.Core/Artifacts/ArtifactCatalogService.cs` — façade.
- Tests under `tests/ClaudeExplorer.Core.Tests/Artifacts/`.

---

## Task 1: Extend IFileSystem with directory enumeration

**Files:**
- Modify: `src/ClaudeExplorer.Core/Io/IFileSystem.cs`
- Modify: `tests/ClaudeExplorer.Core.Tests/Fakes/InMemoryFileSystem.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Fakes/InMemoryFileSystemDirTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Fakes/InMemoryFileSystemDirTests.cs`:
```csharp
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Fakes;

public class InMemoryFileSystemDirTests
{
    private static InMemoryFileSystem Fs() => new InMemoryFileSystem()
        .AddFile("/u/.claude/commands/a.md", "a")
        .AddFile("/u/.claude/commands/sub/b.md", "b")
        .AddFile("/u/.claude/commands/c.txt", "c")
        .AddFile("/u/.claude/skills/alpha/SKILL.md", "s");

    [Fact]
    public void DirectoryExists_is_true_when_a_file_lives_under_it()
    {
        var fs = Fs();
        Assert.True(fs.DirectoryExists("/u/.claude/commands"));
        Assert.False(fs.DirectoryExists("/u/.claude/nope"));
    }

    [Fact]
    public void GetDirectories_returns_immediate_children_only()
    {
        var fs = Fs();
        Assert.Equal(new[] { "/u/.claude/skills/alpha" }, fs.GetDirectories("/u/.claude/skills"));
    }

    [Fact]
    public void GetFiles_filters_by_pattern_and_recursion()
    {
        var fs = Fs();
        Assert.Equal(new[] { "/u/.claude/commands/a.md" },
            fs.GetFiles("/u/.claude/commands", "*.md", recurse: false));
        Assert.Equal(new[] { "/u/.claude/commands/a.md", "/u/.claude/commands/sub/b.md" },
            fs.GetFiles("/u/.claude/commands", "*.md", recurse: true));
    }

    [Fact]
    public void GetFiles_on_missing_dir_is_empty()
    {
        Assert.Empty(Fs().GetFiles("/missing", "*.md", recurse: true));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter InMemoryFileSystemDirTests`
Expected: FAIL — `DirectoryExists`/`GetDirectories`/`GetFiles` don't exist (compile error).

- [ ] **Step 3: Write minimal implementation**

Replace the contents of `src/ClaudeExplorer.Core/Io/IFileSystem.cs` with:
```csharp
namespace ClaudeExplorer.Core.Io;

public interface IFileSystem
{
    bool FileExists(string path);
    string ReadAllText(string path);
    bool DirectoryExists(string path);

    /// <summary>Immediate child directories of <paramref name="path"/> (full paths). Empty if the dir is missing.</summary>
    IReadOnlyList<string> GetDirectories(string path);

    /// <summary>
    /// Files under <paramref name="path"/> matching a simple pattern ("*", "*.md", or an exact name).
    /// Recurses into subdirectories when <paramref name="recurse"/> is true. Empty if the dir is missing.
    /// </summary>
    IReadOnlyList<string> GetFiles(string path, string searchPattern, bool recurse);
}

public sealed class PhysicalFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);
    public string ReadAllText(string path) => File.ReadAllText(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IReadOnlyList<string> GetDirectories(string path)
        => Directory.Exists(path)
            ? Directory.GetDirectories(path).Select(Normalize).ToList()
            : Array.Empty<string>();

    public IReadOnlyList<string> GetFiles(string path, string searchPattern, bool recurse)
        => Directory.Exists(path)
            ? Directory.GetFiles(path, searchPattern,
                    recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Select(Normalize).ToList()
            : Array.Empty<string>();

    private static string Normalize(string p) => p.Replace('\\', '/');
}
```

Then add the new members to `tests/ClaudeExplorer.Core.Tests/Fakes/InMemoryFileSystem.cs`. Insert these methods inside the class (after `ReadAllText`):
```csharp
    public bool DirectoryExists(string path)
    {
        var prefix = Normalize(path).TrimEnd('/') + "/";
        return _files.Keys.Any(k => k.StartsWith(prefix, StringComparison.Ordinal));
    }

    public IReadOnlyList<string> GetDirectories(string path)
    {
        var prefix = Normalize(path).TrimEnd('/') + "/";
        var dirs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var k in _files.Keys)
        {
            if (!k.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var rest = k.Substring(prefix.Length);
            var slash = rest.IndexOf('/');
            if (slash >= 0) dirs.Add(prefix + rest.Substring(0, slash));
        }
        return dirs.OrderBy(d => d, StringComparer.Ordinal).ToList();
    }

    public IReadOnlyList<string> GetFiles(string path, string searchPattern, bool recurse)
    {
        var prefix = Normalize(path).TrimEnd('/') + "/";
        var results = new List<string>();
        foreach (var k in _files.Keys)
        {
            if (!k.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var rest = k.Substring(prefix.Length);
            if (!recurse && rest.Contains('/')) continue;
            var name = rest.Contains('/') ? rest.Substring(rest.LastIndexOf('/') + 1) : rest;
            if (MatchesPattern(name, searchPattern)) results.Add(k);
        }
        results.Sort(StringComparer.Ordinal);
        return results;
    }

    private static bool MatchesPattern(string name, string pattern)
    {
        if (pattern is "*" or "*.*") return true;
        if (pattern.StartsWith("*.", StringComparison.Ordinal))
            return name.EndsWith(pattern.Substring(1), StringComparison.OrdinalIgnoreCase);
        return string.Equals(name, pattern, StringComparison.OrdinalIgnoreCase);
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS — the new dir tests pass and all prior Phase 1 tests still pass.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): extend IFileSystem with directory enumeration"
```

---

## Task 2: Frontmatter parser

**Files:**
- Create: `src/ClaudeExplorer.Core/Artifacts/Frontmatter.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Artifacts/FrontmatterTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Artifacts/FrontmatterTests.cs`:
```csharp
using ClaudeExplorer.Core.Artifacts;

namespace ClaudeExplorer.Core.Tests.Artifacts;

public class FrontmatterTests
{
    [Fact]
    public void Parses_fields_and_body_stripping_quotes()
    {
        var content = "---\nname: graphify\ndescription: \"turn input into a graph\"\n---\n# Heading\nbody text\n";
        var fm = Frontmatter.Parse(content);

        Assert.Equal("graphify", fm.Fields["name"]);
        Assert.Equal("turn input into a graph", fm.Fields["description"]);
        Assert.Contains("body text", fm.Body);
        Assert.DoesNotContain("name:", fm.Body);
    }

    [Fact]
    public void Field_lookup_is_case_insensitive()
    {
        var fm = Frontmatter.Parse("---\nName: x\n---\n");
        Assert.Equal("x", fm.Fields["name"]);
    }

    [Fact]
    public void No_frontmatter_returns_empty_fields_and_whole_body()
    {
        var fm = Frontmatter.Parse("# Just a title\ncontent");
        Assert.Empty(fm.Fields);
        Assert.Contains("Just a title", fm.Body);
    }

    [Fact]
    public void Handles_crlf_line_endings()
    {
        var fm = Frontmatter.Parse("---\r\nname: y\r\n---\r\nbody\r\n");
        Assert.Equal("y", fm.Fields["name"]);
        Assert.Contains("body", fm.Body);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter FrontmatterTests`
Expected: FAIL — `Frontmatter`/`FrontmatterResult` don't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Artifacts/Frontmatter.cs`:
```csharp
namespace ClaudeExplorer.Core.Artifacts;

/// <summary>Result of parsing a markdown file's YAML-style frontmatter.</summary>
public sealed record FrontmatterResult(IReadOnlyDictionary<string, string> Fields, string Body);

/// <summary>
/// Minimal frontmatter parser: reads a leading <c>---</c>…<c>---</c> block of `key: value`
/// lines (surrounding quotes stripped). Does not support nested YAML, lists, or multi-line values.
/// </summary>
public static class Frontmatter
{
    public static FrontmatterResult Parse(string? content)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(content))
            return new FrontmatterResult(fields, content ?? "");

        var text = content.Replace("\r\n", "\n").Replace("\r", "\n");
        if (!text.StartsWith("---\n", StringComparison.Ordinal))
            return new FrontmatterResult(fields, text);

        var close = text.IndexOf("\n---", 4, StringComparison.Ordinal);
        if (close < 0)
            return new FrontmatterResult(fields, text);

        var block = text.Substring(4, close - 4);
        var nl = text.IndexOf('\n', close + 1);
        var body = nl >= 0 ? text.Substring(nl + 1) : "";

        foreach (var rawLine in block.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            var key = line.Substring(0, colon).Trim();
            var value = Unquote(line.Substring(colon + 1).Trim());
            if (key.Length > 0 && !fields.ContainsKey(key))
                fields[key] = value;
        }

        return new FrontmatterResult(fields, body);
    }

    private static string Unquote(string s)
        => s.Length >= 2 && ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\''))
            ? s.Substring(1, s.Length - 2)
            : s;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter FrontmatterTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): markdown frontmatter parser"
```

---

## Task 3: Artifact domain model

**Files:**
- Create: `src/ClaudeExplorer.Core/Artifacts/ArtifactModel.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Artifacts/ArtifactModelTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Artifacts/ArtifactModelTests.cs`:
```csharp
using ClaudeExplorer.Core.Artifacts;

namespace ClaudeExplorer.Core.Tests.Artifacts;

public class ArtifactModelTests
{
    [Fact]
    public void Source_label_and_precedence()
    {
        Assert.Equal("User", new ArtifactSource(ArtifactSourceKind.User).Label);
        Assert.Equal("Plugin: superpowers", new ArtifactSource(ArtifactSourceKind.Plugin, "superpowers").Label);
        Assert.True(new ArtifactSource(ArtifactSourceKind.Project).Precedence
                    > new ArtifactSource(ArtifactSourceKind.User).Precedence);
        Assert.True(new ArtifactSource(ArtifactSourceKind.User).Precedence
                    > new ArtifactSource(ArtifactSourceKind.Plugin, "x").Precedence);
    }

    [Fact]
    public void Resolved_artifact_reports_shadowing_and_catalog_filters_by_kind()
    {
        var win = new DiscoveredArtifact(ArtifactKind.Command, "review", "sum",
            new ArtifactSource(ArtifactSourceKind.Project), "/p/.claude/commands/review.md");
        var resolved = new ResolvedArtifact(win, Array.Empty<DiscoveredArtifact>());
        Assert.False(resolved.IsShadowing);

        var catalog = new ArtifactCatalog(new[] { resolved });
        Assert.Single(catalog.OfKind(ArtifactKind.Command));
        Assert.Empty(catalog.OfKind(ArtifactKind.Skill));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter ArtifactModelTests`
Expected: FAIL — types don't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Artifacts/ArtifactModel.cs`:
```csharp
namespace ClaudeExplorer.Core.Artifacts;

public enum ArtifactKind { Command, Skill, Subagent }

public enum ArtifactSourceKind { User, Project, Plugin }

public sealed record ArtifactSource(ArtifactSourceKind Kind, string? PluginName = null)
{
    public string Label => Kind == ArtifactSourceKind.Plugin ? $"Plugin: {PluginName}" : Kind.ToString();

    /// <summary>Higher wins when the same (Kind, Name) appears in multiple sources.</summary>
    public int Precedence => Kind switch
    {
        ArtifactSourceKind.Project => 2,
        ArtifactSourceKind.User => 1,
        _ => 0,
    };
}

public sealed record DiscoveredArtifact(
    ArtifactKind Kind,
    string Name,
    string? Summary,
    ArtifactSource Source,
    string FilePath);

public sealed record ResolvedArtifact(DiscoveredArtifact Winner, IReadOnlyList<DiscoveredArtifact> Shadowed)
{
    public bool IsShadowing => Shadowed.Count > 0;
}

public sealed record ArtifactCatalog(IReadOnlyList<ResolvedArtifact> Artifacts)
{
    public IEnumerable<ResolvedArtifact> OfKind(ArtifactKind kind)
        => Artifacts.Where(a => a.Winner.Kind == kind);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter ArtifactModelTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): artifact domain model"
```

---

## Task 4: Summary extractor

**Files:**
- Create: `src/ClaudeExplorer.Core/Artifacts/ArtifactSummary.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Artifacts/ArtifactSummaryTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Artifacts/ArtifactSummaryTests.cs`:
```csharp
using ClaudeExplorer.Core.Artifacts;

namespace ClaudeExplorer.Core.Tests.Artifacts;

public class ArtifactSummaryTests
{
    [Fact]
    public void Prefers_frontmatter_description()
    {
        var fm = Frontmatter.Parse("---\ndescription: the summary\n---\n# Title\nbody");
        Assert.Equal("the summary", ArtifactSummary.Extract(fm));
    }

    [Fact]
    public void Falls_back_to_first_non_heading_body_line()
    {
        var fm = Frontmatter.Parse("---\nname: x\n---\n# Title\n\nFirst real line.\nsecond");
        Assert.Equal("First real line.", ArtifactSummary.Extract(fm));
    }

    [Fact]
    public void Strips_leading_hashes_when_only_headings_exist()
    {
        var fm = Frontmatter.Parse("## Only A Heading");
        Assert.Equal("Only A Heading", ArtifactSummary.Extract(fm));
    }

    [Fact]
    public void Returns_null_when_empty()
    {
        var fm = Frontmatter.Parse("---\nname: x\n---\n   \n");
        Assert.Null(ArtifactSummary.Extract(fm));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter ArtifactSummaryTests`
Expected: FAIL — `ArtifactSummary` doesn't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Artifacts/ArtifactSummary.cs`:
```csharp
namespace ClaudeExplorer.Core.Artifacts;

public static class ArtifactSummary
{
    /// <summary>Frontmatter `description`, else the first non-empty non-heading body line, else null.</summary>
    public static string? Extract(FrontmatterResult frontmatter)
    {
        if (frontmatter.Fields.TryGetValue("description", out var description)
            && !string.IsNullOrWhiteSpace(description))
            return description.Trim();

        foreach (var rawLine in frontmatter.Body.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("#", StringComparison.Ordinal))
                line = line.TrimStart('#').Trim();
            if (line.Length > 0)
                return line;
        }

        return null;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter ArtifactSummaryTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): artifact summary extraction"
```

---

## Task 5: Discover commands (User + Project scopes)

**Files:**
- Create: `src/ClaudeExplorer.Core/Artifacts/ArtifactDiscoverer.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Artifacts/ArtifactDiscovererCommandTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Artifacts/ArtifactDiscovererCommandTests.cs`:
```csharp
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Artifacts;

public class ArtifactDiscovererCommandTests
{
    [Fact]
    public void Discovers_commands_from_user_and_project_with_name_and_summary()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/commands/standup.md", "---\ndescription: daily standup\n---\nbody")
            .AddFile("/repo/.claude/commands/deploy.md", "# Deploy\nDeploys the app.");

        var found = new ArtifactDiscoverer(fs)
            .Discover("/home", "/repo", Array.Empty<PluginLocation>());

        var standup = found.Single(a => a.Name == "standup");
        Assert.Equal(ArtifactKind.Command, standup.Kind);
        Assert.Equal(ArtifactSourceKind.User, standup.Source.Kind);
        Assert.Equal("daily standup", standup.Summary);

        var deploy = found.Single(a => a.Name == "deploy");
        Assert.Equal(ArtifactSourceKind.Project, deploy.Source.Kind);
        Assert.Equal("Deploys the app.", deploy.Summary);
    }

    [Fact]
    public void Command_name_uses_filename_when_no_frontmatter_name()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/commands/nested/thing.md", "x");
        var found = new ArtifactDiscoverer(fs).Discover("/home", null, Array.Empty<PluginLocation>());
        Assert.Equal("thing", found.Single().Name);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter ArtifactDiscovererCommandTests`
Expected: FAIL — `ArtifactDiscoverer`/`PluginLocation` don't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Artifacts/ArtifactDiscoverer.cs`:
```csharp
using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Artifacts;

/// <summary>A plugin to scan: a display name and the directory containing its commands/skills/agents.</summary>
public sealed record PluginLocation(string Name, string RootPath);

public sealed class ArtifactDiscoverer
{
    private readonly IFileSystem _fs;

    public ArtifactDiscoverer(IFileSystem fs) => _fs = fs;

    public IReadOnlyList<DiscoveredArtifact> Discover(
        string userDir, string? projectDir, IReadOnlyList<PluginLocation> plugins)
    {
        var result = new List<DiscoveredArtifact>();
        result.AddRange(DiscoverScope($"{userDir}/.claude", new ArtifactSource(ArtifactSourceKind.User)));
        if (projectDir is not null)
            result.AddRange(DiscoverScope($"{projectDir}/.claude", new ArtifactSource(ArtifactSourceKind.Project)));
        foreach (var plugin in plugins)
            result.AddRange(DiscoverScope(plugin.RootPath, new ArtifactSource(ArtifactSourceKind.Plugin, plugin.Name)));
        return result;
    }

    private IEnumerable<DiscoveredArtifact> DiscoverScope(string root, ArtifactSource source)
    {
        foreach (var a in DiscoverCommands($"{root}/commands", source)) yield return a;
    }

    private IEnumerable<DiscoveredArtifact> DiscoverCommands(string dir, ArtifactSource source)
    {
        foreach (var file in _fs.GetFiles(dir, "*.md", recurse: true))
        {
            var fm = Frontmatter.Parse(_fs.ReadAllText(file));
            var name = NameFrom(fm, FileNameWithoutExtension(file));
            yield return new DiscoveredArtifact(ArtifactKind.Command, name, ArtifactSummary.Extract(fm), source, file);
        }
    }

    private static string NameFrom(FrontmatterResult fm, string fallback)
        => fm.Fields.TryGetValue("name", out var n) && n.Length > 0 ? n : fallback;

    private static string LastSegment(string path)
    {
        var trimmed = path.TrimEnd('/');
        var i = trimmed.LastIndexOf('/');
        return i >= 0 ? trimmed.Substring(i + 1) : trimmed;
    }

    private static string FileNameWithoutExtension(string path)
    {
        var seg = LastSegment(path);
        var dot = seg.LastIndexOf('.');
        return dot > 0 ? seg.Substring(0, dot) : seg;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter ArtifactDiscovererCommandTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): discover slash commands"
```

---

## Task 6: Discover skills

**Files:**
- Modify: `src/ClaudeExplorer.Core/Artifacts/ArtifactDiscoverer.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Artifacts/ArtifactDiscovererSkillTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Artifacts/ArtifactDiscovererSkillTests.cs`:
```csharp
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Artifacts;

public class ArtifactDiscovererSkillTests
{
    [Fact]
    public void Discovers_skills_from_SKILL_md_using_frontmatter_name()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/skills/graphify/SKILL.md", "---\nname: graphify\ndescription: input to graph\n---\nbody")
            .AddFile("/home/.claude/skills/empty-dir/README.md", "not a skill");

        var found = new ArtifactDiscoverer(fs).Discover("/home", null, Array.Empty<PluginLocation>());
        var skills = found.Where(a => a.Kind == ArtifactKind.Skill).ToList();

        Assert.Single(skills);
        Assert.Equal("graphify", skills[0].Name);
        Assert.Equal("input to graph", skills[0].Summary);
        Assert.EndsWith("graphify/SKILL.md", skills[0].FilePath);
    }

    [Fact]
    public void Skill_name_falls_back_to_directory_name()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/skills/my-skill/SKILL.md", "no frontmatter here");
        var found = new ArtifactDiscoverer(fs).Discover("/home", null, Array.Empty<PluginLocation>());
        Assert.Equal("my-skill", found.Single(a => a.Kind == ArtifactKind.Skill).Name);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter ArtifactDiscovererSkillTests`
Expected: FAIL — no skills discovered yet (asserts fail).

- [ ] **Step 3: Write minimal implementation**

In `src/ClaudeExplorer.Core/Artifacts/ArtifactDiscoverer.cs`, update `DiscoverScope` to also discover skills:
```csharp
    private IEnumerable<DiscoveredArtifact> DiscoverScope(string root, ArtifactSource source)
    {
        foreach (var a in DiscoverCommands($"{root}/commands", source)) yield return a;
        foreach (var a in DiscoverSkills($"{root}/skills", source)) yield return a;
    }
```

And add the `DiscoverSkills` method to the class:
```csharp
    private IEnumerable<DiscoveredArtifact> DiscoverSkills(string dir, ArtifactSource source)
    {
        foreach (var sub in _fs.GetDirectories(dir))
        {
            var skillFile = $"{sub}/SKILL.md";
            if (!_fs.FileExists(skillFile)) continue;
            var fm = Frontmatter.Parse(_fs.ReadAllText(skillFile));
            var name = NameFrom(fm, LastSegment(sub));
            yield return new DiscoveredArtifact(ArtifactKind.Skill, name, ArtifactSummary.Extract(fm), source, skillFile);
        }
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter ArtifactDiscovererSkillTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): discover skills"
```

---

## Task 7: Discover subagents

**Files:**
- Modify: `src/ClaudeExplorer.Core/Artifacts/ArtifactDiscoverer.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Artifacts/ArtifactDiscovererSubagentTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Artifacts/ArtifactDiscovererSubagentTests.cs`:
```csharp
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Artifacts;

public class ArtifactDiscovererSubagentTests
{
    [Fact]
    public void Discovers_top_level_agent_md_files_only()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/repo/.claude/agents/reviewer.md", "---\nname: reviewer\ndescription: reviews code\n---\nbody")
            .AddFile("/repo/.claude/agents/notes/scratch.md", "nested - should be ignored");

        var found = new ArtifactDiscoverer(fs).Discover("/home", "/repo", Array.Empty<PluginLocation>());
        var agents = found.Where(a => a.Kind == ArtifactKind.Subagent).ToList();

        Assert.Single(agents);
        Assert.Equal("reviewer", agents[0].Name);
        Assert.Equal(ArtifactSourceKind.Project, agents[0].Source.Kind);
        Assert.Equal("reviews code", agents[0].Summary);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter ArtifactDiscovererSubagentTests`
Expected: FAIL — no subagents discovered yet.

- [ ] **Step 3: Write minimal implementation**

In `src/ClaudeExplorer.Core/Artifacts/ArtifactDiscoverer.cs`, extend `DiscoverScope`:
```csharp
    private IEnumerable<DiscoveredArtifact> DiscoverScope(string root, ArtifactSource source)
    {
        foreach (var a in DiscoverCommands($"{root}/commands", source)) yield return a;
        foreach (var a in DiscoverSkills($"{root}/skills", source)) yield return a;
        foreach (var a in DiscoverSubagents($"{root}/agents", source)) yield return a;
    }
```

And add `DiscoverSubagents` (note: top-level only, `recurse: false`):
```csharp
    private IEnumerable<DiscoveredArtifact> DiscoverSubagents(string dir, ArtifactSource source)
    {
        foreach (var file in _fs.GetFiles(dir, "*.md", recurse: false))
        {
            var fm = Frontmatter.Parse(_fs.ReadAllText(file));
            var name = NameFrom(fm, FileNameWithoutExtension(file));
            yield return new DiscoveredArtifact(ArtifactKind.Subagent, name, ArtifactSummary.Extract(fm), source, file);
        }
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter ArtifactDiscovererSubagentTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): discover subagents"
```

---

## Task 8: Discover plugin artifacts

**Files:**
- Test: `tests/ClaudeExplorer.Core.Tests/Artifacts/ArtifactDiscovererPluginTests.cs`
- (No source change — exercises the plugin loop already in `Discover`.)

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Artifacts/ArtifactDiscovererPluginTests.cs`:
```csharp
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Artifacts;

public class ArtifactDiscovererPluginTests
{
    [Fact]
    public void Discovers_plugin_commands_and_skills_tagged_with_plugin_name()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/plugins/superpowers/commands/brainstorm.md", "---\ndescription: explore design\n---\nb")
            .AddFile("/plugins/superpowers/skills/tdd/SKILL.md", "---\nname: tdd\ndescription: test first\n---\nb");

        var plugins = new[] { new PluginLocation("superpowers", "/plugins/superpowers") };
        var found = new ArtifactDiscoverer(fs).Discover("/home", null, plugins);

        Assert.All(found, a =>
        {
            Assert.Equal(ArtifactSourceKind.Plugin, a.Source.Kind);
            Assert.Equal("superpowers", a.Source.PluginName);
        });
        Assert.Contains(found, a => a.Kind == ArtifactKind.Command && a.Name == "brainstorm");
        Assert.Contains(found, a => a.Kind == ArtifactKind.Skill && a.Name == "tdd");
    }
}
```

- [ ] **Step 2: Run test to verify it fails (or passes)**

Run: `dotnet test --filter ArtifactDiscovererPluginTests`
Expected: PASS immediately — the plugin loop already exists from Task 5. If it fails, fix the plugin branch in `Discover`. (This task locks plugin behavior with a test.)

- [ ] **Step 3: (No implementation needed if green)**

If the test passed in Step 2, no code change. If not, ensure `Discover` iterates `plugins` and calls `DiscoverScope(plugin.RootPath, new ArtifactSource(ArtifactSourceKind.Plugin, plugin.Name))`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter ArtifactDiscovererPluginTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "test(core): lock plugin artifact discovery"
```

---

## Task 9: Shadow/override resolver

**Files:**
- Create: `src/ClaudeExplorer.Core/Artifacts/ArtifactResolver.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Artifacts/ArtifactResolverTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Artifacts/ArtifactResolverTests.cs`:
```csharp
using ClaudeExplorer.Core.Artifacts;

namespace ClaudeExplorer.Core.Tests.Artifacts;

public class ArtifactResolverTests
{
    private static DiscoveredArtifact A(ArtifactKind kind, string name, ArtifactSourceKind src, string? plugin = null)
        => new(kind, name, null, new ArtifactSource(src, plugin), $"/{src}/{name}");

    [Fact]
    public void Highest_precedence_source_wins_and_others_are_shadowed()
    {
        var input = new[]
        {
            A(ArtifactKind.Command, "review", ArtifactSourceKind.User),
            A(ArtifactKind.Command, "review", ArtifactSourceKind.Project),
            A(ArtifactKind.Command, "review", ArtifactSourceKind.Plugin, "pack"),
        };

        var catalog = new ArtifactResolver().Resolve(input);
        var review = catalog.Artifacts.Single();

        Assert.Equal(ArtifactSourceKind.Project, review.Winner.Source.Kind);
        Assert.True(review.IsShadowing);
        Assert.Equal(2, review.Shadowed.Count);
        Assert.Contains(review.Shadowed, s => s.Source.Kind == ArtifactSourceKind.User);
        Assert.Contains(review.Shadowed, s => s.Source.Kind == ArtifactSourceKind.Plugin);
    }

    [Fact]
    public void Different_kinds_with_same_name_do_not_collide()
    {
        var input = new[]
        {
            A(ArtifactKind.Command, "x", ArtifactSourceKind.User),
            A(ArtifactKind.Skill, "x", ArtifactSourceKind.User),
        };
        var catalog = new ArtifactResolver().Resolve(input);
        Assert.Equal(2, catalog.Artifacts.Count);
        Assert.All(catalog.Artifacts, r => Assert.False(r.IsShadowing));
    }

    [Fact]
    public void Output_is_sorted_by_kind_then_name()
    {
        var input = new[]
        {
            A(ArtifactKind.Skill, "beta", ArtifactSourceKind.User),
            A(ArtifactKind.Command, "zeta", ArtifactSourceKind.User),
            A(ArtifactKind.Command, "alpha", ArtifactSourceKind.User),
        };
        var names = new ArtifactResolver().Resolve(input).Artifacts
            .Select(r => $"{r.Winner.Kind}:{r.Winner.Name}").ToArray();
        Assert.Equal(new[] { "Command:alpha", "Command:zeta", "Skill:beta" }, names);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter ArtifactResolverTests`
Expected: FAIL — `ArtifactResolver` doesn't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Artifacts/ArtifactResolver.cs`:
```csharp
namespace ClaudeExplorer.Core.Artifacts;

public sealed class ArtifactResolver
{
    public ArtifactCatalog Resolve(IReadOnlyList<DiscoveredArtifact> discovered)
    {
        var resolved = new List<ResolvedArtifact>();

        foreach (var group in discovered.GroupBy(a => (a.Kind, a.Name)))
        {
            var ordered = group.OrderByDescending(a => a.Source.Precedence).ToList();
            resolved.Add(new ResolvedArtifact(ordered[0], ordered.Skip(1).ToList()));
        }

        var sorted = resolved
            .OrderBy(r => r.Winner.Kind)
            .ThenBy(r => r.Winner.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ArtifactCatalog(sorted);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter ArtifactResolverTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): resolve artifact name-collision shadowing"
```

---

## Task 10: ArtifactCatalogService façade + integration

**Files:**
- Create: `src/ClaudeExplorer.Core/Artifacts/ArtifactCatalogService.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Artifacts/ArtifactCatalogServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Artifacts/ArtifactCatalogServiceTests.cs`:
```csharp
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Artifacts;

public class ArtifactCatalogServiceTests
{
    [Fact]
    public void Builds_categorized_catalog_with_shadowing_across_all_sources()
    {
        var fs = new InMemoryFileSystem()
            // user
            .AddFile("/home/.claude/commands/review.md", "---\ndescription: user review\n---\nb")
            .AddFile("/home/.claude/skills/graphify/SKILL.md", "---\nname: graphify\ndescription: to graph\n---\nb")
            // project overrides the user 'review' command
            .AddFile("/repo/.claude/commands/review.md", "---\ndescription: project review\n---\nb")
            // plugin
            .AddFile("/plugins/superpowers/skills/tdd/SKILL.md", "---\nname: tdd\ndescription: test first\n---\nb");

        var plugins = new[] { new PluginLocation("superpowers", "/plugins/superpowers") };
        var catalog = new ArtifactCatalogService(fs).Build("/home", "/repo", plugins);

        // review: project wins, user shadowed
        var review = catalog.OfKind(ArtifactKind.Command).Single(r => r.Winner.Name == "review");
        Assert.Equal(ArtifactSourceKind.Project, review.Winner.Source.Kind);
        Assert.Equal("project review", review.Winner.Summary);
        Assert.True(review.IsShadowing);
        Assert.Single(review.Shadowed);

        // skills: graphify (user) + tdd (plugin), no collisions
        var skills = catalog.OfKind(ArtifactKind.Skill).ToList();
        Assert.Equal(2, skills.Count);
        Assert.Contains(skills, s => s.Winner.Name == "graphify" && s.Winner.Source.Kind == ArtifactSourceKind.User);
        Assert.Contains(skills, s => s.Winner.Name == "tdd" && s.Winner.Source.Label == "Plugin: superpowers");
    }

    [Fact]
    public void Empty_workspace_yields_empty_catalog()
    {
        var catalog = new ArtifactCatalogService(new InMemoryFileSystem()).Build("/home");
        Assert.Empty(catalog.Artifacts);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter ArtifactCatalogServiceTests`
Expected: FAIL — `ArtifactCatalogService` doesn't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Artifacts/ArtifactCatalogService.cs`:
```csharp
using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Artifacts;

/// <summary>Top-level façade: discover all file-based artifacts and resolve them into a catalog.</summary>
public sealed class ArtifactCatalogService
{
    private readonly ArtifactDiscoverer _discoverer;
    private readonly ArtifactResolver _resolver;

    public ArtifactCatalogService(IFileSystem fileSystem)
    {
        _discoverer = new ArtifactDiscoverer(fileSystem);
        _resolver = new ArtifactResolver();
    }

    public ArtifactCatalog Build(
        string userDir,
        string? projectDir = null,
        IReadOnlyList<PluginLocation>? plugins = null)
    {
        var discovered = _discoverer.Discover(userDir, projectDir, plugins ?? Array.Empty<PluginLocation>());
        return _resolver.Resolve(discovered);
    }
}
```

- [ ] **Step 4: Run full test suite**

Run: `dotnet test`
Expected: PASS — all Phase 1 + Phase 2 tests green.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): ArtifactCatalogService facade"
```

---

## Self-Review

**Spec coverage:**
- Discover commands/skills/subagents across User/Project/Plugin → Tasks 5–8. ✓
- Frontmatter parse + name/summary extraction → Tasks 2, 4. ✓
- Source categorization + shadow/override resolution with precedence → Tasks 3, 9. ✓
- Directory enumeration via `IFileSystem` seam (testable) → Task 1. ✓
- Façade + integration → Task 10. ✓

**Deferred (noted, not forgotten):** built-in commands list; nested-command namespacing (e.g. `git:commit`); MCP servers + plugins/marketplaces parsing; real plugin-directory resolution (caller supplies `PluginLocation`s for now); multi-line/complex frontmatter. These belong to later phases / the existing tech-debt issue.

**Placeholder scan:** none — every step has complete code/commands.

**Type consistency:** `ArtifactKind`, `ArtifactSourceKind`, `ArtifactSource`, `DiscoveredArtifact`, `ResolvedArtifact`, `ArtifactCatalog`, `PluginLocation`, `ArtifactDiscoverer.Discover`, `ArtifactResolver.Resolve`, `ArtifactCatalogService.Build`, `Frontmatter.Parse`, `ArtifactSummary.Extract` are used identically across all tasks. The new `IFileSystem` members (`DirectoryExists`, `GetDirectories`, `GetFiles`) match between interface, `PhysicalFileSystem`, and `InMemoryFileSystem`.

---

## Execution Handoff

Plan saved to `docs/superpowers/plans/2026-06-07-02-artifact-discovery.md`. Execute via superpowers:subagent-driven-development (one implementer for the cohesive engine, then spec + code-quality review), then finishing-a-development-branch.
