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
