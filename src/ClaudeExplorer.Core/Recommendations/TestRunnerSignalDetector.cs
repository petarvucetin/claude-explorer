using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Recommendations;

/// <summary>Detects test runners from their config files.</summary>
public sealed class TestRunnerSignalDetector : SignalDetectorBase, ISignalDetector
{
    public TestRunnerSignalDetector(IFileSystem fs) : base(fs) { }

    public IReadOnlyList<Signal> Detect(string projectDir)
    {
        var signals = new List<Signal>();
        void Add(string value, string? file)
        {
            if (file is not null)
                signals.Add(new Signal(SignalKind.TestRunner, value, new[] { new Evidence(file) }));
        }

        Add("playwright", FirstExisting(projectDir, "playwright.config.ts", "playwright.config.js"));
        Add("jest", FirstExisting(projectDir,
            "jest.config.js", "jest.config.ts", "jest.config.mjs", "jest.config.cjs", "jest.config.json"));
        Add("vitest", FirstExisting(projectDir, "vitest.config.ts", "vitest.config.js"));
        Add("pytest", FirstExisting(projectDir, "pytest.ini", "conftest.py"));

        return signals;
    }
}

/// <summary>Detects databases/ORMs from marker files and SQL migrations.</summary>
public sealed class DatabaseSignalDetector : SignalDetectorBase, ISignalDetector
{
    public DatabaseSignalDetector(IFileSystem fs) : base(fs) { }

    public IReadOnlyList<Signal> Detect(string projectDir)
    {
        var signals = new List<Signal>();

        var prisma = $"{projectDir}/prisma/schema.prisma";
        if (Fs.FileExists(prisma))
            signals.Add(new Signal(SignalKind.Database, "prisma", new[] { new Evidence(prisma) }));

        var migrations = Fs.GetFiles($"{projectDir}/migrations", "*.sql", recurse: true);
        if (migrations.Count > 0)
            signals.Add(new Signal(SignalKind.Database, "sql",
                new[] { new Evidence(migrations[0], migrations.Count, "migrations/*.sql") }));

        var knex = FirstExisting(projectDir, "knexfile.js", "knexfile.ts");
        if (knex is not null)
            signals.Add(new Signal(SignalKind.Database, "knex", new[] { new Evidence(knex) }));

        return signals;
    }
}
