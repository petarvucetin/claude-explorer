using System.Text.Json.Nodes;
using ClaudeExplorer.App.Environments;
using ClaudeExplorer.App.Util;
using ClaudeExplorer.Core.Model;
using CoreEffectiveConfig = ClaudeExplorer.Core.Model.EffectiveConfig;

namespace ClaudeExplorer.App.Screens.EnvironmentSettings;

/// <summary>Identifies the active Claude environment for display.</summary>
public sealed record EnvironmentIdentity(
    string Name,
    EnvironmentKind Kind,
    string UserDir,
    string ProjectLabel);

/// <summary>A single scalar value with its winning scope CSS class for scope-tag rendering.</summary>
/// <param name="Display">Human-readable value (raw string, no outer quotes).</param>
/// <param name="ScopeCss">One of: "user","project","local","enterprise","plugin","" (empty when no winner).</param>
public sealed record ScopedValue(string Display, string ScopeCss);

public sealed record EnvVarRow(string Name, string Value, string ScopeCss);
public sealed record KvRow(string Key, string Value, string ScopeCss);
public sealed record HookEventRow(string Event, int MatcherCount);

/// <summary>View-ready model for the Claude Environment Settings screen.</summary>
public sealed record EnvironmentSettingsView(
    EnvironmentIdentity Identity,
    ScopedValue? Model,
    ScopedValue? OutputStyle,
    ScopedValue? DefaultMode,
    IReadOnlyList<string> Allow,
    IReadOnlyList<string> Deny,
    IReadOnlyList<string> Ask,
    IReadOnlyList<EnvVarRow> EnvVars,
    IReadOnlyList<KvRow> StatusLine,
    IReadOnlyList<HookEventRow> Hooks);

/// <summary>Pure mapper: builds an <see cref="EnvironmentSettingsView"/> from the merged
/// effective config and a pre-built identity.</summary>
public static class EnvironmentSettingsMapper
{
    private const string EnvPrefix = "env.";
    private const string StatusLinePrefix = "statusLine.";
    private const string HooksPrefix = "hooks.";

    public static EnvironmentSettingsView Map(CoreEffectiveConfig cfg, EnvironmentIdentity id)
    {
        var model = ToScopedValue(cfg.Find("model"));
        var outputStyle = ToScopedValue(cfg.Find("outputStyle"));
        var defaultMode = ToScopedValue(cfg.Find("permissions.defaultMode"));

        var allow = ToStringList(cfg.Find("permissions.allow"));
        var deny = ToStringList(cfg.Find("permissions.deny"));
        var ask = ToStringList(cfg.Find("permissions.ask"));

        var envVars = cfg.Settings
            .Where(s => s.Key.StartsWith(EnvPrefix, StringComparison.Ordinal))
            .Select(s => new EnvVarRow(
                s.Key.Substring(EnvPrefix.Length),
                ScalarDisplay(s.Value),
                ScopeCss(s.Winner?.Scope)))
            .OrderBy(r => r.Name, StringComparer.Ordinal)
            .ToList();

        var statusLine = cfg.Settings
            .Where(s => s.Key.StartsWith(StatusLinePrefix, StringComparison.Ordinal))
            .Select(s => new KvRow(
                s.Key.Substring(StatusLinePrefix.Length),
                ScalarDisplay(s.Value),
                ScopeCss(s.Winner?.Scope)))
            .OrderBy(r => r.Key, StringComparer.Ordinal)
            .ToList();

        var hooks = cfg.Settings
            .Where(s => s.Key.StartsWith(HooksPrefix, StringComparison.Ordinal))
            .Select(s => new HookEventRow(
                s.Key.Substring(HooksPrefix.Length),
                s.Value is JsonArray arr ? arr.Count : 0))
            .OrderBy(r => r.Event, StringComparer.Ordinal)
            .ToList();

        return new EnvironmentSettingsView(
            id,
            model,
            outputStyle,
            defaultMode,
            allow,
            deny,
            ask,
            envVars,
            statusLine,
            hooks);
    }

    private static ScopedValue? ToScopedValue(EffectiveSetting? setting)
    {
        if (setting is null) return null;
        return new ScopedValue(ScalarDisplay(setting.Value), ScopeCss(setting.Winner?.Scope));
    }

    private static IReadOnlyList<string> ToStringList(EffectiveSetting? setting)
    {
        if (setting?.Value is not JsonArray arr) return Array.Empty<string>();
        var list = new List<string>();
        foreach (var item in arr)
        {
            if (item is JsonValue v)
                list.Add(v.GetValue<string>());
        }
        return list;
    }

    /// <summary>Scalar display: for a JsonValue render the raw string without surrounding quotes;
    /// for other node types use Pretty. Empty string for null.</summary>
    internal static string ScalarDisplay(JsonNode? node)
    {
        if (node is null) return "";
        if (node is JsonValue jv && jv.TryGetValue<string>(out var s)) return s;
        return JsonFormat.Pretty(node);
    }

    /// <summary>Maps a nullable <see cref="ScopeKind"/> to the CSS modifier used in scope-tag classes.</summary>
    internal static string ScopeCss(ScopeKind? scope) => scope switch
    {
        ScopeKind.User => "user",
        ScopeKind.Project => "project",
        ScopeKind.Local => "local",
        ScopeKind.Enterprise => "enterprise",
        ScopeKind.Plugin => "plugin",
        _ => "",
    };
}
