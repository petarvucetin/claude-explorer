# Core Config Engine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a headless .NET library that computes Claude Code's **effective `settings.json` configuration** for a workspace — merged across scopes with correct precedence, per-key merge semantics, conflict detection, and full provenance — driven entirely by injectable file fixtures.

**Architecture:** Pure `ClaudeExplorer.Core` class library. An `IFileSystem` abstraction makes everything testable against in-memory fixtures. A `SettingsLocator` finds the per-scope settings files; a `SettingsReader` parses them to `JsonObject`; a `MergeEngine` resolves a flat list of `EffectiveSetting` records (one per dotted key like `model`, `permissions.allow`, `env.FOO`, `hooks.PreToolUse`) carrying the winning value, all contributions, and a conflict flag.

**Tech Stack:** .NET 10, C#, `System.Text.Json` (`System.Text.Json.Nodes`), xUnit.

---

## Precedence & semantics (authoritative)

Highest wins: **Enterprise > Local > Project > User**. Modeled as `ScopeKind` int values
(`User=0, Project=1, Local=2, Enterprise=3`); the winner is the highest-value scope that
defines a key.

- **Scalars** (`model`, `outputStyle`, `statusLine`, `permissions.defaultMode`, `env.*`):
  highest-precedence contribution wins; **conflict** = >1 distinct contributed value.
- **Permission lists** (`permissions.allow|deny|ask`): **union** (dedup, order = scope
  precedence ascending then file order).
- **Hooks** (`hooks.<Event>`): **array concat** across scopes.

## File structure

- `ClaudeExplorer.sln`
- `src/ClaudeExplorer.Core/`
  - `Model/ScopeKind.cs` — scope enum (precedence by int value)
  - `Model/ConfigFile.cs` — a located settings file
  - `Model/Provenance.cs` — `SettingOrigin`, `SettingContribution`
  - `Model/EffectiveSetting.cs` — `MergeStrategy`, `EffectiveSetting`, `EffectiveConfig`
  - `Io/IFileSystem.cs` — fs abstraction + `PhysicalFileSystem`
  - `Discovery/SettingsLocator.cs` — find settings files per scope
  - `Reading/SettingsReader.cs` — parse a `ConfigFile` → `JsonObject` (+ `SettingsParseException`)
  - `Merge/SettingSpec.cs` — spec record + `SettingSpecs` registry
  - `Merge/ScopeSettings.cs` — `(ScopeKind, FilePath, JsonObject)` engine input
  - `Merge/MergeEngine.cs` — the resolver
  - `EffectiveConfigService.cs` — façade: locate → read → merge
- `tests/ClaudeExplorer.Core.Tests/`
  - `Fakes/InMemoryFileSystem.cs`
  - `Discovery/SettingsLocatorTests.cs`
  - `Reading/SettingsReaderTests.cs`
  - `Merge/MergeEngineTests.cs`
  - `EffectiveConfigServiceTests.cs`

---

## Task 1: Solution & project scaffolding

**Files:**
- Create: `ClaudeExplorer.sln`, `src/ClaudeExplorer.Core/ClaudeExplorer.Core.csproj`, `tests/ClaudeExplorer.Core.Tests/ClaudeExplorer.Core.Tests.csproj`

- [ ] **Step 1: Create solution, projects, references**

Run:
```bash
dotnet new sln -n ClaudeExplorer
dotnet new classlib -n ClaudeExplorer.Core -o src/ClaudeExplorer.Core -f net10.0
dotnet new xunit -n ClaudeExplorer.Core.Tests -o tests/ClaudeExplorer.Core.Tests -f net10.0
dotnet sln add src/ClaudeExplorer.Core/ClaudeExplorer.Core.csproj
dotnet sln add tests/ClaudeExplorer.Core.Tests/ClaudeExplorer.Core.Tests.csproj
dotnet add tests/ClaudeExplorer.Core.Tests/ClaudeExplorer.Core.Tests.csproj reference src/ClaudeExplorer.Core/ClaudeExplorer.Core.csproj
```

- [ ] **Step 2: Remove template stub files**

Run:
```bash
rm src/ClaudeExplorer.Core/Class1.cs
rm tests/ClaudeExplorer.Core.Tests/UnitTest1.cs
```

- [ ] **Step 3: Add a wiring smoke test**

Create `tests/ClaudeExplorer.Core.Tests/WiringTests.cs`:
```csharp
namespace ClaudeExplorer.Core.Tests;

public class WiringTests
{
    [Fact]
    public void Solution_builds_and_tests_run()
    {
        Assert.True(true);
    }
}
```

- [ ] **Step 4: Build and test**

