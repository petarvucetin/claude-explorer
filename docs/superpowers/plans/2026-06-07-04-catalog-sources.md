# Catalog + User-Added Sources (Trust) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Read installable items from Claude's configured marketplaces (local, on disk) and from user-added sources (a `owner/repo`, a github.com URL, or any http(s) marketplace URL), normalize them into `CatalogItem`s with a **trust level** (Verified for the official Anthropic marketplace, Community for everything else) — **metadata-only: nothing is downloaded or run until install.**

**Architecture:** Builds on the Phase 1–3 Core library. Adds a `Catalog` namespace: a network seam `ICatalogFetcher` (+ `HttpCatalogFetcher` impl + fake — the only network boundary), a `SourceDetector` (string → typed `CatalogSource`), a `MarketplaceManifestParser` (`marketplace.json` text → `CatalogItem`s), a `MarketplaceTrust` policy, an `InstalledMarketplaceReader` (reads `~/.claude/plugins/marketplaces/*` via the existing `IFileSystem` seam), and a `CatalogService` façade. Fully fixture-driven; the `Http*` seam is the only code that touches the network and is intentionally not unit-tested (matching `PhysicalFileSystem`/`PhysicalProcessRunner`).

**Tech Stack:** .NET 10, C#, `System.Text.Json` (`System.Text.Json.Nodes`), `System.Net.Http.HttpClient` (in the `Http` fetcher only — part of the shared framework, no new package), `System.Text.RegularExpressions`, xUnit. No new NuGet dependencies.

---

## Scope & decisions

- **Two read paths:** (a) **installed marketplaces** already on disk at
  `{userDir}/.claude/plugins/marketplaces/*/.claude-plugin/marketplace.json`, read via `IFileSystem`
  (no network); (b) **user-added sources** — fetch the remote `marketplace.json` via `ICatalogFetcher`
  and normalize. Both produce `CatalogItem`s.
- **Marketplace manifest shape** (grounded in real files): top-level `name`, optional `owner.{name,email}`,
  optional `metadata.description`, and `plugins: [ { name, description?, author?:{name}, category?,
  homepage?, source, tags?:[...], ... } ]`. The `source` field (string path or `{source:"url|git-subdir|
  github", ...}`) is **not** resolved in Phase 4 (that's install-time, Phase 6) — we read metadata only.
- **CatalogItem.Type:** marketplace entries are **plugins**, so `Type = Plugin`. The enum also has
  `Skill`/`Agent` for future user-added bare-skills sources (deferred).
- **Trust (source-level):** Verified iff the marketplace name is the known official directory
  (`claude-plugins-official`) **or** its `owner.email` ends with `@anthropic.com` (case-insensitive);
  otherwise Community. User-added sources are always Community. Items inherit their source's trust. (A
  plugin tagged `community-managed` *inside* the Verified official marketplace still inherits Verified
  trust — the tag is surfaced separately via `CatalogItem.Tags`.)
- **Source detection:** `owner/repo` and `https://github.com/owner/repo[.git]` → **GitHub** kind, with
  `Location` = the raw manifest URL `https://raw.githubusercontent.com/{owner}/{repo}/HEAD/.claude-plugin/marketplace.json`;
  any other `http(s)://…` → **Url** kind, with `Location` = the URL as-is if it ends in `.json`, else
  `{url}/.claude-plugin/marketplace.json`. Unrecognized input throws `FormatException`. (The raw-URL
  convention is an assumption pinned by tests; refine against reality later.)
- **Metadata-only (hard rule):** browsing/adding fetches only manifest **metadata**; nothing is
  downloaded or executed. **Persisting** an added source and **installing** items go through the
  safe-mutation layer (Phase 6) — out of scope here. `FetchAddedSource` returns items for preview only.
- **Leniency:** malformed/missing manifests yield an empty list (never throw from parsing); plugin
  entries without a `name` are skipped. Items are deduped by `(Source.Name, Name)` and sorted.
- **Case sensitivity:** marketplace/plugin names matched ordinally (consistent with the codebase);
  trust/email/runtime-ish comparisons that are inherently case-insensitive use `OrdinalIgnoreCase` and
  are documented at the call site.

## File structure

