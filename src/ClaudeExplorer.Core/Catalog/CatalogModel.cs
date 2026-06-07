namespace ClaudeExplorer.Core.Catalog;

/// <summary>How a catalog source is reached.</summary>
public enum CatalogSourceKind { ClaudeMarketplace, Url, GitHub }

/// <summary>Trust level surfaced everywhere a source/item appears.</summary>
public enum TrustLevel { Verified, Community }

/// <summary>The kind of installable item.</summary>
public enum CatalogItemType { Plugin, Skill, Agent }

/// <summary>A source of installable items.</summary>
/// <param name="Location">For <see cref="CatalogSourceKind.ClaudeMarketplace"/>: the on-disk manifest
/// path. For Url/GitHub: the manifest URL to fetch.</param>
public sealed record CatalogSource(CatalogSourceKind Kind, TrustLevel Trust, string Name, string Location);

/// <summary>Reserved usage stats. Not populated from marketplace manifests in v1 (shape for the UI later).</summary>
public sealed record CatalogItemStats(long? Stars = null, long? Downloads = null);

/// <summary>A normalized installable item (metadata only). Inherits its source's trust.</summary>
public sealed record CatalogItem(
    string Name,
    CatalogItemType Type,
    string? Summary,
    string? Author,
    string? Category,
    string? Homepage,
    IReadOnlyList<string> Tags,
    CatalogSource Source,
    TrustLevel Trust,
    CatalogItemStats? Stats = null);
