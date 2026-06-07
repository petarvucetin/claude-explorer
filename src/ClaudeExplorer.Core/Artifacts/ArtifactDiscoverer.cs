using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Artifacts;

/// <summary>A plugin to scan: a display name and the directory containing its commands/skills/agents.</summary>
public sealed record PluginLocation(string Name, string RootPath);

public sealed class ArtifactDiscoverer
{
    private readonly IFileSystem _fs;

    public ArtifactDiscoverer(IFileSystem fs) => _fs = fs;

    public IReadOnlyList<DiscoveredArtifact> Discover(
        string userDir, string? projectDir, IReadOnlyList<PluginLocation> plugins)
    {
        var result = new List<DiscoveredArtifact>();
        result.AddRange(DiscoverScope($"{userDir}/.claude", new ArtifactSource(ArtifactSourceKind.User)));
        if (projectDir is not null)
            result.AddRange(DiscoverScope($"{projectDir}/.claude", new ArtifactSource(ArtifactSourceKind.Project)));
        foreach (var plugin in plugins)
            result.AddRange(DiscoverScope(plugin.RootPath, new ArtifactSource(ArtifactSourceKind.Plugin, plugin.Name)));
        return result;
    }

    private IEnumerable<DiscoveredArtifact> DiscoverScope(string root, ArtifactSource source)
    {
        foreach (var a in DiscoverCommands($"{root}/commands", source)) yield return a;
        foreach (var a in DiscoverSkills($"{root}/skills", source)) yield return a;
    }

    private IEnumerable<DiscoveredArtifact> DiscoverCommands(string dir, ArtifactSource source)
    {
        foreach (var file in _fs.GetFiles(dir, "*.md", recurse: true))
        {
            var fm = Frontmatter.Parse(_fs.ReadAllText(file));
            var name = NameFrom(fm, FileNameWithoutExtension(file));
            yield return new DiscoveredArtifact(ArtifactKind.Command, name, ArtifactSummary.Extract(fm), source, file);
        }
    }

    private IEnumerable<DiscoveredArtifact> DiscoverSkills(string dir, ArtifactSource source)
    {
        foreach (var sub in _fs.GetDirectories(dir))
        {
            var skillFile = $"{sub}/SKILL.md";
            if (!_fs.FileExists(skillFile)) continue;
            var fm = Frontmatter.Parse(_fs.ReadAllText(skillFile));
            var name = NameFrom(fm, LastSegment(sub));
            yield return new DiscoveredArtifact(ArtifactKind.Skill, name, ArtifactSummary.Extract(fm), source, skillFile);
        }
    }

    private static string NameFrom(FrontmatterResult fm, string fallback)
        => fm.Fields.TryGetValue("name", out var n) && n.Length > 0 ? n : fallback;

    private static string LastSegment(string path)
    {
        var trimmed = path.TrimEnd('/');
        var i = trimmed.LastIndexOf('/');
        return i >= 0 ? trimmed.Substring(i + 1) : trimmed;
    }

    private static string FileNameWithoutExtension(string path)
    {
        var seg = LastSegment(path);
        var dot = seg.LastIndexOf('.');
        return dot > 0 ? seg.Substring(0, dot) : seg;
    }
}