- `src/ClaudeExplorer.Core/Catalog/ICatalogFetcher.cs` — `ICatalogFetcher` + `HttpCatalogFetcher`.
- `src/ClaudeExplorer.Core/Catalog/CatalogModel.cs` — enums + `CatalogSource` + `CatalogItem` + `CatalogItemStats`.
- `src/ClaudeExplorer.Core/Catalog/MarketplaceTrust.cs` — trust policy.
- `src/ClaudeExplorer.Core/Catalog/SourceDetector.cs` — string → `CatalogSource`.
- `src/ClaudeExplorer.Core/Catalog/MarketplaceManifestParser.cs` — manifest text → `CatalogItem`s.
- `src/ClaudeExplorer.Core/Catalog/InstalledMarketplaceReader.cs` — local marketplaces via `IFileSystem`.
- `src/ClaudeExplorer.Core/Catalog/CatalogService.cs` — façade.
- `tests/ClaudeExplorer.Core.Tests/Fakes/FakeCatalogFetcher.cs` — in-memory `ICatalogFetcher`.
- Tests under `tests/ClaudeExplorer.Core.Tests/Catalog/`.

> **Note for the implementer:** the test project has global usings for `Xunit` (existing tests use
> `[Fact]`/`[Theory]`/`Assert` with no `using Xunit;`). Do **not** add `using Xunit;`. `ImplicitUsings`
> is enabled in both projects (so `System`, `System.Linq`, `System.Collections.Generic`,
> `System.Net.Http`, `System.Threading.Tasks` are available without explicit usings); explicit usings
> shown below are for clarity and are harmless. Paths use forward slashes.

---

## Task 1: ICatalogFetcher seam + fake

**Files:**
- Create: `src/ClaudeExplorer.Core/Catalog/ICatalogFetcher.cs`
- Create: `tests/ClaudeExplorer.Core.Tests/Fakes/FakeCatalogFetcher.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Catalog/FakeCatalogFetcherTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Catalog/FakeCatalogFetcherTests.cs`:
```csharp
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Catalog;

public class FakeCatalogFetcherTests
{
    [Fact]
    public void Returns_canned_text_and_records_the_request()
    {
        var fetcher = new FakeCatalogFetcher().Add("https://x/marketplace.json", "{\"ok\":true}");

        var text = fetcher.FetchText("https://x/marketplace.json");

        Assert.Equal("{\"ok\":true}", text);
        Assert.Equal(new[] { "https://x/marketplace.json" }, fetcher.Requests);
    }

    [Fact]
    public void Unknown_url_returns_null_but_is_still_recorded()
    {
        var fetcher = new FakeCatalogFetcher();
        Assert.Null(fetcher.FetchText("https://missing"));
        Assert.Single(fetcher.Requests);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter FakeCatalogFetcherTests`
Expected: FAIL — `ICatalogFetcher`/`FakeCatalogFetcher` don't exist (compile error).

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Catalog/ICatalogFetcher.cs`:
```csharp
using System.Net.Http;

namespace ClaudeExplorer.Core.Catalog;

/// <summary>
/// Fetches raw manifest text from a remote source. The ONLY network boundary in the catalog engine;
/// faked in tests so nothing touches the network. Metadata-only — this fetches a manifest, it never
/// downloads or runs an item.
/// </summary>
public interface ICatalogFetcher
{
    /// <summary>The response body for <paramref name="url"/>, or <c>null</c> if the fetch failed.</summary>
    string? FetchText(string url);
}

/// <summary>
/// Real fetcher over HTTP(S). Not unit-tested (it touches the network), mirroring the other
/// <c>Physical*</c> seams. Performs only GET requests with a bounded timeout.
/// </summary>
public sealed class HttpCatalogFetcher : ICatalogFetcher, IDisposable
{
    private readonly HttpClient _http;

    public HttpCatalogFetcher(TimeSpan? timeout = null)
        => _http = new HttpClient { Timeout = timeout ?? TimeSpan.FromSeconds(15) };

    public string? FetchText(string url)
    {
        try
        {
            using var response = _http.GetAsync(url).GetAwaiter().GetResult();
            return response.IsSuccessStatusCode
                ? response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                : null;
        }
        catch (HttpRequestException) { return null; }
        catch (TaskCanceledException) { return null; } // includes timeout
    }

    public void Dispose() => _http.Dispose();
}
```

Create `tests/ClaudeExplorer.Core.Tests/Fakes/FakeCatalogFetcher.cs`:
```csharp
using ClaudeExplorer.Core.Catalog;

namespace ClaudeExplorer.Core.Tests.Fakes;

/// <summary>Deterministic catalog fetcher: returns canned manifest text per URL, records every request.</summary>
public sealed class FakeCatalogFetcher : ICatalogFetcher
{
    private readonly Dictionary<string, string> _responses = new(StringComparer.Ordinal);

    /// <summary>Every URL <see cref="FetchText"/> was asked for, in order.</summary>
    public List<string> Requests { get; } = new();

