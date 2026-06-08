namespace ClaudeExplorer.App.Dashboard;

public enum BadgeTone { None, Ok, Warn, Bad }
public enum AttentionTone { Bad, Warn, Info }

/// <summary>One numbered stat card on the dashboard (Commands, MCP Servers, …).</summary>
public sealed record StatCard(string Label, string Index, int Value, string? Badge, BadgeTone Tone, string Sub);

/// <summary>A "needs attention" row: a missing dep, a conflict, or an inconclusive probe.</summary>
public sealed record AttentionItem(AttentionTone Tone, string Title, string Detail);

/// <summary>A recent reversible change for the "recent changes" panel.</summary>
public sealed record RecentChange(string Id, string Title, string Meta, bool IsUndone);

public sealed record DashboardData(
    int Health,
    string HealthCaption,
    string EffectiveForLabel,
    string MergeOrder,
    IReadOnlyList<StatCard> Stats,
    IReadOnlyList<AttentionItem> Attention,
    IReadOnlyList<RecentChange> RecentChanges);
