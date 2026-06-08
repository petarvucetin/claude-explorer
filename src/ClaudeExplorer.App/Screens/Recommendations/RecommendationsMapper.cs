using ClaudeExplorer.Core.Catalog;
using ClaudeExplorer.Core.Recommendations;

namespace ClaudeExplorer.App.Screens.Recommendations;

/// <summary>View-ready row for a single recommendation.</summary>
public sealed record RecommendationRow(
    string Name,
    CatalogItemType Type,
    TrustLevel Trust,
    double Confidence,
    RecommendationBucket Bucket,
    IReadOnlyList<ReasonRow> Reasons,
    IReadOnlyList<RuntimeRow> Runtimes);

/// <summary>A single reason with flattened evidence chips.</summary>
public sealed record ReasonRow(string Text, IReadOnlyList<EvidenceChip> Evidence);

/// <summary>A linkable evidence chip (file path + optional count).</summary>
public sealed record EvidenceChip(string FilePath, int? Count, string? Detail);

/// <summary>Runtime annotation for display.</summary>
public sealed record RuntimeRow(string Runtime, bool Available);

/// <summary>Signal chip for the top of the screen.</summary>
public sealed record SignalChip(string Kind, string Value);

/// <summary>Top-level view produced by <see cref="RecommendationsMapper.Map"/>.</summary>
public sealed record RecommendationsView(
    IReadOnlyList<SignalChip> Signals,
    IReadOnlyList<RecommendationRow> Strong,
    IReadOnlyList<RecommendationRow> Consider,
    IReadOnlyList<RecommendationRow> AlreadyCovered);

/// <summary>Pure mapper from <see cref="RecommendationResult"/> to view rows. Tested without IO.</summary>
public static class RecommendationsMapper
{
    public static RecommendationsView Map(RecommendationResult result)
    {
        // Collect unique signals from all reasons across all recommendations
        var signals = result.Recommendations
            .SelectMany(r => r.Reasons)
            .Select(r => r.Signal)
            .GroupBy(s => (s.Kind, s.Value))
            .Select(g => new SignalChip(g.Key.Kind.ToString(), g.Key.Value))
            .ToList();

        return new RecommendationsView(
            Signals: signals,
            Strong: MapBucket(result.Strong),
            Consider: MapBucket(result.Consider),
            AlreadyCovered: MapBucket(result.AlreadyCovered));
    }

    private static IReadOnlyList<RecommendationRow> MapBucket(IEnumerable<Recommendation> bucket)
        => bucket.Select(MapRow).ToList();

    private static RecommendationRow MapRow(Recommendation r) => new(
        r.Item.Name,
        r.Item.Type,
        r.Item.Trust,
        r.Confidence,
        r.Bucket,
        r.Reasons.Select(reason => new ReasonRow(
            reason.Text,
            reason.Signal.Evidence.Select(e => new EvidenceChip(e.FilePath, e.Count, e.Detail)).ToList()
        )).ToList(),
        r.Runtimes.Select(rt => new RuntimeRow(rt.Runtime, rt.Available)).ToList());
}
