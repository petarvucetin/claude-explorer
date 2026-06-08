using ClaudeExplorer.Core.Model;
using CoreEffectiveConfig = ClaudeExplorer.Core.Model.EffectiveConfig;

namespace ClaudeExplorer.App.Screens.EffectiveConfig;

public sealed record ScopeCell(bool Present, string? Display, bool IsWinner, bool IsOverridden);

public sealed record SettingRow(
    string Key,
    MergeStrategy Strategy,
    string MergeLabel,
    bool HasConflict,
    string EffectiveDisplay,
    IReadOnlyDictionary<ScopeKind, ScopeCell> Cells,
    IReadOnlyList<SettingContribution> Trace,
    SettingOrigin? Winner);

public sealed record EffectiveConfigView(IReadOnlyList<SettingRow> Rows, int ConflictCount);

public static class EffectiveConfigMapper
{
    private static readonly ScopeKind[] AllScopes =
    {
        ScopeKind.Enterprise, ScopeKind.User, ScopeKind.Project, ScopeKind.Local
    };

    public static EffectiveConfigView Map(CoreEffectiveConfig config)
    {
        var rows = config.Settings.Select(s =>
        {
            var byScope = s.Contributions
                .GroupBy(c => c.Origin.Scope)
                .ToDictionary(g => g.Key, g => g.Last());

            var cells = new Dictionary<ScopeKind, ScopeCell>();
            foreach (var scope in AllScopes)
            {
                if (byScope.TryGetValue(scope, out var contrib))
                {
                    var isWinner = s.Winner is not null && s.Winner.Scope == scope;
                    cells[scope] = new ScopeCell(
                        true,
                        Display(contrib.Value),
                        isWinner,
                        !isWinner && s.Strategy == MergeStrategy.ScalarLastWins);
                }
                else
                {
                    cells[scope] = new ScopeCell(false, null, false, false);
                }
            }

            return new SettingRow(
                s.Key,
                s.Strategy,
                MergeLabel(s.Strategy),
                s.HasConflict,
                Display(s.Value),
                cells,
                s.Contributions,
                s.Winner);
        }).ToList();

        return new EffectiveConfigView(rows, rows.Count(r => r.HasConflict));
    }

    public static string MergeLabel(MergeStrategy s) => s switch
    {
        MergeStrategy.ListUnion => "merged · union",
        MergeStrategy.ArrayConcat => "merged · concat",
        _ => "scalar · last-wins",
    };

    public static string Display(System.Text.Json.Nodes.JsonNode? node)
        => node is null ? "" : node.ToJsonString();
}
