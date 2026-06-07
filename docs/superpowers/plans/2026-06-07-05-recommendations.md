# Project-Fit Recommendations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Locally analyze a project into **signals** (language / framework / test-runner / database, each with linkable file **evidence**), match those signals against the Phase-4 catalog, exclude already-installed plugins, rank by **match-confidence**, bucket into *Strong / Worth considering / Already covered*, and optionally annotate with runtime (dependency) health — **local-only: project contents never leave the machine; only catalog metadata is used.**

**Architecture:** Builds on Phases 1–4. Adds a `Recommendations` namespace: pluggable `ISignalDetector`s (over the existing `IFileSystem` seam) aggregated by a `SignalDetectionService`; an `InstalledPluginsReader` (reads `~/.claude/plugins/cache/*/*`); a `RecommendationMatcher` (token-based signal↔`CatalogItem` matching → reasons + evidence + confidence + bucket); and a `RecommendationService` façade that also applies an optional dependency-health annotation. Fully fixture-driven via `InMemoryFileSystem`; no network, no project upload.

**Tech Stack:** .NET 10, C#, xUnit. Consumes Phase-4 `CatalogItem` (`ClaudeExplorer.Core.Catalog`). No new NuGet dependencies.

---

## Scope & decisions

- **Local-only (hard rule):** detectors read the project tree via `IFileSystem` and emit only small derived facts (signal value + file path/count). Nothing reads file *contents* into the result, and nothing leaves the machine. The catalog is passed in (Phase-4 metadata) — Phase 5 adds no network.
- **Evidence is mandatory:** every `Signal` carries `Evidence` (file path, optional count). Every `Recommendation` carries `Reasons`, each referencing the `Signal` that triggered it (so the UI can render evidence chips that deep-link to the source file). A catalog item with no matching signal is **not** recommended (no traceable reason → not shown).
- **Detectors (v1):** Language, Framework, TestRunner, Database — marker-file based, pluggable via `ISignalDetector`. **Deferred (noted):** issue-tracker refs and commit-pattern signals (need git history / a git-log seam, not a file-tree scan).
- **Matching:** token-based. Split each `CatalogItem`'s Name/Tags/Category/Summary into lowercase alphanumeric tokens; a signal `Value` that exactly matches a **name** token scores 1.0, a **tag/category** token 0.6, a **summary** token 0.3. A recommendation's confidence = the max matching weight; reasons accumulate one per matching signal. (No stemming/fuzzy matching — `postgres` ≠ `postgresql`; refine later.)
- **Buckets:** installed → **AlreadyCovered**; else confidence ≥ 0.8 → **Strong**; else **Consider** (*Worth considering*). Deduped by item name (highest confidence wins), sorted by confidence desc then name.
- **Installed exclusion:** "installed" = a plugin present in the on-disk cache `~/.claude/plugins/cache/<marketplace>/<plugin>/<version>/` (the `<plugin>` dir name). Matched against `CatalogItem.Name` ordinally (consistent with the codebase's name handling).
- **Dependency-health annotation:** the mechanism is wired now but its *requirement source* is deferred. `RecommendationService.Recommend` accepts an optional `itemRuntimes` resolver (`CatalogItem → required runtime names`) and a `runtimeAvailability` map (runtime → present?, built by the caller from the Phase-3 check); each recommendation is annotated with `RuntimeAnnotation(runtime, available)`. By default (no resolver) there are no annotations — catalog metadata does not declare runtime requirements until a plugin's manifest is fetched at install-time (Phase 6).

## File structure

- `src/ClaudeExplorer.Core/Recommendations/RecommendationModel.cs` — signals, evidence, recommendation records + enums.
- `src/ClaudeExplorer.Core/Recommendations/ISignalDetector.cs` — `ISignalDetector` + `SignalDetectorBase` + `LanguageSignalDetector`.
- `src/ClaudeExplorer.Core/Recommendations/TestRunnerSignalDetector.cs` — test-runner + `DatabaseSignalDetector` (same file).
- `src/ClaudeExplorer.Core/Recommendations/FrameworkSignalDetector.cs` — framework detector + `SignalDetectionService`.
- `src/ClaudeExplorer.Core/Recommendations/InstalledPluginsReader.cs` — installed plugin names from the cache.
- `src/ClaudeExplorer.Core/Recommendations/RecommendationMatcher.cs` — matching → `RecommendationResult`.
- `src/ClaudeExplorer.Core/Recommendations/RecommendationService.cs` — façade + runtime annotation.
- Tests under `tests/ClaudeExplorer.Core.Tests/Recommendations/`.

> **Note for the implementer:** `Xunit` is a GLOBAL using in the test project — do NOT add `using Xunit;`. `ImplicitUsings` is enabled (System, System.Linq, System.Collections.Generic, System.Text available without explicit usings). Paths use forward slashes. `InMemoryFileSystem` lives in `ClaudeExplorer.Core.Tests.Fakes`.

---

## Task 1: Recommendation domain model

**Files:**
- Create: `src/ClaudeExplorer.Core/Recommendations/RecommendationModel.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Recommendations/RecommendationModelTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Recommendations/RecommendationModelTests.cs`:
```csharp
using ClaudeExplorer.Core.Catalog;
using ClaudeExplorer.Core.Recommendations;

namespace ClaudeExplorer.Core.Tests.Recommendations;

public class RecommendationModelTests
{
    [Fact]
    public void Signal_carries_value_and_evidence_and_project_signals_filter_by_kind()
    {
        var sig = new Signal(SignalKind.TestRunner, "playwright",
            new[] { new Evidence("/proj/playwright.config.ts") });
        var lang = new Signal(SignalKind.Language, "typescript", new[] { new Evidence("/proj/tsconfig.json") });
        var ps = new ProjectSignals(new[] { sig, lang });

        Assert.Equal("playwright", sig.Value);
        Assert.Equal("/proj/playwright.config.ts", sig.Evidence[0].FilePath);
        Assert.Single(ps.OfKind(SignalKind.TestRunner));
    }

    [Fact]
    public void Evidence_supports_an_optional_count()
    {
        var ev = new Evidence("/proj/migrations/0001.sql", Count: 9);
        Assert.Equal(9, ev.Count);
        Assert.Null(new Evidence("/proj/x").Count);
    }

    [Fact]
    public void Result_buckets_filter_recommendations()
    {
        var src = new CatalogSource(CatalogSourceKind.GitHub, TrustLevel.Community, "o/r", "loc");
        CatalogItem Item(string n) => new(n, CatalogItemType.Plugin, null, null, null, null,
            Array.Empty<string>(), src, TrustLevel.Community);
        Recommendation Rec(string n, double c, RecommendationBucket b)
            => new(Item(n), Array.Empty<RecommendationReason>(), c, b, Array.Empty<RuntimeAnnotation>());

        var result = new RecommendationResult(new[]
        {
            Rec("a", 1.0, RecommendationBucket.Strong),
            Rec("b", 0.6, RecommendationBucket.Consider),
            Rec("c", 1.0, RecommendationBucket.AlreadyCovered),
        });

        Assert.Single(result.Strong);
        Assert.Single(result.Consider);
        Assert.Single(result.AlreadyCovered);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter RecommendationModelTests`
Expected: FAIL — model types don't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Recommendations/RecommendationModel.cs`:
```csharp
using ClaudeExplorer.Core.Catalog;

namespace ClaudeExplorer.Core.Recommendations;

/// <summary>The kind of locally-detected project signal.</summary>
public enum SignalKind { Language, Framework, TestRunner, Database }

/// <summary>A linkable piece of evidence for a signal: a source file (+ optional match count/detail).</summary>
public sealed record Evidence(string FilePath, int? Count = null, string? Detail = null);

/// <summary>A locally-detected fact about a project, with the evidence that produced it.</summary>
public sealed record Signal(SignalKind Kind, string Value, IReadOnlyList<Evidence> Evidence);

public sealed record ProjectSignals(IReadOnlyList<Signal> Signals)
{
    public IEnumerable<Signal> OfKind(SignalKind kind) => Signals.Where(s => s.Kind == kind);
}

/// <summary>Why an item was recommended: the triggering signal (carrying evidence) + a short label.</summary>
public sealed record RecommendationReason(Signal Signal, string Text);

/// <summary>A required runtime for an item and whether it is available on this machine.</summary>
public sealed record RuntimeAnnotation(string Runtime, bool Available);

public enum RecommendationBucket { Strong, Consider, AlreadyCovered }

public sealed record Recommendation(
    CatalogItem Item,
    IReadOnlyList<RecommendationReason> Reasons,
    double Confidence,
    RecommendationBucket Bucket,
    IReadOnlyList<RuntimeAnnotation> Runtimes);

public sealed record RecommendationResult(IReadOnlyList<Recommendation> Recommendations)
{
    public IEnumerable<Recommendation> Strong => Recommendations.Where(r => r.Bucket == RecommendationBucket.Strong);
    public IEnumerable<Recommendation> Consider => Recommendations.Where(r => r.Bucket == RecommendationBucket.Consider);
    public IEnumerable<Recommendation> AlreadyCovered => Recommendations.Where(r => r.Bucket == RecommendationBucket.AlreadyCovered);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter RecommendationModelTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): recommendation domain model"
```

---

## Task 2: ISignalDetector seam + language detector

**Files:**
- Create: `src/ClaudeExplorer.Core/Recommendations/ISignalDetector.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Recommendations/LanguageSignalDetectorTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Recommendations/LanguageSignalDetectorTests.cs`:
```csharp
using ClaudeExplorer.Core.Recommendations;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Recommendations;

public class LanguageSignalDetectorTests
{
    [Fact]
    public void Detects_js_and_ts_from_marker_files_with_evidence()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/proj/package.json", "{}")
            .AddFile("/proj/tsconfig.json", "{}");

        var signals = new LanguageSignalDetector(fs).Detect("/proj");

        var ts = signals.Single(s => s.Value == "typescript");
        Assert.Equal(SignalKind.Language, ts.Kind);
        Assert.Equal("/proj/tsconfig.json", ts.Evidence[0].FilePath);
        Assert.Contains(signals, s => s.Value == "javascript");
    }

    [Fact]
    public void Detects_csharp_from_csproj_with_count()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/proj/src/App.csproj", "<Project/>")
            .AddFile("/proj/tests/Tests.csproj", "<Project/>");

        var cs = new LanguageSignalDetector(fs).Detect("/proj").Single();
        Assert.Equal("csharp", cs.Value);
        Assert.Equal(2, cs.Evidence[0].Count);
    }

    [Fact]
    public void Empty_project_yields_no_signals()
    {
        Assert.Empty(new LanguageSignalDetector(new InMemoryFileSystem()).Detect("/proj"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter LanguageSignalDetectorTests`
Expected: FAIL — `ISignalDetector`/`LanguageSignalDetector` don't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Recommendations/ISignalDetector.cs`:
```csharp
using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Recommendations;

/// <summary>Detects one family of project signals from a project tree (local, read-only).</summary>
public interface ISignalDetector
{
    IReadOnlyList<Signal> Detect(string projectDir);
}

/// <summary>Shared helpers for marker-file detectors.</summary>
public abstract class SignalDetectorBase
{
    protected readonly IFileSystem Fs;
    protected SignalDetectorBase(IFileSystem fs) => Fs = fs;

    /// <summary>The first of <paramref name="relativeNames"/> that exists under the project, else null.</summary>
    protected string? FirstExisting(string projectDir, params string[] relativeNames)
    {
        foreach (var name in relativeNames)
        {
            var path = $"{projectDir}/{name}";
            if (Fs.FileExists(path)) return path;
        }
        return null;
    }
}

/// <summary>Detects programming languages from well-known marker files.</summary>
public sealed class LanguageSignalDetector : SignalDetectorBase, ISignalDetector
{
    public LanguageSignalDetector(IFileSystem fs) : base(fs) { }

    public IReadOnlyList<Signal> Detect(string projectDir)
    {
        var signals = new List<Signal>();
        void Add(string value, string? file)
        {
            if (file is not null)
                signals.Add(new Signal(SignalKind.Language, value, new[] { new Evidence(file) }));
        }

        Add("javascript", FirstExisting(projectDir, "package.json"));
        Add("typescript", FirstExisting(projectDir, "tsconfig.json"));
        Add("python", FirstExisting(projectDir, "pyproject.toml", "requirements.txt", "setup.py"));
        Add("go", FirstExisting(projectDir, "go.mod"));
        Add("rust", FirstExisting(projectDir, "Cargo.toml"));
        Add("java", FirstExisting(projectDir, "pom.xml", "build.gradle"));

        var csproj = Fs.GetFiles(projectDir, "*.csproj", recurse: true);
        if (csproj.Count > 0)
            signals.Add(new Signal(SignalKind.Language, "csharp", new[] { new Evidence(csproj[0], csproj.Count) }));

        return signals;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter LanguageSignalDetectorTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): ISignalDetector seam + language detector"
```

---

## Task 3: Test-runner + database detectors

**Files:**
- Create: `src/ClaudeExplorer.Core/Recommendations/TestRunnerSignalDetector.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Recommendations/TestRunnerAndDatabaseDetectorTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Recommendations/TestRunnerAndDatabaseDetectorTests.cs`:
```csharp
using ClaudeExplorer.Core.Recommendations;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Recommendations;

public class TestRunnerAndDatabaseDetectorTests
{
    [Fact]
    public void Detects_playwright_and_pytest()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/proj/playwright.config.ts", "x")
            .AddFile("/proj/conftest.py", "x");

        var signals = new TestRunnerSignalDetector(fs).Detect("/proj");

        var pw = signals.Single(s => s.Value == "playwright");
        Assert.Equal(SignalKind.TestRunner, pw.Kind);
        Assert.Equal("/proj/playwright.config.ts", pw.Evidence[0].FilePath);
        Assert.Contains(signals, s => s.Value == "pytest");
    }

    [Fact]
    public void Detects_prisma_and_sql_migrations_with_count()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/proj/prisma/schema.prisma", "x")
            .AddFile("/proj/migrations/0001_init.sql", "x")
            .AddFile("/proj/migrations/0002_more.sql", "x");

        var signals = new DatabaseSignalDetector(fs).Detect("/proj");

        Assert.Contains(signals, s => s.Value == "prisma");
        var sql = signals.Single(s => s.Value == "sql");
        Assert.Equal(2, sql.Evidence[0].Count);
    }

    [Fact]
    public void No_test_or_db_markers_yields_nothing()
    {
        var fs = new InMemoryFileSystem().AddFile("/proj/readme.md", "x");
        Assert.Empty(new TestRunnerSignalDetector(fs).Detect("/proj"));
        Assert.Empty(new DatabaseSignalDetector(fs).Detect("/proj"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter TestRunnerAndDatabaseDetectorTests`
Expected: FAIL — detectors don't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Recommendations/TestRunnerSignalDetector.cs`:
```csharp
using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Recommendations;

/// <summary>Detects test runners from their config files.</summary>
public sealed class TestRunnerSignalDetector : SignalDetectorBase, ISignalDetector
{
    public TestRunnerSignalDetector(IFileSystem fs) : base(fs) { }

    public IReadOnlyList<Signal> Detect(string projectDir)
    {
        var signals = new List<Signal>();
        void Add(string value, string? file)
        {
            if (file is not null)
                signals.Add(new Signal(SignalKind.TestRunner, value, new[] { new Evidence(file) }));
        }

        Add("playwright", FirstExisting(projectDir, "playwright.config.ts", "playwright.config.js"));
        Add("jest", FirstExisting(projectDir,
            "jest.config.js", "jest.config.ts", "jest.config.mjs", "jest.config.cjs", "jest.config.json"));
        Add("vitest", FirstExisting(projectDir, "vitest.config.ts", "vitest.config.js"));
        Add("pytest", FirstExisting(projectDir, "pytest.ini", "conftest.py"));

        return signals;
    }
}

/// <summary>Detects databases/ORMs from marker files and SQL migrations.</summary>
public sealed class DatabaseSignalDetector : SignalDetectorBase, ISignalDetector
{
    public DatabaseSignalDetector(IFileSystem fs) : base(fs) { }

    public IReadOnlyList<Signal> Detect(string projectDir)
    {
        var signals = new List<Signal>();

        var prisma = $"{projectDir}/prisma/schema.prisma";
        if (Fs.FileExists(prisma))
            signals.Add(new Signal(SignalKind.Database, "prisma", new[] { new Evidence(prisma) }));

        var migrations = Fs.GetFiles($"{projectDir}/migrations", "*.sql", recurse: true);
        if (migrations.Count > 0)
            signals.Add(new Signal(SignalKind.Database, "sql",
                new[] { new Evidence(migrations[0], migrations.Count, "migrations/*.sql") }));

        var knex = FirstExisting(projectDir, "knexfile.js", "knexfile.ts");
        if (knex is not null)
            signals.Add(new Signal(SignalKind.Database, "knex", new[] { new Evidence(knex) }));

        return signals;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter TestRunnerAndDatabaseDetectorTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): test-runner + database signal detectors"
```

---

## Task 4: Framework detector + signal-detection service

**Files:**
- Create: `src/ClaudeExplorer.Core/Recommendations/FrameworkSignalDetector.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Recommendations/SignalDetectionServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Recommendations/SignalDetectionServiceTests.cs`:
```csharp
using ClaudeExplorer.Core.Recommendations;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Recommendations;

public class SignalDetectionServiceTests
{
    [Fact]
    public void Framework_detector_finds_nextjs()
    {
        var fs = new InMemoryFileSystem().AddFile("/proj/next.config.js", "x");
        var sig = new FrameworkSignalDetector(fs).Detect("/proj").Single();
        Assert.Equal(SignalKind.Framework, sig.Kind);
        Assert.Equal("nextjs", sig.Value);
    }

    [Fact]
    public void Service_aggregates_all_detectors_into_project_signals()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/proj/package.json", "{}")
            .AddFile("/proj/next.config.js", "x")
            .AddFile("/proj/playwright.config.ts", "x")
            .AddFile("/proj/migrations/0001.sql", "x");

        var ps = new SignalDetectionService(fs).Detect("/proj");

        Assert.Contains(ps.Signals, s => s.Kind == SignalKind.Language && s.Value == "javascript");
        Assert.Contains(ps.Signals, s => s.Kind == SignalKind.Framework && s.Value == "nextjs");
        Assert.Contains(ps.Signals, s => s.Kind == SignalKind.TestRunner && s.Value == "playwright");
        Assert.Contains(ps.Signals, s => s.Kind == SignalKind.Database && s.Value == "sql");
    }

    [Fact]
    public void Service_accepts_a_custom_detector_set()
    {
        var ps = new SignalDetectionService(Array.Empty<ISignalDetector>()).Detect("/proj");
        Assert.Empty(ps.Signals);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter SignalDetectionServiceTests`
Expected: FAIL — `FrameworkSignalDetector`/`SignalDetectionService` don't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Recommendations/FrameworkSignalDetector.cs`:
```csharp
using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Recommendations;

/// <summary>Detects web frameworks from their config files.</summary>
public sealed class FrameworkSignalDetector : SignalDetectorBase, ISignalDetector
{
    public FrameworkSignalDetector(IFileSystem fs) : base(fs) { }

    public IReadOnlyList<Signal> Detect(string projectDir)
    {
        var signals = new List<Signal>();
        void Add(string value, string? file)
        {
            if (file is not null)
                signals.Add(new Signal(SignalKind.Framework, value, new[] { new Evidence(file) }));
        }

        Add("nextjs", FirstExisting(projectDir, "next.config.js", "next.config.ts", "next.config.mjs"));
        Add("nuxt", FirstExisting(projectDir, "nuxt.config.js", "nuxt.config.ts"));
        Add("angular", FirstExisting(projectDir, "angular.json"));
        Add("astro", FirstExisting(projectDir, "astro.config.js", "astro.config.ts", "astro.config.mjs"));
        Add("svelte", FirstExisting(projectDir, "svelte.config.js", "svelte.config.ts"));

        return signals;
    }
}

/// <summary>Runs all signal detectors and aggregates their output into <see cref="ProjectSignals"/>.</summary>
public sealed class SignalDetectionService
{
    private readonly IReadOnlyList<ISignalDetector> _detectors;

    public SignalDetectionService(IFileSystem fs)
        => _detectors = new ISignalDetector[]
        {
            new LanguageSignalDetector(fs),
            new FrameworkSignalDetector(fs),
            new TestRunnerSignalDetector(fs),
            new DatabaseSignalDetector(fs),
        };

    /// <summary>Overload for a custom detector set (extensibility / testing).</summary>
    public SignalDetectionService(IReadOnlyList<ISignalDetector> detectors) => _detectors = detectors;

    public ProjectSignals Detect(string projectDir)
        => new(_detectors.SelectMany(d => d.Detect(projectDir)).ToList());
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter SignalDetectionServiceTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): framework detector + signal-detection service"
```

---

## Task 5: Installed-plugins reader

**Files:**
- Create: `src/ClaudeExplorer.Core/Recommendations/InstalledPluginsReader.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Recommendations/InstalledPluginsReaderTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Recommendations/InstalledPluginsReaderTests.cs`:
```csharp
using ClaudeExplorer.Core.Recommendations;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Recommendations;

public class InstalledPluginsReaderTests
{
    [Fact]
    public void Reads_plugin_names_from_the_cache_across_marketplaces()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/plugins/cache/claude-plugins-official/feature-dev/unknown/.claude-plugin/plugin.json", "{}")
            .AddFile("/home/.claude/plugins/cache/claude-plugins-official/superpowers/5.1.0/plugin.json", "{}")
            .AddFile("/home/.claude/plugins/cache/unifi-plugins/unifi-network/0.17.3/.mcp.json", "{}");

        var installed = new InstalledPluginsReader(fs).Read("/home");

        Assert.Contains("feature-dev", installed);
        Assert.Contains("superpowers", installed);
        Assert.Contains("unifi-network", installed);
        Assert.DoesNotContain("claude-plugins-official", installed); // marketplace dir, not a plugin
    }

    [Fact]
    public void No_cache_yields_empty_set()
    {
        Assert.Empty(new InstalledPluginsReader(new InMemoryFileSystem()).Read("/home"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter InstalledPluginsReaderTests`
Expected: FAIL — `InstalledPluginsReader` doesn't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Recommendations/InstalledPluginsReader.cs`:
```csharp
using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Recommendations;

/// <summary>
/// The set of installed plugin names, read from the on-disk plugin cache
/// <c>{userDir}/.claude/plugins/cache/&lt;marketplace&gt;/&lt;plugin&gt;/&lt;version&gt;/</c>. Local only.
/// </summary>
public sealed class InstalledPluginsReader
{
    private readonly IFileSystem _fs;

    public InstalledPluginsReader(IFileSystem fs) => _fs = fs;

    public IReadOnlySet<string> Read(string userDir)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        var cache = $"{userDir}/.claude/plugins/cache";

        foreach (var marketplaceDir in _fs.GetDirectories(cache))
            foreach (var pluginDir in _fs.GetDirectories(marketplaceDir))
                set.Add(LastSegment(pluginDir));

        return set;
    }

    private static string LastSegment(string path)
    {
        var trimmed = path.TrimEnd('/');
        var i = trimmed.LastIndexOf('/');
        return i >= 0 ? trimmed.Substring(i + 1) : trimmed;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter InstalledPluginsReaderTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): installed-plugins reader"
```

---

## Task 6: Recommendation matcher

**Files:**
- Create: `src/ClaudeExplorer.Core/Recommendations/RecommendationMatcher.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Recommendations/RecommendationMatcherTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Recommendations/RecommendationMatcherTests.cs`:
```csharp
using ClaudeExplorer.Core.Catalog;
using ClaudeExplorer.Core.Recommendations;

namespace ClaudeExplorer.Core.Tests.Recommendations;

public class RecommendationMatcherTests
{
    private static readonly CatalogSource Src =
        new(CatalogSourceKind.ClaudeMarketplace, TrustLevel.Verified, "official", "/p");

    private static CatalogItem Item(string name, string? category = null,
        IReadOnlyList<string>? tags = null, string? summary = null)
        => new(name, CatalogItemType.Plugin, summary, null, category, null,
            tags ?? Array.Empty<string>(), Src, TrustLevel.Verified);

    private static ProjectSignals Signals(params Signal[] s) => new(s);
    private static Signal Sig(SignalKind k, string v) => new(k, v, new[] { new Evidence($"/proj/{v}") });

    [Fact]
    public void Name_token_match_is_strong_tag_match_is_consider_and_no_match_is_excluded()
    {
        var signals = Signals(
            Sig(SignalKind.TestRunner, "playwright"),
            Sig(SignalKind.Database, "sql"));
        var catalog = new[]
        {
            Item("playwright"),                                  // name token -> Strong
            Item("db-toolkit", tags: new[] { "sql" }),           // tag token  -> Consider
            Item("unrelated", summary: "nothing here"),          // no match   -> excluded
        };

        var result = new RecommendationMatcher().Match(signals, catalog, new HashSet<string>());

        var pw = result.Recommendations.Single(r => r.Item.Name == "playwright");
        Assert.Equal(RecommendationBucket.Strong, pw.Bucket);
        Assert.Equal(1.0, pw.Confidence);
        Assert.Single(pw.Reasons);
        Assert.Equal("playwright", pw.Reasons[0].Signal.Value);
        Assert.Equal("/proj/playwright", pw.Reasons[0].Signal.Evidence[0].FilePath);

        Assert.Equal(RecommendationBucket.Consider, result.Recommendations.Single(r => r.Item.Name == "db-toolkit").Bucket);
        Assert.DoesNotContain(result.Recommendations, r => r.Item.Name == "unrelated");
    }

    [Fact]
    public void Installed_items_are_bucketed_already_covered()
    {
        var signals = Signals(Sig(SignalKind.TestRunner, "playwright"));
        var catalog = new[] { Item("playwright") };

        var result = new RecommendationMatcher().Match(signals, catalog,
            new HashSet<string>(StringComparer.Ordinal) { "playwright" });

        Assert.Equal(RecommendationBucket.AlreadyCovered, result.Recommendations.Single().Bucket);
    }

    [Fact]
    public void Results_are_sorted_by_confidence_then_name_and_deduped_by_name()
    {
        var signals = Signals(Sig(SignalKind.Language, "typescript"), Sig(SignalKind.Database, "sql"));
        var catalog = new[]
        {
            Item("ts-helper", summary: "for typescript"),     // summary -> 0.3
            Item("typescript", tags: new[] { "sql" }),        // name token "typescript" 1.0 (beats tag)
            Item("typescript", summary: "dup"),               // duplicate name -> deduped, lower confidence dropped
        };

        var ordered = new RecommendationMatcher().Match(signals, catalog, new HashSet<string>())
            .Recommendations.Select(r => r.Item.Name).ToArray();

        Assert.Equal(new[] { "typescript", "ts-helper" }, ordered);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter RecommendationMatcherTests`
Expected: FAIL — `RecommendationMatcher` doesn't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Recommendations/RecommendationMatcher.cs`:
```csharp
using System.Text;
using ClaudeExplorer.Core.Catalog;

namespace ClaudeExplorer.Core.Recommendations;

/// <summary>
/// Matches project signals against catalog items by token overlap, producing ranked, bucketed
/// recommendations. Name-token match = 1.0, tag/category-token = 0.6, summary-token = 0.3; a
/// recommendation's confidence is its strongest match. Items with no matching signal are dropped
/// (no traceable reason → not shown). Installed items are bucketed AlreadyCovered.
/// </summary>
public sealed class RecommendationMatcher
{
    private const double NameWeight = 1.0;
    private const double TagWeight = 0.6;
    private const double SummaryWeight = 0.3;
    private const double StrongThreshold = 0.8;

    public RecommendationResult Match(
        ProjectSignals signals,
        IReadOnlyList<CatalogItem> catalog,
        IReadOnlySet<string> installedPluginNames)
    {
        var recs = new List<Recommendation>();

        foreach (var item in catalog)
        {
            var nameTokens = ToTokenSet(item.Name);
            var tagTokens = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tag in item.Tags) tagTokens.UnionWith(Tokenize(tag));
            tagTokens.UnionWith(Tokenize(item.Category));
            var summaryTokens = ToTokenSet(item.Summary);

            var reasons = new List<RecommendationReason>();
            double confidence = 0;

            foreach (var signal in signals.Signals)
            {
                var v = signal.Value.ToLowerInvariant();
                double weight =
                    nameTokens.Contains(v) ? NameWeight
                    : tagTokens.Contains(v) ? TagWeight
                    : summaryTokens.Contains(v) ? SummaryWeight
                    : 0;
                if (weight == 0) continue;

                reasons.Add(new RecommendationReason(signal, $"Matches {signal.Kind} '{signal.Value}'"));
                confidence = Math.Max(confidence, weight);
            }

            if (reasons.Count == 0) continue;

            var bucket = installedPluginNames.Contains(item.Name)
                ? RecommendationBucket.AlreadyCovered
                : confidence >= StrongThreshold ? RecommendationBucket.Strong
                : RecommendationBucket.Consider;

            recs.Add(new Recommendation(item, reasons, confidence, bucket, Array.Empty<RuntimeAnnotation>()));
        }

        var ordered = recs
            .GroupBy(r => r.Item.Name, StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(r => r.Confidence).First())
            .OrderByDescending(r => r.Confidence)
            .ThenBy(r => r.Item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new RecommendationResult(ordered);
    }

    private static HashSet<string> ToTokenSet(string? text)
        => new(Tokenize(text), StringComparer.Ordinal);

    /// <summary>Lowercase alphanumeric tokens (split on every non-alphanumeric char).</summary>
    private static IEnumerable<string> Tokenize(string? text)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        var sb = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            else if (sb.Length > 0) { yield return sb.ToString(); sb.Clear(); }
        }
        if (sb.Length > 0) yield return sb.ToString();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter RecommendationMatcherTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): recommendation matcher"
```

---

## Task 7: RecommendationService façade + integration

**Files:**
- Create: `src/ClaudeExplorer.Core/Recommendations/RecommendationService.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Recommendations/RecommendationServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Recommendations/RecommendationServiceTests.cs`:
```csharp
using ClaudeExplorer.Core.Catalog;
using ClaudeExplorer.Core.Recommendations;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Recommendations;

public class RecommendationServiceTests
{
    private static readonly CatalogSource Src =
        new(CatalogSourceKind.ClaudeMarketplace, TrustLevel.Verified, "official", "/p");

    private static CatalogItem Item(string name, IReadOnlyList<string>? tags = null, string? summary = null)
        => new(name, CatalogItemType.Plugin, summary, null, null, null,
            tags ?? Array.Empty<string>(), Src, TrustLevel.Verified);

    [Fact]
    public void End_to_end_signals_to_bucketed_recommendations_with_evidence_and_runtime_annotation()
    {
        var fs = new InMemoryFileSystem()
            // project under analysis
            .AddFile("/proj/tsconfig.json", "{}")
            .AddFile("/proj/playwright.config.ts", "x")
            .AddFile("/proj/migrations/0001.sql", "x")
            .AddFile("/proj/migrations/0002.sql", "x")
            // installed plugin cache (playwright already installed)
            .AddFile("/home/.claude/plugins/cache/official/playwright/1.0.0/plugin.json", "{}");

        var catalog = new[]
        {
            Item("playwright"),                              // matches TestRunner 'playwright' (Strong) but installed -> AlreadyCovered
            Item("typescript-helper"),                       // name token 'typescript' (Strong)
            Item("db-toolkit", tags: new[] { "sql" }),       // tag 'sql' (Consider)
            Item("unrelated", summary: "nothing"),           // no match -> excluded
        };

        var runtimeAvailability = new Dictionary<string, bool> { ["uvx"] = false };
        IReadOnlyList<string> ItemRuntimes(CatalogItem i) =>
            i.Name == "db-toolkit" ? new[] { "uvx" } : Array.Empty<string>();

        var result = new RecommendationService(fs)
            .Recommend("/home", "/proj", catalog, runtimeAvailability, ItemRuntimes);

        // Strong: typescript-helper
        var strong = Assert.Single(result.Strong);
        Assert.Equal("typescript-helper", strong.Item.Name);

        // Consider: db-toolkit, annotated needs uvx (missing)
        var consider = Assert.Single(result.Consider);
        Assert.Equal("db-toolkit", consider.Item.Name);
        var runtime = Assert.Single(consider.Runtimes);
        Assert.Equal("uvx", runtime.Runtime);
        Assert.False(runtime.Available);

        // Already covered: playwright (installed)
        Assert.Equal("playwright", Assert.Single(result.AlreadyCovered).Item.Name);

        // Excluded: unrelated
        Assert.DoesNotContain(result.Recommendations, r => r.Item.Name == "unrelated");

        // Evidence links back to a source file
        var sqlReason = consider.Reasons.Single(r => r.Signal.Value == "sql");
        Assert.Equal("/proj/migrations/0001.sql", sqlReason.Signal.Evidence[0].FilePath);
        Assert.Equal(2, sqlReason.Signal.Evidence[0].Count);
    }

    [Fact]
    public void Without_runtime_resolver_there_are_no_annotations()
    {
        var fs = new InMemoryFileSystem().AddFile("/proj/tsconfig.json", "{}");
        var catalog = new[] { Item("typescript-helper") };

        var result = new RecommendationService(fs).Recommend("/home", "/proj", catalog);

        Assert.Empty(Assert.Single(result.Recommendations).Runtimes);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter RecommendationServiceTests`
Expected: FAIL — `RecommendationService` doesn't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Recommendations/RecommendationService.cs`:
```csharp
using ClaudeExplorer.Core.Catalog;
using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Recommendations;

/// <summary>
/// Top-level façade: detect a project's signals locally, match them against the catalog (excluding
/// installed plugins), and optionally annotate each recommendation with runtime availability.
/// Local-only — the project tree is read but never uploaded; only the passed-in catalog metadata
/// is consulted.
/// </summary>
public sealed class RecommendationService
{
    private readonly SignalDetectionService _detection;
    private readonly InstalledPluginsReader _installed;
    private readonly RecommendationMatcher _matcher;

    public RecommendationService(IFileSystem fileSystem)
    {
        _detection = new SignalDetectionService(fileSystem);
        _installed = new InstalledPluginsReader(fileSystem);
        _matcher = new RecommendationMatcher();
    }

    /// <param name="runtimeAvailability">runtime name → is it present on this machine (from a Phase-3 check).</param>
    /// <param name="itemRuntimes">resolves the runtimes an item requires (default: none — see plan).</param>
    public RecommendationResult Recommend(
        string userDir,
        string projectDir,
        IReadOnlyList<CatalogItem> catalog,
        IReadOnlyDictionary<string, bool>? runtimeAvailability = null,
        Func<CatalogItem, IReadOnlyList<string>>? itemRuntimes = null)
    {
        var signals = _detection.Detect(projectDir);
        var installed = _installed.Read(userDir);
        var result = _matcher.Match(signals, catalog, installed);

        return itemRuntimes is null ? result : Annotate(result, runtimeAvailability, itemRuntimes);
    }

    private static RecommendationResult Annotate(
        RecommendationResult result,
        IReadOnlyDictionary<string, bool>? availability,
        Func<CatalogItem, IReadOnlyList<string>> itemRuntimes)
    {
        var annotated = result.Recommendations.Select(r =>
        {
            var needs = itemRuntimes(r.Item);
            if (needs.Count == 0) return r;
            var notes = needs
                .Select(rt => new RuntimeAnnotation(
                    rt, availability is not null && availability.TryGetValue(rt, out var ok) && ok))
                .ToList();
            return r with { Runtimes = notes };
        }).ToList();

        return new RecommendationResult(annotated);
    }
}
```

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test`
Expected: PASS — all Phase 1–4 tests plus the new Phase-5 tests are green.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): RecommendationService facade"
```

---

## Self-Review

**Spec coverage (roadmap Phase 5 deliverables):**
- Pluggable `ISignalDetector`s (language/framework/test-runner/DB) over fixture trees → `ProjectSignals` → Tasks 2–4. ✓
- Matcher rules (signal → catalog item w/ reason + evidence refs + confidence) → Task 6. ✓
- Exclude installed (from Phase-2 ecosystem; here via the plugin cache) → Tasks 5, 6. ✓
- Annotate with dep health (Phase 3) → Task 7 (mechanism + machine availability; requirement source deferred). ✓
- `RecommendationService` façade → Task 7. ✓
- **Rule** every recommendation carries why + linkable evidence → enforced: items with zero matching signals are dropped; each kept recommendation has `Reasons` whose `Signal.Evidence` points at source files → Tasks 1, 6. ✓
- **Local-only** (never upload project contents) → only `IFileSystem` reads + the passed-in catalog; no network in this namespace → Task 7. ✓
- Sections *Strong / Worth considering / Already covered* → `RecommendationBucket` + `RecommendationResult` filters → Tasks 1, 6. ✓
- Tests: signal detection from fixtures, matcher reasons/evidence, installed excluded, dep-health annotation → Tasks 2–7. ✓

**Deferred (noted, not forgotten):** issue-tracker & commit-pattern signals (need a git-log seam); fuzzy/synonym matching (`postgres`↔`postgresql`); capturing catalog `keywords` for matching (Phase-4 parser reads only `tags`); the runtime-requirement source for annotations (plugin manifest fetched at install-time, Phase 6); using Phase-2 artifact discovery as an alternate "installed" signal. These belong to later phases / CLA-16.

**Placeholder scan:** none — every code step contains complete code; every run step has an exact command + expected result.

**Type consistency:** `SignalKind {Language,Framework,TestRunner,Database}`, `Evidence(FilePath,Count?,Detail?)`, `Signal(Kind,Value,Evidence)`, `ProjectSignals(Signals)`+`OfKind`, `RecommendationReason(Signal,Text)`, `RuntimeAnnotation(Runtime,Available)`, `RecommendationBucket {Strong,Consider,AlreadyCovered}`, `Recommendation(Item,Reasons,Confidence,Bucket,Runtimes)`, `RecommendationResult(Recommendations)`+`Strong`/`Consider`/`AlreadyCovered`, `ISignalDetector.Detect`, `SignalDetectorBase.FirstExisting`, the four detectors, `SignalDetectionService(IFileSystem)` + `(IReadOnlyList<ISignalDetector>)` + `Detect`, `InstalledPluginsReader.Read`, `RecommendationMatcher.Match`, `RecommendationService.Recommend` are used identically across all tasks and match the Phase-4 `CatalogItem(Name,Type,Summary,Author,Category,Homepage,Tags,Source,Trust,Stats?)` and the `IFileSystem` seam (`FileExists`/`GetFiles`/`GetDirectories`).

---

## Execution Handoff

Plan saved to `docs/superpowers/plans/2026-06-07-05-recommendations.md`. Execute via superpowers:subagent-driven-development (one implementer for the cohesive engine, then spec + code-quality review), then finishing-a-development-branch — per the playbook in `docs/superpowers/HANDOFF.md`.
