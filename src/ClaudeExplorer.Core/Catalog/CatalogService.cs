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
            .GroupBy(i => (i.Source.Name, i.Name)) // value-tuple key — no separator-collision risk
            .Select(g => g.First())
            .OrderBy(i => i.Source.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
