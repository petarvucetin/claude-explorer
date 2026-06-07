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
