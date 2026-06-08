using ClaudeExplorer.App.Screens.Recommendations;
using ClaudeExplorer.Core.Catalog;
using ClaudeExplorer.Core.Recommendations;

namespace ClaudeExplorer.App.Tests.Screens;

public class RecommendationsViewModelTests
{
    // ── helpers ────────────────────────────────────────────────────────────────

    private static CatalogSource MakeSource(TrustLevel trust = TrustLevel.Verified)
        => new(CatalogSourceKind.ClaudeMarketplace, trust, "Test", "loc");

    private static CatalogItem MakeItem(string name, CatalogItemType type = CatalogItemType.Plugin,
        TrustLevel trust = TrustLevel.Verified)
        => new(name, type, $"Summary of {name}", "Author", null, null, Array.Empty<string>(),
            MakeSource(trust), trust);

    private static Signal MakeSignal(SignalKind kind, string value, params Evidence[] evidence)
        => new(kind, value, evidence);

    private static Evidence MakeEvidence(string file, int? count = null, string? detail = null)
        => new(file, count, detail);

    private static Recommendation MakeRecommendation(
        CatalogItem item,
        RecommendationBucket bucket,
        double confidence = 0.9,
        Signal[]? signals = null,
        RuntimeAnnotation[]? runtimes = null)
    {
        var reasons = (signals ?? Array.Empty<Signal>())
            .Select(s => new RecommendationReason(s, $"Detected {s.Value}"))
            .ToList();
        return new Recommendation(item, reasons, confidence, bucket,
            runtimes ?? Array.Empty<RuntimeAnnotation>());
    }

    // ── RecommendationsMapper unit tests ──────────────────────────────────────

    [Fact]
    public void Map_partitions_into_three_buckets()
    {
        var strongItem = MakeItem("playwright");
        var considerItem = MakeItem("docker-tools");
        var coveredItem = MakeItem("git-extras");

        var result = new RecommendationResult(new[]
        {
            MakeRecommendation(strongItem, RecommendationBucket.Strong, 0.9),
            MakeRecommendation(considerItem, RecommendationBucket.Consider, 0.5),
            MakeRecommendation(coveredItem, RecommendationBucket.AlreadyCovered, 0.7),
        });

        var view = RecommendationsMapper.Map(result);

        Assert.Single(view.Strong);
        Assert.Single(view.Consider);
        Assert.Single(view.AlreadyCovered);
        Assert.Equal("playwright", view.Strong[0].Name);
        Assert.Equal("docker-tools", view.Consider[0].Name);
        Assert.Equal("git-extras", view.AlreadyCovered[0].Name);
    }

    [Fact]
    public void Map_confidence_passes_through()
    {
        var item = MakeItem("my-tool");
        var result = new RecommendationResult(new[]
        {
            MakeRecommendation(item, RecommendationBucket.Strong, 0.75),
        });

        var view = RecommendationsMapper.Map(result);

        Assert.Equal(0.75, view.Strong[0].Confidence);
    }

    [Fact]
    public void Map_reasons_and_evidence_surfaced()
    {
        var evidence1 = MakeEvidence("src/playwright.config.ts", 3, "config file");
        var evidence2 = MakeEvidence("tests/e2e/login.spec.ts", 9);
        var signal = MakeSignal(SignalKind.Framework, "Playwright", evidence1, evidence2);
        var item = MakeItem("playwright-mcp");
        var result = new RecommendationResult(new[]
        {
            MakeRecommendation(item, RecommendationBucket.Strong, 0.9, signals: new[] { signal }),
        });

        var view = RecommendationsMapper.Map(result);

        var row = view.Strong[0];
        Assert.Single(row.Reasons);
        Assert.Equal("Detected Playwright", row.Reasons[0].Text);
        Assert.Equal(2, row.Reasons[0].Evidence.Count);
        Assert.Equal("src/playwright.config.ts", row.Reasons[0].Evidence[0].FilePath);
        Assert.Equal(3, row.Reasons[0].Evidence[0].Count);
        Assert.Equal("tests/e2e/login.spec.ts", row.Reasons[0].Evidence[1].FilePath);
    }

    [Fact]
    public void Map_signals_deduplicated_from_all_recommendations()
    {
        var sig1 = MakeSignal(SignalKind.Language, "TypeScript");
        var sig2 = MakeSignal(SignalKind.Framework, "React");
        var sig1Dup = MakeSignal(SignalKind.Language, "TypeScript"); // duplicate
        var result = new RecommendationResult(new[]
        {
            MakeRecommendation(MakeItem("a"), RecommendationBucket.Strong, signals: new[] { sig1, sig2 }),
            MakeRecommendation(MakeItem("b"), RecommendationBucket.Consider, signals: new[] { sig1Dup }),
        });

        var view = RecommendationsMapper.Map(result);

        // Should be 2 unique signals, not 3
        Assert.Equal(2, view.Signals.Count);
        Assert.Contains(view.Signals, s => s.Kind == "Language" && s.Value == "TypeScript");
        Assert.Contains(view.Signals, s => s.Kind == "Framework" && s.Value == "React");
    }

    [Fact]
    public void Map_runtime_annotations_surfaced()
    {
        var runtimes = new[] { new RuntimeAnnotation("uvx", true), new RuntimeAnnotation("node", false) };
        var item = MakeItem("some-agent");
        var result = new RecommendationResult(new[]
        {
            MakeRecommendation(item, RecommendationBucket.Strong, runtimes: runtimes),
        });

        var view = RecommendationsMapper.Map(result);

        var row = view.Strong[0];
        Assert.Equal(2, row.Runtimes.Count);
        Assert.Equal("uvx", row.Runtimes[0].Runtime);
        Assert.True(row.Runtimes[0].Available);
        Assert.Equal("node", row.Runtimes[1].Runtime);
        Assert.False(row.Runtimes[1].Available);
    }

    [Fact]
    public void Map_empty_result_gives_empty_buckets_and_signals()
    {
        var result = new RecommendationResult(Array.Empty<Recommendation>());

        var view = RecommendationsMapper.Map(result);

        Assert.Empty(view.Strong);
        Assert.Empty(view.Consider);
        Assert.Empty(view.AlreadyCovered);
        Assert.Empty(view.Signals);
    }

    [Fact]
    public void Map_type_and_trust_pass_through()
    {
        var item = MakeItem("skills-agent", CatalogItemType.Agent, TrustLevel.Community);
        var result = new RecommendationResult(new[]
        {
            MakeRecommendation(item, RecommendationBucket.Consider),
        });

        var view = RecommendationsMapper.Map(result);

        var row = view.Consider[0];
        Assert.Equal(CatalogItemType.Agent, row.Type);
        Assert.Equal(TrustLevel.Community, row.Trust);
    }
}
