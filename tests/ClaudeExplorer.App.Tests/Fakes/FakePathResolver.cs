using ClaudeExplorer.Core.Dependencies;

namespace ClaudeExplorer.App.Tests.Fakes;

/// <summary>Deterministic path resolver for App tests: only executables explicitly added are "on PATH".</summary>
public sealed class FakePathResolver : IPathResolver
{
    private readonly Dictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase);

    public FakePathResolver Add(string executable, string path)
    {
        _paths[executable] = path;
        return this;
    }

    public string? Resolve(string executable)
        => _paths.TryGetValue(executable, out var p) ? p : null;
}
