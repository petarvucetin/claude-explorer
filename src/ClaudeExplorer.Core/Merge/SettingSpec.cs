using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Merge;

/// <summary>A statically-known setting: its dotted key, merge strategy, and JSON path.</summary>
public sealed record SettingSpec(string Key, MergeStrategy Strategy, string[] Path);

public static class SettingSpecs
{
    public static readonly IReadOnlyList<SettingSpec> Scalars = new[]
    {
        new SettingSpec("model", MergeStrategy.ScalarLastWins, new[] { "model" }),
        new SettingSpec("outputStyle", MergeStrategy.ScalarLastWins, new[] { "outputStyle" }),
        new SettingSpec("statusLine", MergeStrategy.ScalarLastWins, new[] { "statusLine" }),
        new SettingSpec("permissions.defaultMode", MergeStrategy.ScalarLastWins, new[] { "permissions", "defaultMode" }),
    };

    public static readonly IReadOnlyList<SettingSpec> Lists = new[]
    {
        new SettingSpec("permissions.allow", MergeStrategy.ListUnion, new[] { "permissions", "allow" }),
        new SettingSpec("permissions.deny", MergeStrategy.ListUnion, new[] { "permissions", "deny" }),
        new SettingSpec("permissions.ask", MergeStrategy.ListUnion, new[] { "permissions", "ask" }),
    };
}