Run: `dotnet test`
Expected: build succeeds, 1 test passes.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "chore: scaffold ClaudeExplorer.Core solution and test project"
```

---

## Task 2: Scope model

**Files:**
- Create: `src/ClaudeExplorer.Core/Model/ScopeKind.cs`, `src/ClaudeExplorer.Core/Model/ConfigFile.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Model/ScopeKindTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Model/ScopeKindTests.cs`:
```csharp
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Tests.Model;

public class ScopeKindTests
{
    [Fact]
    public void Precedence_orders_user_lowest_enterprise_highest()
    {
        Assert.True((int)ScopeKind.User < (int)ScopeKind.Project);
        Assert.True((int)ScopeKind.Project < (int)ScopeKind.Local);
        Assert.True((int)ScopeKind.Local < (int)ScopeKind.Enterprise);
    }

    [Fact]
    public void ConfigFile_carries_scope_and_path()
    {
        var f = new ConfigFile(ScopeKind.Project, "/repo/.claude/settings.json");
        Assert.Equal(ScopeKind.Project, f.Scope);
        Assert.Equal("/repo/.claude/settings.json", f.Path);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter ScopeKindTests`
Expected: FAIL — `ScopeKind`/`ConfigFile` do not exist (compile error).

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Model/ScopeKind.cs`:
```csharp
namespace ClaudeExplorer.Core.Model;

/// <summary>
/// Configuration scopes, ordered by precedence. Higher integer value wins when a key is
/// defined in multiple scopes. Command-line args (between Enterprise and Local at runtime)
/// are not modeled because this tool reads files only.
/// </summary>
public enum ScopeKind
{
    User = 0,
    Project = 1,
    Local = 2,
    Enterprise = 3,
}
```

Create `src/ClaudeExplorer.Core/Model/ConfigFile.cs`:
```csharp
namespace ClaudeExplorer.Core.Model;

/// <summary>A settings file located on disk for a given scope.</summary>
public sealed record ConfigFile(ScopeKind Scope, string Path);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter ScopeKindTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): add ScopeKind precedence model and ConfigFile"
```

---

## Task 3: Provenance & effective-setting model

**Files:**
- Create: `src/ClaudeExplorer.Core/Model/Provenance.cs`, `src/ClaudeExplorer.Core/Model/EffectiveSetting.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Model/EffectiveSettingTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Model/EffectiveSettingTests.cs`:
```csharp
using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Tests.Model;

public class EffectiveSettingTests
{
    [Fact]
    public void Find_returns_setting_by_key()
    {
        var origin = new SettingOrigin(ScopeKind.User, "/u/settings.json", "model");
        var contrib = new SettingContribution(origin, JsonValue.Create("opus"));
        var setting = new EffectiveSetting(
            Key: "model",
            Strategy: MergeStrategy.ScalarLastWins,
            Value: JsonValue.Create("opus"),
            Winner: origin,
            Contributions: new[] { contrib },
            HasConflict: false);

        var config = new EffectiveConfig(new[] { setting });

        Assert.Same(setting, config.Find("model"));
        Assert.Null(config.Find("nope"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter EffectiveSettingTests`
Expected: FAIL — types do not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Model/Provenance.cs`:
```csharp
using System.Text.Json.Nodes;

namespace ClaudeExplorer.Core.Model;

/// <summary>Where a contributed value came from.</summary>
public sealed record SettingOrigin(ScopeKind Scope, string FilePath, string JsonPath);

/// <summary>One scope's contribution to a setting (its raw value at that scope).</summary>
public sealed record SettingContribution(SettingOrigin Origin, JsonNode? Value);
```

Create `src/ClaudeExplorer.Core/Model/EffectiveSetting.cs`:
```csharp
using System.Text.Json.Nodes;

namespace ClaudeExplorer.Core.Model;

public enum MergeStrategy
{
    ScalarLastWins,
    ListUnion,
    ArrayConcat,
}

/// <summary>
/// A single resolved setting keyed by dotted path (e.g. "model", "permissions.allow",
/// "env.FOO", "hooks.PreToolUse").
/// </summary>
public sealed record EffectiveSetting(
    string Key,
    MergeStrategy Strategy,
    JsonNode? Value,
    SettingOrigin? Winner,
    IReadOnlyList<SettingContribution> Contributions,
    bool HasConflict);

public sealed record EffectiveConfig(IReadOnlyList<EffectiveSetting> Settings)
{
    public EffectiveSetting? Find(string key)
        => Settings.FirstOrDefault(s => s.Key == key);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter EffectiveSettingTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): add provenance and effective-setting model"
```

---

## Task 4: File-system abstraction + in-memory fake

**Files:**
- Create: `src/ClaudeExplorer.Core/Io/IFileSystem.cs`
- Create: `tests/ClaudeExplorer.Core.Tests/Fakes/InMemoryFileSystem.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Fakes/InMemoryFileSystemTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Fakes/InMemoryFileSystemTests.cs`:
```csharp
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Fakes;

public class InMemoryFileSystemTests
{
    [Fact]
    public void Reports_existence_and_reads_content()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/u/.claude/settings.json", "{}");

        Assert.True(fs.FileExists("/u/.claude/settings.json"));
        Assert.False(fs.FileExists("/missing.json"));
        Assert.Equal("{}", fs.ReadAllText("/u/.claude/settings.json"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter InMemoryFileSystemTests`
Expected: FAIL — `IFileSystem`/`InMemoryFileSystem` do not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Io/IFileSystem.cs`:
```csharp
namespace ClaudeExplorer.Core.Io;

public interface IFileSystem
{
    bool FileExists(string path);
    string ReadAllText(string path);
}

public sealed class PhysicalFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);
    public string ReadAllText(string path) => File.ReadAllText(path);
}
```

Create `tests/ClaudeExplorer.Core.Tests/Fakes/InMemoryFileSystem.cs`:
```csharp
using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Tests.Fakes;

/// <summary>Deterministic in-memory file system. Paths use forward slashes.</summary>
public sealed class InMemoryFileSystem : IFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    public InMemoryFileSystem AddFile(string path, string content)
    {
        _files[Normalize(path)] = content;
        return this;
    }

    public bool FileExists(string path) => _files.ContainsKey(Normalize(path));

    public string ReadAllText(string path)
        => _files.TryGetValue(Normalize(path), out var c)
            ? c
            : throw new FileNotFoundException(path);

    private static string Normalize(string path) => path.Replace('\\', '/');
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter InMemoryFileSystemTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): add IFileSystem abstraction and in-memory fake"
```

---

## Task 5: Settings locator

**Files:**
- Create: `src/ClaudeExplorer.Core/Discovery/SettingsLocator.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Discovery/SettingsLocatorTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Discovery/SettingsLocatorTests.cs`:
```csharp
using ClaudeExplorer.Core.Discovery;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Discovery;

public class SettingsLocatorTests
{
    [Fact]
    public void Locates_only_existing_files_in_precedence_order()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/me/.claude/settings.json", "{}")
            .AddFile("/repo/.claude/settings.json", "{}")
            .AddFile("/repo/.claude/settings.local.json", "{}");
        // enterprise file intentionally absent

        var located = new SettingsLocator(fs).Locate("/home/me", "/repo", "/etc/claude/managed-settings.json");

        Assert.Equal(
            new[] { ScopeKind.User, ScopeKind.Project, ScopeKind.Local },
            located.Select(f => f.Scope).ToArray());
        Assert.Equal("/repo/.claude/settings.local.json", located.Single(f => f.Scope == ScopeKind.Local).Path);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter SettingsLocatorTests`
Expected: FAIL — `SettingsLocator` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Discovery/SettingsLocator.cs`:
```csharp
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Discovery;

/// <summary>
/// Locates the settings files that exist for a workspace. Paths are built with forward
/// slashes for determinism; .NET accepts '/' on all platforms.
/// </summary>
public sealed class SettingsLocator
{
    private readonly IFileSystem _fs;

    public SettingsLocator(IFileSystem fs) => _fs = fs;

    public IReadOnlyList<ConfigFile> Locate(string userDir, string projectDir, string? enterprisePath = null)
    {
        var candidates = new List<ConfigFile>();
        if (enterprisePath is not null)
            candidates.Add(new ConfigFile(ScopeKind.Enterprise, enterprisePath));
        candidates.Add(new ConfigFile(ScopeKind.User, $"{userDir}/.claude/settings.json"));
        candidates.Add(new ConfigFile(ScopeKind.Project, $"{projectDir}/.claude/settings.json"));
        candidates.Add(new ConfigFile(ScopeKind.Local, $"{projectDir}/.claude/settings.local.json"));

        return candidates
            .Where(c => _fs.FileExists(c.Path))
            .OrderBy(c => (int)c.Scope)
            .ToList();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter SettingsLocatorTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): locate per-scope settings files"
```

---

## Task 6: Settings reader (resilient JSON parse)

**Files:**
- Create: `src/ClaudeExplorer.Core/Reading/SettingsReader.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Reading/SettingsReaderTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Reading/SettingsReaderTests.cs`:
```csharp
using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Reading;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Reading;

public class SettingsReaderTests
{
    [Fact]
    public void Parses_object_allowing_comments_and_trailing_commas()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/u/.claude/settings.json", """
            {
              // user model
              "model": "opus",
            }
            """);
        var reader = new SettingsReader(fs);

        var obj = reader.Read(new ConfigFile(ScopeKind.User, "/u/.claude/settings.json"));

        Assert.Equal("opus", (string?)obj["model"]);
    }

    [Fact]
    public void Throws_when_root_is_not_an_object()
    {
        var fs = new InMemoryFileSystem().AddFile("/u/.claude/settings.json", "[1,2,3]");
        var reader = new SettingsReader(fs);

        Assert.Throws<SettingsParseException>(
            () => reader.Read(new ConfigFile(ScopeKind.User, "/u/.claude/settings.json")));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter SettingsReaderTests`
Expected: FAIL — `SettingsReader`/`SettingsParseException` do not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Reading/SettingsReader.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Reading;

public sealed class SettingsParseException : Exception
{
    public SettingsParseException(string message, Exception? inner = null) : base(message, inner) { }
}

public sealed class SettingsReader
{
    private static readonly JsonDocumentOptions DocOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IFileSystem _fs;

    public SettingsReader(IFileSystem fs) => _fs = fs;

    public JsonObject Read(ConfigFile file)
    {
        string text = _fs.ReadAllText(file.Path);
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text, nodeOptions: null, documentOptions: DocOptions);
        }
        catch (JsonException ex)
        {
            throw new SettingsParseException($"Invalid JSON in {file.Path}: {ex.Message}", ex);
        }

        if (node is not JsonObject obj)
            throw new SettingsParseException($"Settings root is not a JSON object: {file.Path}");

        return obj;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter SettingsReaderTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): resilient settings JSON reader"
```

---

## Task 7: Setting-spec registry & engine input

**Files:**
- Create: `src/ClaudeExplorer.Core/Merge/SettingSpec.cs`, `src/ClaudeExplorer.Core/Merge/ScopeSettings.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Merge/SettingSpecTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Merge/SettingSpecTests.cs`:
```csharp
using ClaudeExplorer.Core.Merge;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Tests.Merge;

public class SettingSpecTests
{
    [Fact]
    public void Registry_defines_scalar_and_list_specs()
    {
        Assert.Contains(SettingSpecs.Scalars, s => s.Key == "model" && s.Strategy == MergeStrategy.ScalarLastWins);
        Assert.Contains(SettingSpecs.Scalars, s => s.Key == "permissions.defaultMode");
        Assert.Contains(SettingSpecs.Lists, s => s.Key == "permissions.allow" && s.Strategy == MergeStrategy.ListUnion);
        Assert.Equal(new[] { "permissions", "allow" }, SettingSpecs.Lists.Single(s => s.Key == "permissions.allow").Path);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter SettingSpecTests`
Expected: FAIL — `SettingSpec`/`SettingSpecs` do not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Merge/SettingSpec.cs`:
```csharp
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Merge;

/// <summary>A statically-known setting: its dotted key, merge strategy, and JSON path.</summary>
public sealed record SettingSpec(string Key, MergeStrategy Strategy, string[] Path);

public static class SettingSpecs
{
    public static readonly IReadOnlyList<SettingSpec> Scalars = new[]
    {
        new SettingSpec("model", MergeStrategy.ScalarLastWins, new[] { "model" }),
        new SettingSpec("outputStyle", MergeStrategy.ScalarLastWins, new[] { "outputStyle" }),
        new SettingSpec("statusLine", MergeStrategy.ScalarLastWins, new[] { "statusLine" }),
        new SettingSpec("permissions.defaultMode", MergeStrategy.ScalarLastWins, new[] { "permissions", "defaultMode" }),
    };

    public static readonly IReadOnlyList<SettingSpec> Lists = new[]
    {
        new SettingSpec("permissions.allow", MergeStrategy.ListUnion, new[] { "permissions", "allow" }),
        new SettingSpec("permissions.deny", MergeStrategy.ListUnion, new[] { "permissions", "deny" }),
        new SettingSpec("permissions.ask", MergeStrategy.ListUnion, new[] { "permissions", "ask" }),
    };
}
```

Create `src/ClaudeExplorer.Core/Merge/ScopeSettings.cs`:
```csharp
using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Merge;

/// <summary>A parsed settings object tagged with its scope and source path.</summary>
public sealed record ScopeSettings(ScopeKind Scope, string FilePath, JsonObject Root);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter SettingSpecTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): setting-spec registry and engine input type"
```

---

## Task 8: Merge engine — scalars & conflict detection

**Files:**
- Create: `src/ClaudeExplorer.Core/Merge/MergeEngine.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Merge/MergeEngineScalarTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Merge/MergeEngineScalarTests.cs`:
```csharp
using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Merge;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Tests.Merge;

public class MergeEngineScalarTests
{
    private static ScopeSettings Scope(ScopeKind kind, string json)
        => new(kind, $"/{kind}.json", (JsonObject)JsonNode.Parse(json)!);

    [Fact]
    public void Higher_precedence_scope_wins_and_conflict_is_flagged()
    {
        var engine = new MergeEngine();
        var result = engine.Compute(new[]
        {
            Scope(ScopeKind.User, """{ "model": "opus" }"""),
            Scope(ScopeKind.Project, """{ "model": "sonnet" }"""),
        });

        var model = result.Find("model")!;
        Assert.Equal("sonnet", (string?)model.Value);              // project (1) beats user (0)
        Assert.Equal(ScopeKind.Project, model.Winner!.Scope);
        Assert.True(model.HasConflict);                            // two differing values
        Assert.Equal(2, model.Contributions.Count);
    }

    [Fact]
    public void Single_contribution_is_not_a_conflict()
    {
        var engine = new MergeEngine();
        var result = engine.Compute(new[] { Scope(ScopeKind.User, """{ "model": "opus" }""") });

        var model = result.Find("model")!;
        Assert.Equal("opus", (string?)model.Value);
        Assert.False(model.HasConflict);
    }

    [Fact]
    public void Absent_setting_is_omitted()
    {
        var engine = new MergeEngine();
        var result = engine.Compute(new[] { Scope(ScopeKind.User, "{ }") });
        Assert.Null(result.Find("model"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter MergeEngineScalarTests`
Expected: FAIL — `MergeEngine` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Merge/MergeEngine.cs`:
```csharp
using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Merge;

public sealed class MergeEngine
{
    public EffectiveConfig Compute(IReadOnlyList<ScopeSettings> scopes)
    {
        var ordered = scopes.OrderBy(s => (int)s.Scope).ToList();
        var results = new List<EffectiveSetting>();

        foreach (var spec in SettingSpecs.Scalars)
        {
            var s = ResolveScalar(spec.Key, spec.Path, ordered);
            if (s is not null) results.Add(s);
        }

        return new EffectiveConfig(results);
    }

    private static JsonNode? Navigate(JsonObject root, string[] path)
    {
        JsonNode? cur = root;
        foreach (var seg in path)
        {
            if (cur is JsonObject o && o.TryGetPropertyValue(seg, out var next))
                cur = next;
            else
                return null;
        }
        return cur;
    }

    private static EffectiveSetting? ResolveScalar(string key, string[] path, List<ScopeSettings> ordered)
    {
        var contributions = new List<SettingContribution>();
        foreach (var s in ordered)
        {
            var v = Navigate(s.Root, path);
            if (v is not null)
                contributions.Add(new SettingContribution(
                    new SettingOrigin(s.Scope, s.FilePath, string.Join('.', path)),
                    v.DeepClone()));
        }

        if (contributions.Count == 0) return null;

        var winner = contributions[^1];                 // ordered ascending → last is highest precedence
        var distinct = contributions
            .Select(c => c.Value?.ToJsonString() ?? "null")
            .Distinct()
            .Count();

        return new EffectiveSetting(
            Key: key,
            Strategy: MergeStrategy.ScalarLastWins,
            Value: winner.Value?.DeepClone(),
            Winner: winner.Origin,
            Contributions: contributions,
            HasConflict: distinct > 1);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter MergeEngineScalarTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): merge engine scalar resolution with conflict detection"
```

---

## Task 9: Merge engine — permission list union

**Files:**
- Modify: `src/ClaudeExplorer.Core/Merge/MergeEngine.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Merge/MergeEngineListTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Merge/MergeEngineListTests.cs`:
```csharp
using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Merge;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Tests.Merge;

public class MergeEngineListTests
{
    private static ScopeSettings Scope(ScopeKind kind, string json)
        => new(kind, $"/{kind}.json", (JsonObject)JsonNode.Parse(json)!);

    [Fact]
    public void Permission_allow_is_unioned_across_scopes_with_dedup()
    {
        var engine = new MergeEngine();
        var result = engine.Compute(new[]
        {
            Scope(ScopeKind.User, """{ "permissions": { "allow": ["Bash(git*)"] } }"""),
            Scope(ScopeKind.Project, """{ "permissions": { "allow": ["Read(src/**)", "Bash(git*)"] } }"""),
            Scope(ScopeKind.Local, """{ "permissions": { "allow": ["Bash(npm*)"] } }"""),
        });

        var allow = result.Find("permissions.allow")!;
        var values = ((JsonArray)allow.Value!).Select(n => (string?)n).ToArray();

        Assert.Equal(new[] { "Bash(git*)", "Read(src/**)", "Bash(npm*)" }, values);  // dedup, precedence order
        Assert.Equal(MergeStrategy.ListUnion, allow.Strategy);
        Assert.Null(allow.Winner);          // merges have no single winner
        Assert.False(allow.HasConflict);    // union is not a conflict
        Assert.Equal(3, allow.Contributions.Count);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter MergeEngineListTests`
Expected: FAIL — `permissions.allow` is not yet produced (returns null).

- [ ] **Step 3: Write minimal implementation**

In `src/ClaudeExplorer.Core/Merge/MergeEngine.cs`, add the list resolution call inside `Compute` after the scalar loop:
```csharp
        foreach (var spec in SettingSpecs.Lists)
        {
            var s = ResolveListUnion(spec.Key, spec.Path, ordered);
            if (s is not null) results.Add(s);
        }
```

And add the method to the class:
```csharp
    private static EffectiveSetting? ResolveListUnion(string key, string[] path, List<ScopeSettings> ordered)
    {
        var contributions = new List<SettingContribution>();
        var merged = new JsonArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var s in ordered)
        {
            if (Navigate(s.Root, path) is not JsonArray arr) continue;

            contributions.Add(new SettingContribution(
                new SettingOrigin(s.Scope, s.FilePath, string.Join('.', path)),
                arr.DeepClone()));

            foreach (var item in arr)
            {
                var itemKey = item?.ToJsonString() ?? "null";
                if (seen.Add(itemKey))
                    merged.Add(item?.DeepClone());
            }
        }

        if (contributions.Count == 0) return null;

        return new EffectiveSetting(
            Key: key,
            Strategy: MergeStrategy.ListUnion,
            Value: merged,
            Winner: null,
            Contributions: contributions,
            HasConflict: false);
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter MergeEngineListTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): permission list union merge"
```

---

## Task 10: Merge engine — env expansion & hooks concat

**Files:**
- Modify: `src/ClaudeExplorer.Core/Merge/MergeEngine.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Merge/MergeEngineEnvHooksTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Merge/MergeEngineEnvHooksTests.cs`:
```csharp
using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Merge;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Tests.Merge;

public class MergeEngineEnvHooksTests
{
    private static ScopeSettings Scope(ScopeKind kind, string json)
        => new(kind, $"/{kind}.json", (JsonObject)JsonNode.Parse(json)!);

    [Fact]
    public void Env_keys_are_expanded_to_scalar_settings()
    {
        var engine = new MergeEngine();
        var result = engine.Compute(new[]
        {
            Scope(ScopeKind.User, """{ "env": { "DISABLE_TELEMETRY": "1" } }"""),
            Scope(ScopeKind.Project, """{ "env": { "ANTHROPIC_LOG": "debug", "DISABLE_TELEMETRY": "0" } }"""),
        });

        Assert.Equal("debug", (string?)result.Find("env.ANTHROPIC_LOG")!.Value);
        var telem = result.Find("env.DISABLE_TELEMETRY")!;
        Assert.Equal("0", (string?)telem.Value);          // project beats user
        Assert.True(telem.HasConflict);                    // "1" vs "0"
    }

    [Fact]
    public void Hooks_are_concatenated_per_event()
    {
        var engine = new MergeEngine();
        var result = engine.Compute(new[]
        {
            Scope(ScopeKind.User, """{ "hooks": { "PreToolUse": [ { "matcher": "Bash" } ] } }"""),
            Scope(ScopeKind.Project, """{ "hooks": { "PreToolUse": [ { "matcher": "Read" }, { "matcher": "Edit" } ] } }"""),
        });

        var hooks = result.Find("hooks.PreToolUse")!;
        Assert.Equal(MergeStrategy.ArrayConcat, hooks.Strategy);
        Assert.Equal(3, ((JsonArray)hooks.Value!).Count);  // 1 + 2 combined
        Assert.Equal(2, hooks.Contributions.Count);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter MergeEngineEnvHooksTests`
Expected: FAIL — `env.*` and `hooks.*` not produced yet.

- [ ] **Step 3: Write minimal implementation**

In `src/ClaudeExplorer.Core/Merge/MergeEngine.cs`, add inside `Compute` after the list loop:
```csharp
        results.AddRange(ResolveEnv(ordered));
        results.AddRange(ResolveHooks(ordered));
```

Add these methods to the class:
```csharp
    private static IEnumerable<EffectiveSetting> ResolveEnv(List<ScopeSettings> ordered)
    {
        var keys = new List<string>();
        foreach (var s in ordered)
            if (Navigate(s.Root, new[] { "env" }) is JsonObject env)
                foreach (var kv in env)
                    if (!keys.Contains(kv.Key)) keys.Add(kv.Key);

        foreach (var key in keys)
        {
            var contributions = new List<SettingContribution>();
            foreach (var s in ordered)
                if (Navigate(s.Root, new[] { "env" }) is JsonObject env
                    && env.TryGetPropertyValue(key, out var v) && v is not null)
                    contributions.Add(new SettingContribution(
                        new SettingOrigin(s.Scope, s.FilePath, $"env.{key}"),
                        v.DeepClone()));

            var winner = contributions[^1];
            var distinct = contributions.Select(c => c.Value?.ToJsonString() ?? "null").Distinct().Count();

            yield return new EffectiveSetting(
                Key: $"env.{key}",
                Strategy: MergeStrategy.ScalarLastWins,
                Value: winner.Value?.DeepClone(),
                Winner: winner.Origin,
                Contributions: contributions,
                HasConflict: distinct > 1);
        }
    }

    private static IEnumerable<EffectiveSetting> ResolveHooks(List<ScopeSettings> ordered)
    {
        var events = new List<string>();
        foreach (var s in ordered)
            if (Navigate(s.Root, new[] { "hooks" }) is JsonObject h)
                foreach (var kv in h)
                    if (!events.Contains(kv.Key)) events.Add(kv.Key);

        foreach (var ev in events)
        {
            var contributions = new List<SettingContribution>();
            var combined = new JsonArray();
            foreach (var s in ordered)
                if (Navigate(s.Root, new[] { "hooks" }) is JsonObject h
                    && h.TryGetPropertyValue(ev, out var v) && v is JsonArray arr)
                {
                    contributions.Add(new SettingContribution(
                        new SettingOrigin(s.Scope, s.FilePath, $"hooks.{ev}"),
                        arr.DeepClone()));
                    foreach (var item in arr)
                        combined.Add(item?.DeepClone());
                }

            yield return new EffectiveSetting(
                Key: $"hooks.{ev}",
                Strategy: MergeStrategy.ArrayConcat,
                Value: combined,
                Winner: null,
                Contributions: contributions,
                HasConflict: false);
        }
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter MergeEngineEnvHooksTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): env expansion and hooks concat merge"
```

---

## Task 11: EffectiveConfigService façade + end-to-end fixture

**Files:**
- Create: `src/ClaudeExplorer.Core/EffectiveConfigService.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/EffectiveConfigServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/EffectiveConfigServiceTests.cs`:
```csharp
using System.Text.Json.Nodes;
using ClaudeExplorer.Core;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests;

public class EffectiveConfigServiceTests
{
    private static InMemoryFileSystem Workspace()
        => new InMemoryFileSystem()
            .AddFile("/home/me/.claude/settings.json", """
            {
              "model": "opus",
              "permissions": { "defaultMode": "acceptEdits", "allow": ["Bash(git*)"] },
              "env": { "DISABLE_TELEMETRY": "1" },
              "statusLine": { "command": "~/bin/ccline" }
            }
            """)
            .AddFile("/repo/.claude/settings.json", """
            {
              "model": "sonnet",
              "permissions": { "allow": ["Read(src/**)"], "deny": ["Bash(rm -rf*)"] },
              "env": { "ANTHROPIC_LOG": "debug" },
              "outputStyle": "concise",
              "hooks": { "PreToolUse": [ { "matcher": "Bash" } ] }
            }
            """)
            .AddFile("/repo/.claude/settings.local.json", """
            { "permissions": { "allow": ["Bash(npm*)"] } }
            """);

    [Fact]
    public void Computes_effective_config_with_correct_precedence_and_merges()
    {
        var service = new EffectiveConfigService(Workspace());

        var cfg = service.Compute(userDir: "/home/me", projectDir: "/repo");

        // scalar precedence: project overrides user, flagged as conflict
        var model = cfg.Find("model")!;
        Assert.Equal("sonnet", (string?)model.Value);
        Assert.Equal(ScopeKind.Project, model.Winner!.Scope);
        Assert.True(model.HasConflict);

        // user-only scalar
        Assert.Equal("acceptEdits", (string?)cfg.Find("permissions.defaultMode")!.Value);

        // project-only scalar
        Assert.Equal("concise", (string?)cfg.Find("outputStyle")!.Value);

        // list union across all three scopes
        var allow = ((JsonArray)cfg.Find("permissions.allow")!.Value!).Select(n => (string?)n).ToArray();
        Assert.Equal(new[] { "Bash(git*)", "Read(src/**)", "Bash(npm*)" }, allow);

        // deny present from project only
        Assert.Single((JsonArray)cfg.Find("permissions.deny")!.Value!);

        // env expanded
        Assert.Equal("1", (string?)cfg.Find("env.DISABLE_TELEMETRY")!.Value);
        Assert.Equal("debug", (string?)cfg.Find("env.ANTHROPIC_LOG")!.Value);

        // hooks concat (only project contributes here)
        Assert.Single((JsonArray)cfg.Find("hooks.PreToolUse")!.Value!);

        // provenance: every contribution points at a real file path
        Assert.All(cfg.Settings.SelectMany(s => s.Contributions),
            c => Assert.False(string.IsNullOrWhiteSpace(c.Origin.FilePath)));
    }

    [Fact]
    public void Empty_workspace_yields_empty_config()
    {
        var service = new EffectiveConfigService(new InMemoryFileSystem());
        var cfg = service.Compute("/home/me", "/repo");
        Assert.Empty(cfg.Settings);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter EffectiveConfigServiceTests`
Expected: FAIL — `EffectiveConfigService` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/EffectiveConfigService.cs`:
```csharp
using ClaudeExplorer.Core.Discovery;
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Merge;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Reading;

namespace ClaudeExplorer.Core;

/// <summary>
/// Top-level façade: locate settings files for a workspace, parse them, and compute the
/// effective merged configuration with provenance.
/// </summary>
public sealed class EffectiveConfigService
{
    private readonly SettingsLocator _locator;
    private readonly SettingsReader _reader;
    private readonly MergeEngine _engine;

    public EffectiveConfigService(IFileSystem fileSystem)
    {
        _locator = new SettingsLocator(fileSystem);
        _reader = new SettingsReader(fileSystem);
        _engine = new MergeEngine();
    }

    public EffectiveConfig Compute(string userDir, string projectDir, string? enterprisePath = null)
    {
        var files = _locator.Locate(userDir, projectDir, enterprisePath);
        var scopes = files
            .Select(f => new ScopeSettings(f.Scope, f.Path, _reader.Read(f)))
            .ToList();
        return _engine.Compute(scopes);
    }
}
```

- [ ] **Step 4: Run full test suite**

Run: `dotnet test`
Expected: PASS (all tests, including the 2 new ones).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): EffectiveConfigService end-to-end facade"
```

---

## Self-Review

**Spec coverage (vs CLAUDE.md "Discover — effective config + provenance"):**
- Effective merged config across scopes → Tasks 8–11. ✓
- Correct precedence (Enterprise>Local>Project>User) → `ScopeKind` (Task 2) + ordering (Task 8). ✓
- Per-key merge semantics (scalar last-wins / list union / hooks concat / env expansion) → Tasks 8–10. ✓
- Conflict detection → Tasks 8, 10. ✓
- Provenance (scope, file path, json path, all contributions, winner) → Task 3 model, populated throughout. ✓
- Testable without touching the real machine (`IFileSystem`) → Task 4. ✓

**Deferred to later plans (intentional, noted so they're not forgotten):**
- **Line numbers** in provenance (UI nicety) — needs a `Utf8JsonReader`-based locator; Plan 2/UI.
- **CLAUDE.md memory, commands/skills/agents, MCP/plugins** discovery — Plan 2.
- **Deep object merge** beyond `env` (only `env` is expanded; `statusLine` is treated as a scalar object) — revisit if a real key needs it.
- **`managed-settings.json` real-path resolution** per-OS — the locator accepts an explicit `enterprisePath`; OS default resolution lands with the platform-paths work in Plan 2.

**Placeholder scan:** none — every step has complete code/commands.

**Type consistency:** `EffectiveSetting`, `SettingContribution`, `SettingOrigin`, `ScopeSettings`, `MergeStrategy`, `MergeEngine.Compute`, `EffectiveConfig.Find` are used identically across all tasks. `Navigate`, `ResolveScalar`, `ResolveListUnion`, `ResolveEnv`, `ResolveHooks` are all private members of the one `MergeEngine` class.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-06-07-01-core-config-engine.md`.
Two execution options:

1. **Subagent-Driven (recommended)** — a fresh subagent per task, review between tasks, fast iteration.
2. **Inline Execution** — execute tasks in this session with checkpoints for review.
