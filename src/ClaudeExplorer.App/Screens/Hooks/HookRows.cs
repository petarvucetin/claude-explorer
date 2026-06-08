using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Model;
using CoreEffectiveConfig = ClaudeExplorer.Core.Model.EffectiveConfig;

namespace ClaudeExplorer.App.Screens.Hooks;

/// <summary>Health of a hook command's runtime.</summary>
public enum HookHealth { Ok, Missing, Unverifiable, Na }

public sealed record HookRow(
    string Event,
    string Matcher,
    string Command,
    string? Type,
    ScopeKind Source,
    string SourceFile,
    string? Runtime,
    HookHealth Health);

public sealed record HookGroup(string Event, IReadOnlyList<HookRow> Rows);

public sealed record HookView(IReadOnlyList<HookGroup> Groups, int Total, int Missing);

/// <summary>
/// Pure mapper: flattens the merged <c>hooks.*</c> effective settings into per-source rows (event ×
/// matcher × command) with provenance and a runtime-health pill. Iterates each setting's
/// contributions, so every row keeps the scope/plugin that defined it. Commands whose runtime is a
/// templated path (e.g. <c>${CLAUDE_PLUGIN_ROOT}/run-hook.cmd</c>) can't be resolved on PATH, so they
/// are reported n/a rather than "missing".
/// </summary>
public static class HookRowsMapper
{
    private const string Prefix = "hooks.";

    public static HookView Map(CoreEffectiveConfig config, DependencyReport health)
    {
        var rows = new List<HookRow>();

        foreach (var setting in config.Settings)
        {
            if (!setting.Key.StartsWith(Prefix, StringComparison.Ordinal)) continue;
            var evt = setting.Key.Substring(Prefix.Length);

            foreach (var contribution in setting.Contributions)
            {
                if (contribution.Value is not JsonArray matcherGroups) continue;

                foreach (var groupNode in matcherGroups)
                {
                    if (groupNode is not JsonObject mg) continue;
                    var matcher = (string?)mg["matcher"];
                    if (string.IsNullOrEmpty(matcher)) matcher = "*";
                    if (mg["hooks"] is not JsonArray hookNodes) continue;

                    foreach (var hookNode in hookNodes)
                    {
                        if (hookNode is not JsonObject h) continue;
                        var command = (string?)h["command"];
                        if (string.IsNullOrEmpty(command)) continue;

                        var runtime = ExecutableExtractor.Extract(command);
                        rows.Add(new HookRow(
                            evt, matcher!, command, (string?)h["type"],
                            contribution.Origin.Scope, contribution.Origin.FilePath,
                            runtime, HealthOf(command, runtime, health)));
                    }
                }
            }
        }

        var groups = rows
            .GroupBy(r => r.Event, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new HookGroup(g.Key, g.ToList()))
            .ToList();

        return new HookView(groups, rows.Count, rows.Count(r => r.Health == HookHealth.Missing));
    }

    private static HookHealth HealthOf(string command, string? runtime, DependencyReport health)
    {
        if (runtime is null) return HookHealth.Na;
        var templated = command.Contains('$') || command.Contains('%');
        var result = health.Results.FirstOrDefault(
            r => string.Equals(r.Ref.Name, runtime, StringComparison.OrdinalIgnoreCase));
        return result?.Status.Kind switch
        {
            DependencyStatusKind.Found => HookHealth.Ok,
            DependencyStatusKind.Missing => templated ? HookHealth.Na : HookHealth.Missing,
            DependencyStatusKind.Unverifiable => HookHealth.Unverifiable,
            _ => templated ? HookHealth.Na : HookHealth.Unverifiable,
        };
    }

    public static string Pill(HookHealth h) => h switch
    {
        HookHealth.Ok => "ok",
        HookHealth.Missing => "bad",
        HookHealth.Na => "na",
        _ => "warn",
    };

    public static string HealthText(HookRow r) => r.Health switch
    {
        HookHealth.Ok => $"{r.Runtime} ✓",
        HookHealth.Missing => $"{r.Runtime} ✗ missing",
        HookHealth.Na => "script · n/a",
        _ => $"{r.Runtime} · present",
    };
}

/// <summary>One matcher token rendered as a chip. <see cref="IsAny"/> marks the wildcard.</summary>
public sealed record MatcherChip(string Text, bool IsAny);

/// <summary>Splits a hook matcher (a tool-name regex) into display chips. A <c>*</c> or empty matcher
/// is a single "any tool" chip; otherwise the pipe-delimited tokens each become a chip (regex tokens
/// pass through unchanged).</summary>
public static class HookMatcher
{
    public static IReadOnlyList<MatcherChip> Chips(string? matcher)
    {
        if (string.IsNullOrWhiteSpace(matcher) || matcher == "*")
            return new[] { new MatcherChip("∗ any tool", true) };

        return matcher
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => new MatcherChip(t, false))
            .ToList();
    }
}
