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

    public DependencyChecker(IPathResolver resolver, IProcessRunner runner)
    {
        _resolver = resolver;
        _runner = runner;
    }

    public DependencyReport Check(IReadOnlyList<DependencyRef> refs)
        => new(refs.Select(CheckOne).ToList());

    private DependencyResult CheckOne(DependencyRef dep)
    {
        var path = _resolver.Resolve(dep.Name);
        if (path is null)
            return new DependencyResult(dep, new DependencyStatus(DependencyStatusKind.Missing));

        if (!RuntimeAllowlist.IsAllowed(dep.Name))
            return new DependencyResult(dep, new DependencyStatus(DependencyStatusKind.Unverifiable, Path: path));

        // Allowlisted + present: the only case where we execute anything, and only `--version`.
        // Probe the resolved path (not the bare name) so the captured version reflects exactly the
        // binary we resolved, with no second PATH lookup (closes a resolve→exec TOCTOU window).
        var probe = _runner.Run(path, RuntimeAllowlist.ProbeArguments);
        return new DependencyResult(dep,
            new DependencyStatus(DependencyStatusKind.Found, ParseVersion(probe), path));
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
