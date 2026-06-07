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
