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
    /// <remarks>Runtime annotations are produced only when BOTH <paramref name="itemRuntimes"/> and
    /// <paramref name="runtimeAvailability"/> are supplied; without availability data a runtime's
    /// status is unknown, so no (potentially false "missing") annotation is emitted.</remarks>
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

        // Annotate only when BOTH the requirement resolver and availability data are present —
        // without availability we cannot honestly claim a runtime is missing, so we skip annotation.
        return itemRuntimes is null || runtimeAvailability is null
            ? result
            : Annotate(result, runtimeAvailability, itemRuntimes);
    }

    private static RecommendationResult Annotate(
        RecommendationResult result,
        IReadOnlyDictionary<string, bool> availability,
        Func<CatalogItem, IReadOnlyList<string>> itemRuntimes)
    {
        var annotated = result.Recommendations.Select(r =>
        {
            var needs = itemRuntimes(r.Item);
            if (needs.Count == 0) return r;
            var notes = needs
                .Select(rt => new RuntimeAnnotation(rt, availability.TryGetValue(rt, out var ok) && ok))
                .ToList();
            return r with { Runtimes = notes };
        }).ToList();

        return new RecommendationResult(annotated);
    }
}
