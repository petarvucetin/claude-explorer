using System.Text.Json.Nodes;

namespace ClaudeExplorer.Core.Model;

public enum MergeStrategy
{
    ScalarLastWins,
    ListUnion,
    ArrayConcat,
}

/// <summary>
/// A single resolved setting keyed by dotted path (e.g. "model", "permissions.allow",
/// "env.FOO", "hooks.PreToolUse").
/// </summary>
public sealed record EffectiveSetting(
    string Key,
    MergeStrategy Strategy,
    JsonNode? Value,
    SettingOrigin? Winner,
    IReadOnlyList<SettingContribution> Contributions,
    bool HasConflict);

public sealed record EffectiveConfig(IReadOnlyList<EffectiveSetting> Settings)
{
    public EffectiveSetting? Find(string key)
        => Settings.FirstOrDefault(s => s.Key == key);
}
