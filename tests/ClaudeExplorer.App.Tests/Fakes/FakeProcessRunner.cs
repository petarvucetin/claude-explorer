using ClaudeExplorer.Core.Dependencies;

namespace ClaudeExplorer.App.Tests.Fakes;

/// <summary>Deterministic process runner for App tests. Records every invocation and
/// returns a canned <see cref="ProcessResult"/> per executable.</summary>
public sealed class FakeProcessRunner : IProcessRunner
{
    private readonly Dictionary<string, ProcessResult> _results = new(StringComparer.OrdinalIgnoreCase);

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