    public FakeCatalogFetcher Add(string url, string text)
    {
        _responses[url] = text;
        return this;
    }

    public string? FetchText(string url)
    {
        Requests.Add(url);
        return _responses.TryGetValue(url, out var t) ? t : null;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter FakeCatalogFetcherTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): ICatalogFetcher seam + fake"
```

---

## Task 2: Catalog domain model

**Files:**
- Create: `src/ClaudeExplorer.Core/Catalog/CatalogModel.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Catalog/CatalogModelTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Catalog/CatalogModelTests.cs`:
```csharp
using ClaudeExplorer.Core.Catalog;

namespace ClaudeExplorer.Core.Tests.Catalog;

public class CatalogModelTests
{
    [Fact]
    public void Source_carries_kind_trust_name_and_location()
    {
        var src = new CatalogSource(CatalogSourceKind.GitHub, TrustLevel.Community, "owner/repo",
            "https://raw.githubusercontent.com/owner/repo/HEAD/.claude-plugin/marketplace.json");

        Assert.Equal(CatalogSourceKind.GitHub, src.Kind);
        Assert.Equal(TrustLevel.Community, src.Trust);
        Assert.Equal("owner/repo", src.Name);
    }

    [Fact]
    public void Item_defaults_tags_empty_and_stats_null_and_keeps_fields()
    {
        var src = new CatalogSource(CatalogSourceKind.ClaudeMarketplace, TrustLevel.Verified, "official", "/p");
        var item = new CatalogItem(
            Name: "feature-dev",
            Type: CatalogItemType.Plugin,
            Summary: "Feature development workflow",
            Author: "Anthropic",
            Category: "development",
            Homepage: "https://example.com",
            Tags: new[] { "community-managed" },
            Source: src,
            Trust: src.Trust);

        Assert.Equal(CatalogItemType.Plugin, item.Type);
        Assert.Equal(TrustLevel.Verified, item.Trust);
        Assert.Equal("Anthropic", item.Author);
        Assert.Contains("community-managed", item.Tags);
        Assert.Null(item.Stats);
    }

