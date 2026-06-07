using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Recommendations;

/// <summary>Detects web frameworks from their config files.</summary>
public sealed class FrameworkSignalDetector : SignalDetectorBase, ISignalDetector
{
    public FrameworkSignalDetector(IFileSystem fs) : base(fs) { }

    public IReadOnlyList<Signal> Detect(string projectDir)
    {
        var signals = new List<Signal>();
        void Add(string value, string? file)
        {
            if (file is not null)
                signals.Add(new Signal(SignalKind.Framework, value, new[] { new Evidence(file) }));
        }

        Add("nextjs", FirstExisting(projectDir, "next.config.js", "next.config.ts", "next.config.mjs"));
        Add("nuxt", FirstExisting(projectDir, "nuxt.config.js", "nuxt.config.ts"));
        Add("angular", FirstExisting(projectDir, "angular.json"));
        Add("astro", FirstExisting(projectDir, "astro.config.js", "astro.config.ts", "astro.config.mjs"));
        Add("svelte", FirstExisting(projectDir, "svelte.config.js", "svelte.config.ts"));

        return signals;
    }
}

/// <summary>Runs all signal detectors and aggregates their output into <see cref="ProjectSignals"/>.</summary>
public sealed class SignalDetectionService
{
    private readonly IReadOnlyList<ISignalDetector> _detectors;

    public SignalDetectionService(IFileSystem fs)
        => _detectors = new ISignalDetector[]
        {
            new LanguageSignalDetector(fs),
            new FrameworkSignalDetector(fs),
            new TestRunnerSignalDetector(fs),
            new DatabaseSignalDetector(fs),
        };

    /// <summary>Overload for a custom detector set (extensibility / testing).</summary>
    public SignalDetectionService(IReadOnlyList<ISignalDetector> detectors) => _detectors = detectors;

    public ProjectSignals Detect(string projectDir)
        => new(_detectors.SelectMany(d => d.Detect(projectDir)).ToList());
}
