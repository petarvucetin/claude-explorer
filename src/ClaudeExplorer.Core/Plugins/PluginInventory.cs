using ClaudeExplorer.Core.Catalog;

namespace ClaudeExplorer.Core.Plugins;

/// <summary>What a plugin contributes, by artifact kind.</summary>
public sealed record ProvidesCounts(int Commands, int Skills, int Subagents, int Hooks, int Mcp)
{
    public bool IsEmpty => Commands == 0 && Skills == 0 && Subagents == 0 && Hooks == 0 && Mcp == 0;
}

/// <summary>An installed plugin with its source, version, enabled state, trust, and what it provides.</summary>
public sealed record InstalledPluginInfo(
    string Name,
    string Marketplace,
    string Version,
    string Scope,
    string InstallPath,
    bool Enabled,
    ProvidesCounts Provides,
    TrustLevel Trust);

/// <summary>A configured marketplace and how many of its plugins are installed.</summary>
public sealed record MarketplaceInfo(string Name, string? SourceRepo, TrustLevel Trust, int InstalledCount);

public sealed record PluginInventory(
    IReadOnlyList<InstalledPluginInfo> Plugins,
    IReadOnlyList<MarketplaceInfo> Marketplaces);
