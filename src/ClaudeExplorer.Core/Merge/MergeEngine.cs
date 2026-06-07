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

        return new EffectiveConfig(results);
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
