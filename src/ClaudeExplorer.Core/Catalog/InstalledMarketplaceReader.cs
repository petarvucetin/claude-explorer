using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Catalog;

/// <summary>
/// Reads the marketplaces already configured on this machine, from
/// <c>{userDir}/.claude/plugins/marketplaces/*/.claude-plugin/marketplace.json</c>. Local only — no
/// network. Official Anthropic marketplace → Verified; others → Community.
/// </summary>
public sealed class InstalledMarketplaceReader
{
    private readonly IFileSystem _fs;

    public InstalledMarketplaceReader(IFileSystem fs) => _fs = fs;

    public IReadOnlyList<CatalogItem> Read(string userDir)
    {
        var items = new List<CatalogItem>();
        var root = $"{userDir}/.claude/plugins/marketplaces";

        foreach (var dir in _fs.GetDirectories(root))
        {
            var manifestPath = $"{dir}/.claude-plugin/marketplace.json";
            if (!_fs.FileExists(manifestPath)) continue;

            // TOCTOU-safe: the file can vanish/become unreadable between the check and the read
            // (e.g. a concurrent uninstall); skip it rather than throwing out of the reader.
            string text;
            try { text = _fs.ReadAllText(manifestPath); }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }
            var (name, ownerEmail) = MarketplaceManifestParser.ReadHeader(text);
            var trust = MarketplaceTrust.Classify(name, ownerEmail);
            var source = new CatalogSource(
                CatalogSourceKind.ClaudeMarketplace, trust, name ?? LastSegment(dir), manifestPath);

            items.AddRange(MarketplaceManifestParser.Parse(text, source));
        }
        return items;
    }

    private static string LastSegment(string path)
    {
        var trimmed = path.TrimEnd('/');
        var i = trimmed.LastIndexOf('/');
        return i >= 0 ? trimmed.Substring(i + 1) : trimmed;
    }
}
