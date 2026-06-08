using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Mutation;

namespace ClaudeExplorer.App.Dashboard;

/// <summary>Pure derivation of <see cref="DashboardData"/> from raw engine outputs. No IO, so it
/// is fully unit-tested by constructing Core records directly.</summary>
public static class DashboardComputer
{
    public static DashboardData Compute(DashboardInputs input)
    {
        var commands = input.Artifacts.OfKind(ArtifactKind.Command).ToList();
        var cmdUser = commands.Count(a => a.Winner.Source.Kind == ArtifactSourceKind.User);
        var cmdProject = commands.Count(a => a.Winner.Source.Kind == ArtifactSourceKind.Project);
        var cmdPlugin = commands.Count(a => a.Winner.Source.Kind == ArtifactSourceKind.Plugin);

        var skillsAgents = input.Artifacts.OfKind(ArtifactKind.Skill).Count()
                         + input.Artifacts.OfKind(ArtifactKind.Subagent).Count();
        var pluginNames = input.Artifacts.Artifacts
            .Where(a => a.Winner.Source.Kind == ArtifactSourceKind.Plugin && a.Winner.Source.PluginName is not null)
            .Select(a => a.Winner.Source.PluginName!).Distinct().Count();

        var missingServerNames = input.Dependencies.Results
            .Where(r => r.Status.Kind == DependencyStatusKind.Missing)
            .SelectMany(r => r.Ref.ReferencedBy)
            .Where(b => b.StartsWith("mcp:", StringComparison.Ordinal))
            .Select(b => b["mcp:".Length..])
            .ToHashSet(StringComparer.Ordinal);
        var mcpTotal = input.McpServers.Count;
        var mcpDown = input.McpServers.Count(s => missingServerNames.Contains(s.Name));

        var depTotal = input.Dependencies.Results.Count;
        var depMissing = input.Dependencies.Count(DependencyStatusKind.Missing);
        var depUnverifiable = input.Dependencies.Count(DependencyStatusKind.Unverifiable);

        var conflicts = input.Config.Settings.Count(s => s.HasConflict);
        var shadowed = input.Artifacts.Artifacts.Count(a => a.IsShadowing);
        var warnings = shadowed + depUnverifiable;

        var health = Math.Clamp(100 - 8 * depMissing - 8 * mcpDown - 3 * conflicts, 0, 100);

        var stats = new List<StatCard>
        {
            new("Commands", "01", commands.Count, null, BadgeTone.None,
                $"{cmdUser} user / {cmdProject} project / {cmdPlugin} plugin"),
            new("Skills+Agents", "02", skillsAgents, null, BadgeTone.None,
                $"{pluginNames} plugin{(pluginNames == 1 ? "" : "s")}"),
            new("MCP Servers", "03", mcpTotal, mcpDown > 0 ? $"{mcpDown} down" : null,
                mcpDown > 0 ? BadgeTone.Bad : BadgeTone.None, $"{mcpTotal - mcpDown} reachable"),
            new("Dependencies", "04", depTotal, depMissing > 0 ? $"{depMissing} miss" : null,
                depMissing > 0 ? BadgeTone.Warn : BadgeTone.None,
                depMissing > 0 ? "missing on PATH" : "all resolved"),
            new("Conflicts", "05", conflicts, null, BadgeTone.None, "overrides resolved"),
            new("Warnings", "06", warnings, null, BadgeTone.None, "non-blocking"),
        };

        var attention = new List<AttentionItem>();
        foreach (var r in input.Dependencies.Results.Where(r => r.Status.Kind == DependencyStatusKind.Missing))
            attention.Add(new AttentionItem(AttentionTone.Bad, $"Missing {r.Ref.Name}",
                r.Ref.ReferencedBy.Count > 0 ? $"required by {string.Join(", ", r.Ref.ReferencedBy)}" : "unresolved on PATH"));
        foreach (var s in input.Config.Settings.Where(s => s.HasConflict))
            attention.Add(new AttentionItem(AttentionTone.Warn, $"{s.Key} conflict",
                "multiple scopes set this value"));
        foreach (var r in input.Dependencies.Results.Where(r => r.Status.Kind == DependencyStatusKind.Unverifiable))
            attention.Add(new AttentionItem(AttentionTone.Info, $"{r.Ref.Name} probe inconclusive",
                "present, not allowlisted for probing"));

        var recent = input.RecentChanges.Reverse()
            .Take(5)
            .Select(c => new RecentChange(c.Id, c.Description,
                $"{c.Scope} · {c.Timestamp}{(c.IsUndone ? " · undone" : "")}", c.IsUndone))
            .ToList();

        var caption = $"{depMissing} dep missing · {mcpDown} server down · {conflicts} conflicts";
        return new DashboardData(health, caption, input.ProjectLabel,
            "user → project → local", stats, attention, recent);
    }
}