    [Fact]
    public void Stats_are_optional_when_present()
    {
        var stats = new CatalogItemStats(Stars: 42);
        Assert.Equal(42, stats.Stars);
        Assert.Null(stats.Downloads);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter CatalogModelTests`
Expected: FAIL — model types don't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Catalog/CatalogModel.cs`:
```csharp
namespace ClaudeExplorer.Core.Catalog;

/// <summary>How a catalog source is reached.</summary>
public enum CatalogSourceKind { ClaudeMarketplace, Url, GitHub }

/// <summary>Trust level surfaced everywhere a source/item appears.</summary>
public enum TrustLevel { Verified, Community }

/// <summary>The kind of installable item.</summary>
public enum CatalogItemType { Plugin, Skill, Agent }

/// <summary>A source of installable items.</summary>
/// <param name="Location">For <see cref="CatalogSourceKind.ClaudeMarketplace"/>: the on-disk manifest
/// path. For Url/GitHub: the manifest URL to fetch.</param>
public sealed record CatalogSource(CatalogSourceKind Kind, TrustLevel Trust, string Name, string Location);

/// <summary>Reserved usage stats. Not populated from marketplace manifests in v1 (shape for the UI later).</summary>
public sealed record CatalogItemStats(long? Stars = null, long? Downloads = null);

/// <summary>A normalized installable item (metadata only). Inherits its source's trust.</summary>
public sealed record CatalogItem(
    string Name,
    CatalogItemType Type,
    string? Summary,
    string? Author,
    string? Category,
    string? Homepage,
    IReadOnlyList<string> Tags,
    CatalogSource Source,
    TrustLevel Trust,
    CatalogItemStats? Stats = null);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter CatalogModelTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): catalog domain model"
```

---

## Task 3: Marketplace trust policy

**Files:**
- Create: `src/ClaudeExplorer.Core/Catalog/MarketplaceTrust.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Catalog/MarketplaceTrustTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Catalog/MarketplaceTrustTests.cs`:
```csharp
using ClaudeExplorer.Core.Catalog;

namespace ClaudeExplorer.Core.Tests.Catalog;

public class MarketplaceTrustTests
{
    [Fact]
    public void Official_marketplace_name_is_verified()
    {
        Assert.Equal(TrustLevel.Verified, MarketplaceTrust.Classify("claude-plugins-official", null));
    }

    [Fact]
    public void Anthropic_owner_email_is_verified_case_insensitively()
    {
        Assert.Equal(TrustLevel.Verified, MarketplaceTrust.Classify("anything", "Support@Anthropic.com"));
    }

    [Theory]
    [InlineData("unifi-plugins", "unifi@privatly.net")]
    [InlineData("context-mode", "code.bm.ksglu@gmail.com")]
    [InlineData(null, null)]
    public void Everything_else_is_community(string? name, string? email)
    {
        Assert.Equal(TrustLevel.Community, MarketplaceTrust.Classify(name, email));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter MarketplaceTrustTests`
Expected: FAIL — `MarketplaceTrust` doesn't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Catalog/MarketplaceTrust.cs`:
```csharp
namespace ClaudeExplorer.Core.Catalog;

/// <summary>
/// Trust policy for marketplaces. The official Anthropic directory is Verified; everything the user
/// added is Community. Detected by the known official marketplace name or an @anthropic.com owner
/// email (executable-style case-insensitive match — emails/domains are not case-sensitive).
/// </summary>
public static class MarketplaceTrust
{
    private static readonly HashSet<string> OfficialNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "claude-plugins-official",
    };

    public static TrustLevel Classify(string? marketplaceName, string? ownerEmail)
    {
        if (marketplaceName is not null && OfficialNames.Contains(marketplaceName))
            return TrustLevel.Verified;
        if (ownerEmail is not null
            && ownerEmail.Trim().EndsWith("@anthropic.com", StringComparison.OrdinalIgnoreCase))
            return TrustLevel.Verified;
        return TrustLevel.Community;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter MarketplaceTrustTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): marketplace trust policy"
```

---

## Task 4: Source detector

**Files:**
- Create: `src/ClaudeExplorer.Core/Catalog/SourceDetector.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Catalog/SourceDetectorTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Catalog/SourceDetectorTests.cs`:
```csharp
using ClaudeExplorer.Core.Catalog;

namespace ClaudeExplorer.Core.Tests.Catalog;

public class SourceDetectorTests
{
    [Fact]
    public void Detects_owner_repo_as_github_with_raw_manifest_url_and_community_trust()
    {
        var src = SourceDetector.Detect("octocat/plugins");

        Assert.Equal(CatalogSourceKind.GitHub, src.Kind);
        Assert.Equal(TrustLevel.Community, src.Trust);
        Assert.Equal("octocat/plugins", src.Name);
        Assert.Equal("https://raw.githubusercontent.com/octocat/plugins/HEAD/.claude-plugin/marketplace.json",
            src.Location);
    }

    [Theory]
    [InlineData("https://github.com/octocat/plugins")]
    [InlineData("https://github.com/octocat/plugins.git")]
    [InlineData("https://github.com/octocat/plugins/")]
    public void Detects_github_urls(string input)
    {
        var src = SourceDetector.Detect(input);
        Assert.Equal(CatalogSourceKind.GitHub, src.Kind);
        Assert.Equal("octocat/plugins", src.Name);
        Assert.Equal(SourceDetector.RawGitHubManifestUrl("octocat", "plugins"), src.Location);
    }

    [Fact]
    public void Detects_plain_url_and_appends_manifest_path_when_not_json()
    {
        var src = SourceDetector.Detect("https://example.com/my-marketplace");
        Assert.Equal(CatalogSourceKind.Url, src.Kind);
        Assert.Equal(TrustLevel.Community, src.Trust);
        Assert.Equal("https://example.com/my-marketplace/.claude-plugin/marketplace.json", src.Location);
    }

    [Fact]
    public void Plain_url_pointing_at_json_is_used_as_is()
    {
        var src = SourceDetector.Detect("https://example.com/m/marketplace.json");
        Assert.Equal(CatalogSourceKind.Url, src.Kind);
        Assert.Equal("https://example.com/m/marketplace.json", src.Location);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a source!!")]
    public void Unrecognized_input_throws(string input)
    {
        Assert.Throws<FormatException>(() => SourceDetector.Detect(input));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter SourceDetectorTests`
Expected: FAIL — `SourceDetector` doesn't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Catalog/SourceDetector.cs`:
```csharp
using System.Text.RegularExpressions;

namespace ClaudeExplorer.Core.Catalog;

/// <summary>
/// Detects the type of a user-added source string and normalizes it into a <see cref="CatalogSource"/>
/// (always Community trust). Recognizes a github.com URL, a bare <c>owner/repo</c>, or any other
/// http(s) URL. <see cref="CatalogSource.Location"/> is the manifest URL to fetch.
/// </summary>
public static class SourceDetector
{
    private static readonly Regex OwnerRepo =
        new(@"^[A-Za-z0-9._-]+/[A-Za-z0-9._-]+$", RegexOptions.Compiled);

    private static readonly Regex GitHubUrl = new(
        @"^https?://github\.com/(?<owner>[A-Za-z0-9._-]+)/(?<repo>[A-Za-z0-9._-]+?)(?:\.git)?/?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static CatalogSource Detect(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new FormatException("Source is empty.");
        var s = input.Trim();

        var gh = GitHubUrl.Match(s);
        if (gh.Success)
            return GitHub(gh.Groups["owner"].Value, gh.Groups["repo"].Value);

        if (OwnerRepo.IsMatch(s))
        {
            var parts = s.Split('/');
            return GitHub(parts[0], parts[1]);
        }

        if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return new CatalogSource(CatalogSourceKind.Url, TrustLevel.Community, s, ManifestUrlFor(s));

        throw new FormatException(
            $"Unrecognized source: '{input}'. Expected owner/repo, a github.com URL, or an http(s) URL.");
    }

    /// <summary>Raw URL of a GitHub repo's <c>.claude-plugin/marketplace.json</c> at HEAD.</summary>
    public static string RawGitHubManifestUrl(string owner, string repo)
        => $"https://raw.githubusercontent.com/{owner}/{repo}/HEAD/.claude-plugin/marketplace.json";

    private static CatalogSource GitHub(string owner, string repo)
        => new(CatalogSourceKind.GitHub, TrustLevel.Community, $"{owner}/{repo}",
            RawGitHubManifestUrl(owner, repo));

    private static string ManifestUrlFor(string url)
        => url.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? url
            : $"{url.TrimEnd('/')}/.claude-plugin/marketplace.json";
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter SourceDetectorTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): user-added source detector"
```

---

## Task 5: Marketplace manifest parser

**Files:**
- Create: `src/ClaudeExplorer.Core/Catalog/MarketplaceManifestParser.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Catalog/MarketplaceManifestParserTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Catalog/MarketplaceManifestParserTests.cs`:
```csharp
using ClaudeExplorer.Core.Catalog;

namespace ClaudeExplorer.Core.Tests.Catalog;

public class MarketplaceManifestParserTests
{
    private static CatalogSource Src(TrustLevel trust = TrustLevel.Community)
        => new(CatalogSourceKind.ClaudeMarketplace, trust, "mkt", "/p");

    private const string Manifest = """
        {
          "name": "mkt",
          "owner": { "name": "Owner", "email": "owner@example.com" },
          "plugins": [
            {
              "name": "feature-dev",
              "description": "Feature development workflow",
              "author": { "name": "Anthropic" },
              "category": "development",
              "homepage": "https://example.com/fd",
              "tags": ["community-managed"],
              "source": "./plugins/feature-dev"
            },
            {
              "name": "minimal",
              "source": { "source": "url", "url": "https://github.com/x/y.git" }
            },
            {
              "description": "no name -> skipped"
            }
          ]
        }
        """;

    [Fact]
    public void Parses_plugins_into_items_with_fields_and_inherited_trust()
    {
        var items = MarketplaceManifestParser.Parse(Manifest, Src(TrustLevel.Verified));

        Assert.Equal(2, items.Count); // the unnamed entry is skipped

        var fd = items.Single(i => i.Name == "feature-dev");
        Assert.Equal(CatalogItemType.Plugin, fd.Type);
        Assert.Equal("Feature development workflow", fd.Summary);
        Assert.Equal("Anthropic", fd.Author);
        Assert.Equal("development", fd.Category);
        Assert.Equal("https://example.com/fd", fd.Homepage);
        Assert.Contains("community-managed", fd.Tags);
        Assert.Equal(TrustLevel.Verified, fd.Trust);

        var minimal = items.Single(i => i.Name == "minimal");
        Assert.Null(minimal.Summary);
        Assert.Null(minimal.Author);
        Assert.Empty(minimal.Tags);
    }

    [Fact]
    public void ReadHeader_returns_name_and_owner_email()
    {
        var (name, email) = MarketplaceManifestParser.ReadHeader(Manifest);
        Assert.Equal("mkt", name);
        Assert.Equal("owner@example.com", email);
    }

    [Fact]
    public void Malformed_or_empty_manifest_yields_no_items()
    {
        Assert.Empty(MarketplaceManifestParser.Parse("{ not json", Src()));
        Assert.Empty(MarketplaceManifestParser.Parse(null, Src()));
        Assert.Empty(MarketplaceManifestParser.Parse("""{ "name": "x" }""", Src())); // no plugins array
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter MarketplaceManifestParserTests`
Expected: FAIL — `MarketplaceManifestParser` doesn't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Catalog/MarketplaceManifestParser.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClaudeExplorer.Core.Catalog;

/// <summary>
/// Parses a marketplace manifest (the <c>.claude-plugin/marketplace.json</c> shape) into normalized
/// <see cref="CatalogItem"/>s. Lenient: malformed/empty JSON yields an empty list; entries without a
/// name are skipped. Items inherit the source's trust. The <c>source</c> field is not resolved here
/// (that is install-time, Phase 6).
/// </summary>
public static class MarketplaceManifestParser
{
    private static readonly JsonDocumentOptions DocOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>The marketplace <c>name</c> and <c>owner.email</c> (for trust classification).</summary>
    public static (string? Name, string? OwnerEmail) ReadHeader(string? manifestText)
    {
        var root = TryParse(manifestText);
        if (root is null) return (null, null);
        var name = (string?)root["name"];
        var email = root["owner"] is JsonObject owner ? (string?)owner["email"] : null;
        return (name, email);
    }

    public static IReadOnlyList<CatalogItem> Parse(string? manifestText, CatalogSource source)
    {
        var root = TryParse(manifestText);
        if (root?["plugins"] is not JsonArray plugins) return Array.Empty<CatalogItem>();

        var items = new List<CatalogItem>();
        foreach (var node in plugins)
        {
            if (node is not JsonObject p) continue;
            var name = (string?)p["name"];
            if (string.IsNullOrWhiteSpace(name)) continue;

            items.Add(new CatalogItem(
                Name: name,
                Type: CatalogItemType.Plugin,
                Summary: (string?)p["description"],
                Author: p["author"] is JsonObject a ? (string?)a["name"] : null,
                Category: (string?)p["category"],
                Homepage: (string?)p["homepage"],
                Tags: ReadStringArray(p["tags"]),
                Source: source,
                Trust: source.Trust));
        }
        return items;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonNode? node)
        => node is JsonArray arr
            ? arr.Select(x => (string?)x).Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).ToList()
            : Array.Empty<string>();

    private static JsonObject? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try { return JsonNode.Parse(text, nodeOptions: null, documentOptions: DocOptions) as JsonObject; }
        catch (JsonException) { return null; }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter MarketplaceManifestParserTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): marketplace manifest parser"
```

---

## Task 6: Installed-marketplace reader (local)

**Files:**
- Create: `src/ClaudeExplorer.Core/Catalog/InstalledMarketplaceReader.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Catalog/InstalledMarketplaceReaderTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Catalog/InstalledMarketplaceReaderTests.cs`:
```csharp
using ClaudeExplorer.Core.Catalog;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Catalog;

public class InstalledMarketplaceReaderTests
{
    [Fact]
    public void Reads_official_as_verified_and_community_as_community()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/plugins/marketplaces/claude-plugins-official/.claude-plugin/marketplace.json",
                """
                {
                  "name": "claude-plugins-official",
                  "owner": { "name": "Anthropic", "email": "support@anthropic.com" },
                  "plugins": [ { "name": "feature-dev", "description": "wf" } ]
                }
                """)
            .AddFile("/home/.claude/plugins/marketplaces/unifi-plugins/.claude-plugin/marketplace.json",
                """
                {
                  "name": "unifi-plugins",
                  "owner": { "name": "sirkirby", "email": "unifi@privatly.net" },
                  "plugins": [ { "name": "unifi-network", "description": "net" } ]
                }
                """);

