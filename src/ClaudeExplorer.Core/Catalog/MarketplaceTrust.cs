namespace ClaudeExplorer.Core.Catalog;

/// <summary>
/// Trust policy for marketplaces. The official Anthropic directory is Verified; everything the user
/// added is Community. Detected by the known official marketplace name or an @anthropic.com owner
/// email (executable-style case-insensitive match — emails/domains are not case-sensitive).
/// </summary>
public static class MarketplaceTrust
{
    private static readonly HashSet<string> OfficialNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "claude-plugins-official",
    };

    public static TrustLevel Classify(string? marketplaceName, string? ownerEmail)
    {
        if (marketplaceName is not null && OfficialNames.Contains(marketplaceName))
            return TrustLevel.Verified;
        if (ownerEmail is not null
            && ownerEmail.Trim().EndsWith("@anthropic.com", StringComparison.OrdinalIgnoreCase))
            return TrustLevel.Verified;
        return TrustLevel.Community;
    }
}
