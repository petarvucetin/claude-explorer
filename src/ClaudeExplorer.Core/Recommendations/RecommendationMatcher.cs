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
