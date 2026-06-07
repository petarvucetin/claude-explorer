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
