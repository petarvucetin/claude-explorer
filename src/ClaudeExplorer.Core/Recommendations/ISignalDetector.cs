using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Recommendations;

/// <summary>Detects one family of project signals from a project tree (local, read-only).</summary>
public interface ISignalDetector
{
    IReadOnlyList<Signal> Detect(string projectDir);
}

/// <summary>Shared helpers for marker-file detectors.</summary>
public abstract class SignalDetectorBase
{
    protected readonly IFileSystem Fs;
    protected SignalDetectorBase(IFileSystem fs) => Fs = fs;

    /// <summary>The first of <paramref name="relativeNames"/> that exists under the project, else null.</summary>
    protected string? FirstExisting(string projectDir, params string[] relativeNames)
    {
        foreach (var name in relativeNames)
        {
            var path = $"{projectDir}/{name}";
            if (Fs.FileExists(path)) return path;
        }
        return null;
    }
}

/// <summary>Detects programming languages from well-known marker files.</summary>
public sealed class LanguageSignalDetector : SignalDetectorBase, ISignalDetector
{
    public LanguageSignalDetector(IFileSystem fs) : base(fs) { }

    public IReadOnlyList<Signal> Detect(string projectDir)
    {
        var signals = new List<Signal>();
        void Add(string value, string? file)
        {
            if (file is not null)
                signals.Add(new Signal(SignalKind.Language, value, new[] { new Evidence(file) }));
        }

        Add("javascript", FirstExisting(projectDir, "package.json"));
        Add("typescript", FirstExisting(projectDir, "tsconfig.json"));
        Add("python", FirstExisting(projectDir, "pyproject.toml", "requirements.txt", "setup.py"));
        Add("go", FirstExisting(projectDir, "go.mod"));
        Add("rust", FirstExisting(projectDir, "Cargo.toml"));
        Add("java", FirstExisting(projectDir, "pom.xml", "build.gradle"));

        var csproj = Fs.GetFiles(projectDir, "*.csproj", recurse: true);
        if (csproj.Count > 0)
            signals.Add(new Signal(SignalKind.Language, "csharp", new[] { new Evidence(csproj[0], csproj.Count) }));

        return signals;
    }
}
