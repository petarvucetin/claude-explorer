using System.Text.Json.Nodes;

namespace ClaudeExplorer.Core.Model;

/// <summary>Where a contributed value came from.</summary>
public sealed record SettingOrigin(ScopeKind Scope, string FilePath, string JsonPath);

/// <summary>One scope's contribution to a setting (its raw value at that scope).</summary>
public sealed record SettingContribution(SettingOrigin Origin, JsonNode? Value);