        var items = new InstalledMarketplaceReader(fs).Read("/home");

        var fd = items.Single(i => i.Name == "feature-dev");
        Assert.Equal(TrustLevel.Verified, fd.Trust);
        Assert.Equal(CatalogSourceKind.ClaudeMarketplace, fd.Source.Kind);
        Assert.Equal("claude-plugins-official", fd.Source.Name);

        var net = items.Single(i => i.Name == "unifi-network");
        Assert.Equal(TrustLevel.Community, net.Trust);
        Assert.Equal("unifi-plugins", net.Source.Name);
    }

    [Fact]
    public void Marketplace_directory_without_a_manifest_is_skipped()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/plugins/marketplaces/broken/README.md", "no manifest here");

        Assert.Empty(new InstalledMarketplaceReader(fs).Read("/home"));
    }

    [Fact]
    public void No_marketplaces_directory_yields_empty()
    {
        Assert.Empty(new InstalledMarketplaceReader(new InMemoryFileSystem()).Read("/home"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter InstalledMarketplaceReaderTests`
Expected: FAIL — `InstalledMarketplaceReader` doesn't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Catalog/InstalledMarketplaceReader.cs`:
```csharp
using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Catalog;

/// <summary>
/// Reads the marketplaces already configured on this machine, from
/// <c>{userDir}/.claude/plugins/marketplaces/*/.claude-plugin/marketplace.json</c>. Local only — no
/// network. Official Anthropic marketplace → Verified; others → Community.
/// </summary>
public sealed class InstalledMarketplaceReader
{
    private readonly IFileSystem _fs;

    public InstalledMarketplaceReader(IFileSystem fs) => _fs = fs;

    public IReadOnlyList<CatalogItem> Read(string userDir)
    {
        var items = new List<CatalogItem>();
        var root = $"{userDir}/.claude/plugins/marketplaces";

        foreach (var dir in _fs.GetDirectories(root))
        {
            var manifestPath = $"{dir}/.claude-plugin/marketplace.json";
            if (!_fs.FileExists(manifestPath)) continue;

            var text = _fs.ReadAllText(manifestPath);
            var (name, ownerEmail) = MarketplaceManifestParser.ReadHeader(text);
            var trust = MarketplaceTrust.Classify(name, ownerEmail);
            var source = new CatalogSource(
                CatalogSourceKind.ClaudeMarketplace, trust, name ?? LastSegment(dir), manifestPath);

            items.AddRange(MarketplaceManifestParser.Parse(text, source));
        }
        return items;
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

Run: `dotnet test --filter InstalledMarketplaceReaderTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): installed-marketplace reader"
```

---

## Task 7: CatalogService façade + integration

**Files:**
- Create: `src/ClaudeExplorer.Core/Catalog/CatalogService.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Catalog/CatalogServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Catalog/CatalogServiceTests.cs`:
```csharp
using ClaudeExplorer.Core.Catalog;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Catalog;

public class CatalogServiceTests
{
    [Fact]
    public void Installed_catalog_merges_marketplaces_sorted_and_deduped_with_trust()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/plugins/marketplaces/claude-plugins-official/.claude-plugin/marketplace.json",
                """
                {
                  "name": "claude-plugins-official",
                  "owner": { "email": "support@anthropic.com" },
                  "plugins": [
                    { "name": "zeta", "description": "z" },
                    { "name": "alpha", "description": "a" },
                    { "name": "alpha", "description": "dup -> deduped" }
                  ]
                }
                """)
            .AddFile("/home/.claude/plugins/marketplaces/community/.claude-plugin/marketplace.json",
                """
                { "name": "community", "owner": { "email": "x@y.com" }, "plugins": [ { "name": "beta" } ] }
                """);

        var catalog = new CatalogService(fs, new FakeCatalogFetcher()).BuildInstalledCatalog("/home");

        // sorted by (source, name); dup alpha collapsed
        Assert.Equal(new[] { "alpha", "zeta", "beta" }, catalog.Select(i => i.Name).ToArray());
        Assert.Equal(TrustLevel.Verified, catalog.Single(i => i.Name == "zeta").Trust);
        Assert.Equal(TrustLevel.Community, catalog.Single(i => i.Name == "beta").Trust);
    }

    [Fact]
    public void Fetches_added_source_metadata_via_fetcher_with_community_trust()
    {
        var manifestUrl = SourceDetector.RawGitHubManifestUrl("octocat", "plugins");
        var fetcher = new FakeCatalogFetcher().Add(manifestUrl,
            """{ "name": "octo", "plugins": [ { "name": "tool", "description": "t" } ] }""");

        var items = new CatalogService(new InMemoryFileSystem(), fetcher).FetchAddedSource("octocat/plugins");

        var tool = Assert.Single(items);
        Assert.Equal("tool", tool.Name);
        Assert.Equal(TrustLevel.Community, tool.Trust);
        Assert.Equal(CatalogSourceKind.GitHub, tool.Source.Kind);
        Assert.Equal(new[] { manifestUrl }, fetcher.Requests); // metadata-only: a single manifest fetch
    }

    [Fact]
    public void Added_source_that_cannot_be_fetched_yields_empty()
    {
        var items = new CatalogService(new InMemoryFileSystem(), new FakeCatalogFetcher())
            .FetchAddedSource("octocat/plugins"); // fetcher has nothing registered -> null
        Assert.Empty(items);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter CatalogServiceTests`
Expected: FAIL — `CatalogService` doesn't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Catalog/CatalogService.cs`:
```csharp
using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Catalog;

/// <summary>
/// Top-level catalog façade. Reads installed Claude marketplaces locally, and fetches user-added
/// sources' metadata on demand. Metadata-only — persisting an added source and installing items go
/// through the safe-mutation layer (Phase 6); this never downloads or runs an item.
/// </summary>
public sealed class CatalogService
{
    private readonly InstalledMarketplaceReader _installed;
    private readonly ICatalogFetcher _fetcher;

    public CatalogService(IFileSystem fileSystem, ICatalogFetcher fetcher)
    {
        _installed = new InstalledMarketplaceReader(fileSystem);
        _fetcher = fetcher;
    }

    /// <summary>Items from marketplaces already configured on this machine (no network).</summary>
    public IReadOnlyList<CatalogItem> BuildInstalledCatalog(string userDir)
        => Dedupe(_installed.Read(userDir));

    /// <summary>
    /// Detect a user-added source, fetch its manifest metadata, and normalize it. Returns an empty
    /// list if the manifest can't be fetched. Nothing is downloaded or installed.
    /// </summary>
    public IReadOnlyList<CatalogItem> FetchAddedSource(string input)
    {
        var source = SourceDetector.Detect(input);
        var text = _fetcher.FetchText(source.Location);
        return Dedupe(MarketplaceManifestParser.Parse(text, source));
    }

    private static IReadOnlyList<CatalogItem> Dedupe(IReadOnlyList<CatalogItem> items)
        => items
            .GroupBy(i => $"{i.Source.Name} {i.Name}", StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(i => i.Source.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
```

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test`
Expected: PASS — all Phase 1–3 tests plus the new Phase-4 tests are green.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): CatalogService facade"
```

---

## Self-Review

**Spec coverage (roadmap Phase 4 deliverables):**
- Seam `ICatalogFetcher`/`IHttpClient` (+ impl + fake; no real network in tests) → Task 1. ✓
- `CatalogSource(kind: ClaudeMarketplace|Url|GitHub, trust: Verified|Community, name, location)` → Task 2. ✓
- Source detector (URL vs `owner/repo` vs github.com URL vs marketplace) → Task 4. ✓
- Manifest reader/normalizer → `CatalogItem(name, type, summary, source, trust, stats?)` (+ author/category/homepage/tags from real manifests) → Tasks 2, 5. ✓
- `CatalogService` → Task 7. ✓
- Trust: official Anthropic = Verified; user-added = Community (clearly flagged on source + item) → Tasks 3, 6, 7. ✓
- **Hard rule** metadata-only (nothing fetched/run until install) → enforced: only `ICatalogFetcher.FetchText` (a manifest GET) ever hits the network; `FetchAddedSource` does a single manifest fetch and returns items for preview; persistence/install deferred to Phase 6. ✓
- Tests: source-type detection (Task 4), manifest parse (Task 5), trust assignment (Tasks 3, 6), dedupe (Task 7), fake fetcher (Tasks 1, 7). ✓

**Deferred (noted, not forgotten):** persisting/removing user-added sources (Phase 6 safe-mutation); resolving a plugin's `source` and listing its contents (install-time, Phase 6); user-added **bare-skills** repos → `Skill`/`Agent` item types (needs repo inspection); `stats` population (stars/downloads); github URLs with sub-paths/refs; pagination/caching of fetches; `~/.claude.json`-registered marketplaces beyond the on-disk `marketplaces/` dir. These belong to later phases / the tech-debt issue (CLA-16).

**Placeholder scan:** none — every code step contains complete code; every run step has an exact command + expected result.

**Type consistency:** `ICatalogFetcher.FetchText`, `HttpCatalogFetcher`, `FakeCatalogFetcher` (`Add`/`Requests`), `CatalogSourceKind {ClaudeMarketplace,Url,GitHub}`, `TrustLevel {Verified,Community}`, `CatalogItemType {Plugin,Skill,Agent}`, `CatalogSource(Kind,Trust,Name,Location)`, `CatalogItemStats(Stars?,Downloads?)`, `CatalogItem(Name,Type,Summary,Author,Category,Homepage,Tags,Source,Trust,Stats?)`, `MarketplaceTrust.Classify`, `SourceDetector.Detect`/`RawGitHubManifestUrl`, `MarketplaceManifestParser.Parse`/`ReadHeader`, `InstalledMarketplaceReader.Read`, `CatalogService.BuildInstalledCatalog`/`FetchAddedSource` are used identically across all tasks and match the Phase-1/2 types they consume (`IFileSystem` with `GetDirectories`/`FileExists`/`ReadAllText`).

---

## Execution Handoff

Plan saved to `docs/superpowers/plans/2026-06-07-04-catalog-sources.md`. Execute via superpowers:subagent-driven-development (one implementer for the cohesive engine, then spec + code-quality review), then finishing-a-development-branch — per the playbook in `docs/superpowers/HANDOFF.md`.
