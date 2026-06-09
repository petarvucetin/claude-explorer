using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Dependencies;

/// <summary>
/// Classifies each <see cref="DependencyRef"/> as Found / Missing / Unverifiable.
/// Safety contract: a runtime is executed ONLY when it is on the allowlist, and ONLY with
/// <c>--version</c>. A discovered command is never run, and a present-but-non-allowlisted binary is
/// reported Unverifiable without being executed.
/// </summary>
public sealed class DependencyChecker
{
    private readonly IPathResolver _resolver;
    private readonly IProcessRunner _runner;
    private readonly IFileSystem _fs;

    public DependencyChecker(IPathResolver resolver, IProcessRunner runner, IFileSystem fs)
    {
        _resolver = resolver;
        _runner = runner;
        _fs = fs;
    }

    public DependencyReport Check(IReadOnlyList<DependencyRef> refs)
        => new(refs.Select(CheckOne).ToList());

    private DependencyResult CheckOne(DependencyRef dep)
    {
        // A plugin-local script (${CLAUDE_PLUGIN_ROOT}-resolved) lives at a known absolute path, not on
        // PATH: health is whether that file exists. Never resolve it on PATH and never execute it.
        if (dep.ResolvedPath is not null)
            return _fs.FileExists(dep.ResolvedPath)
                ? new DependencyResult(dep, new DependencyStatus(DependencyStatusKind.Found, Path: dep.ResolvedPath))
                : new DependencyResult(dep, new DependencyStatus(DependencyStatusKind.Missing));

        var path = _resolver.Resolve(dep.Name);
        if (path is null)
            return new DependencyResult(dep, new DependencyStatus(DependencyStatusKind.Missing));

        if (!RuntimeAllowlist.IsAllowed(dep.Name))
            return new DependencyResult(dep, new DependencyStatus(DependencyStatusKind.Unverifiable, Path: path));

        // Allowlisted + present: the only case where we execute anything, and only `--version`.
        // Probe the resolved path (not the bare name) so the captured version reflects exactly the
        // binary we resolved, with no second PATH lookup (closes a resolve→exec TOCTOU window).
        // It's resolved on PATH, so it's Found; if the probe couldn't run, just leave the version
        // blank rather than reporting the error text as a version.
        var probe = _runner.Run(path, RuntimeAllowlist.ProbeArguments);
        return new DependencyResult(dep,
            new DependencyStatus(DependencyStatusKind.Found, probe.Success ? ParseVersion(probe) : null, path));
    }

    private static string? ParseVersion(ProcessResult probe)
    {
        var text = !string.IsNullOrWhiteSpace(probe.StdOut) ? probe.StdOut : probe.StdErr;
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0) return trimmed;
        }
        return null;
    }
}
