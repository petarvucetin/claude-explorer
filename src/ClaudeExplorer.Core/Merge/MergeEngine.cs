using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Merge;

public sealed class MergeEngine
{
    public EffectiveConfig Compute(IReadOnlyList<ScopeSettings> scopes)
    {
        var ordered = scopes.OrderBy(s => (int)s.Scope).ToList();
        var results = new List<EffectiveSetting>();

        foreach (var spec in SettingSpecs.Scalars)
        {
            var s = ResolveScalar(spec.Key, spec.Path, ordered);
            if (s is not null) results.Add(s);
        }

        foreach (var spec in SettingSpecs.Lists)
        {
            var s = ResolveListUnion(spec.Key, spec.Path, ordered);
            if (s is not null) results.Add(s);
        }

        results.AddRange(ResolveEnv(ordered));
        results.AddRange(ResolveHooks(ordered));

        return new EffectiveConfig(results);
    }

    private static IEnumerable<EffectiveSetting> ResolveEnv(List<ScopeSettings> ordered)
    {
        var keys = new List<string>();
        foreach (var s in ordered)
            if (Navigate(s.Root, new[] { "env" }) is JsonObject env)
                foreach (var kv in env)
                    if (!keys.Contains(kv.Key)) keys.Add(kv.Key);

        foreach (var key in keys)
        {
            var contributions = new List<SettingContribution>();
            foreach (var s in ordered)
                if (Navigate(s.Root, new[] { "env" }) is JsonObject env
                    && env.TryGetPropertyValue(key, out var v) && v is not null)
                    contributions.Add(new SettingContribution(
                        new SettingOrigin(s.Scope, s.FilePath, $"env.{key}"),
                        v.DeepClone()));

            if (contributions.Count == 0) continue;

            var winner = contributions[^1];
            var distinct = contributions.Select(c => c.Value?.ToJsonString() ?? "null").Distinct().Count();

            yield return new EffectiveSetting(
                Key: $"env.{key}",
                Strategy: MergeStrategy.ScalarLastWins,
                Value: winner.Value?.DeepClone(),
                Winner: winner.Origin,
                Contributions: contributions,
                HasConflict: distinct > 1);
        }
    }

    private static IEnumerable<EffectiveSetting> ResolveHooks(List<ScopeSettings> ordered)
    {
        var events = new List<string>();
        foreach (var s in ordered)
            if (Navigate(s.Root, new[] { "hooks" }) is JsonObject h)
                foreach (var kv in h)
                    if (!events.Contains(kv.Key)) events.Add(kv.Key);

        foreach (var ev in events)
        {
            var contributions = new List<SettingContribution>();
            var combined = new JsonArray();
            foreach (var s in ordered)
                if (Navigate(s.Root, new[] { "hooks" }) is JsonObject h
                    && h.TryGetPropertyValue(ev, out var v) && v is JsonArray arr)
                {
                    contributions.Add(new SettingContribution(
                        new SettingOrigin(s.Scope, s.FilePath, $"hooks.{ev}"),
                        arr.DeepClone()));
                    foreach (var item in arr)
                        combined.Add(item?.DeepClone());
                }

            if (contributions.Count == 0) continue;

            yield return new EffectiveSetting(
                Key: $"hooks.{ev}",
                Strategy: MergeStrategy.ArrayConcat,
                Value: combined,
                Winner: null,
                Contributions: contributions,
                HasConflict: false);
        }
    }

    private static EffectiveSetting? ResolveListUnion(string key, string[] path, List<ScopeSettings> ordered)
    {
        var contributions = new List<SettingContribution>();
        var merged = new JsonArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var s in ordered)
        {
            if (Navigate(s.Root, path) is not JsonArray arr) continue;

            contributions.Add(new SettingContribution(
                new SettingOrigin(s.Scope, s.FilePath, string.Join('.', path)),
                arr.DeepClone()));

            foreach (var item in arr)
            {
                var itemKey = item?.ToJsonString() ?? "null";
                if (seen.Add(itemKey))
                    merged.Add(item?.DeepClone());
            }
        }

        if (contributions.Count == 0) return null;

        return new EffectiveSetting(
            Key: key,
            Strategy: MergeStrategy.ListUnion,
            Value: merged,
            Winner: null,
            Contributions: contributions,
            HasConflict: false);
    }

    private static JsonNode? Navigate(JsonObject root, string[] path)
    {
        JsonNode? cur = root;
        foreach (var seg in path)
        {
            if (cur is JsonObject o && o.TryGetPropertyValue(seg, out var next))
                cur = next;
            else
                return null;
        }
        return cur;
    }

    private static EffectiveSetting? ResolveScalar(string key, string[] path, List<ScopeSettings> ordered)
    {
        var contributions = new List<SettingContribution>();
        foreach (var s in ordered)
        {
            var v = Navigate(s.Root, path);
            if (v is not null)
                contributions.Add(new SettingContribution(
                    new SettingOrigin(s.Scope, s.FilePath, string.Join('.', path)),
                    v.DeepClone()));
        }

        if (contributions.Count == 0) return null;

        var winner = contributions[^1];                 // ordered ascending → last is highest precedence
        var distinct = contributions
            .Select(c => c.Value?.ToJsonString() ?? "null")
            .Distinct()
            .Count();

        return new EffectiveSetting(
            Key: key,
            Strategy: MergeStrategy.ScalarLastWins,
            Value: winner.Value?.DeepClone(),
            Winner: winner.Origin,
            Contributions: contributions,
            HasConflict: distinct > 1);
    }
}
