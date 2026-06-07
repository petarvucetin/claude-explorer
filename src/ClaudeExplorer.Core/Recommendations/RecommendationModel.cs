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
