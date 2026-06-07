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
/// <remarks>
/// <see cref="Value"/> may be a mutable JsonNode (JsonArray for merged lists/hooks).
/// Treat as read-only; call <see cref="CloneValue"/> if you need a mutable copy.
/// </remarks>
public sealed record EffectiveSetting(
    string Key,
    MergeStrategy Strategy,
    JsonNode? Value,
    SettingOrigin? Winner,
    IReadOnlyList<SettingContribution> Contributions,
    bool HasConflict)
{
    /// <summary>
    /// Returns a deep clone of <see cref="Value"/>, or <c>null</c> if Value is null.
    /// Use this when you need a mutable copy.
    /// </summary>
    public JsonNode? CloneValue() => Value?.DeepClone();
}

public sealed record EffectiveConfig(IReadOnlyList<EffectiveSetting> Settings)
{
    public EffectiveSetting? Find(string key)
        => Settings.FirstOrDefault(s => s.Key == key);
}
