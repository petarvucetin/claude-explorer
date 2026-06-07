using ClaudeExplorer.Core.Dependencies;

namespace ClaudeExplorer.Core.Tests.Fakes;

/// <summary>
/// Deterministic process runner. Returns a canned <see cref="ProcessResult"/> per executable and
/// records every invocation so tests can assert the allowlist/probe contract was honored.
/// </summary>
public sealed class FakeProcessRunner : IProcessRunner
{
    private readonly Dictionary<string, ProcessResult> _results = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every (executable, arguments) pair <see cref="Run"/> was called with, in order.</summary>
    public List<(string Executable, IReadOnlyList<string> Arguments)> Invocations { get; } = new();

    public FakeProcessRunner AddVersion(string executable, string stdout, int exitCode = 0)
    {
        _results[executable] = new ProcessResult(exitCode, stdout, "");
        return this;
    }

    public FakeProcessRunner AddResult(string executable, ProcessResult result)
    {
        _results[executable] = result;
        return this;
    }

    public ProcessResult Run(string executable, IReadOnlyList<string> arguments)
    {
        Invocations.Add((executable, arguments));
        return _results.TryGetValue(executable, out var r) ? r : new ProcessResult(-1, "", "not found");
    }
}
