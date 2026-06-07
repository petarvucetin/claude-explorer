using ClaudeExplorer.Core.Recommendations;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Recommendations;

public class TestRunnerAndDatabaseDetectorTests
{
    [Fact]
    public void Detects_playwright_and_pytest()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/proj/playwright.config.ts", "x")
            .AddFile("/proj/conftest.py", "x");

        var signals = new TestRunnerSignalDetector(fs).Detect("/proj");

        var pw = signals.Single(s => s.Value == "playwright");
        Assert.Equal(SignalKind.TestRunner, pw.Kind);
        Assert.Equal("/proj/playwright.config.ts", pw.Evidence[0].FilePath);
        Assert.Contains(signals, s => s.Value == "pytest");
    }

    [Fact]
    public void Detects_prisma_and_sql_migrations_with_count()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/proj/prisma/schema.prisma", "x")
            .AddFile("/proj/migrations/0001_init.sql", "x")
            .AddFile("/proj/migrations/0002_more.sql", "x");

        var signals = new DatabaseSignalDetector(fs).Detect("/proj");

        Assert.Contains(signals, s => s.Value == "prisma");
        var sql = signals.Single(s => s.Value == "sql");
        Assert.Equal(2, sql.Evidence[0].Count);
    }

    [Fact]
    public void No_test_or_db_markers_yields_nothing()
    {
        var fs = new InMemoryFileSystem().AddFile("/proj/readme.md", "x");
        Assert.Empty(new TestRunnerSignalDetector(fs).Detect("/proj"));
        Assert.Empty(new DatabaseSignalDetector(fs).Detect("/proj"));
    }
}
