using ClaudeExplorer.App.Services;
using ClaudeExplorer.App.Tests.Fakes;

namespace ClaudeExplorer.App.Tests.Services;

public class WorkspaceResolverTests
{
    [Fact]
    public void Returns_null_when_neither_arg_nor_cwd_is_a_claude_project()
    {
        var fs = new InMemoryFileSystem(); // nothing on disk

        Assert.Null(WorkspaceResolver.ResolveProjectDir(Array.Empty<string>(), "/work/plain", fs));
    }

    [Fact]
    public void Uses_current_dir_when_it_contains_a_dotclaude_folder()
    {
        var fs = new InMemoryFileSystem().AddFile("/work/proj/.claude/settings.json", "{}");

        Assert.Equal("/work/proj", WorkspaceResolver.ResolveProjectDir(Array.Empty<string>(), "/work/proj", fs));
    }

    [Fact]
    public void Prefers_an_explicit_arg_that_is_a_claude_project_over_the_cwd()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/work/proj/.claude/settings.json", "{}")
            .AddFile("/other/cwd/.claude/settings.json", "{}");

        Assert.Equal("/work/proj",
            WorkspaceResolver.ResolveProjectDir(new[] { "/work/proj" }, "/other/cwd", fs));
    }

    [Fact]
    public void Ignores_an_arg_that_is_not_a_claude_project_and_falls_back_to_cwd()
    {
        var fs = new InMemoryFileSystem().AddFile("/cwd/.claude/settings.json", "{}");

        Assert.Equal("/cwd",
            WorkspaceResolver.ResolveProjectDir(new[] { "--flag", "/not/a/project" }, "/cwd", fs));
    }

    [Fact]
    public void IsClaudeProject_is_false_for_blank_or_plain_dirs()
    {
        var fs = new InMemoryFileSystem().AddFile("/p/.claude/x", "1");

        Assert.False(WorkspaceResolver.IsClaudeProject("", fs));
        Assert.False(WorkspaceResolver.IsClaudeProject("   ", fs));
        Assert.False(WorkspaceResolver.IsClaudeProject("/plain", fs));
        Assert.True(WorkspaceResolver.IsClaudeProject("/p", fs));
        Assert.True(WorkspaceResolver.IsClaudeProject(@"\p\", fs)); // backslashes + trailing slash normalized
    }
}
