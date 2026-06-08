using ClaudeExplorer.App.Services;

namespace ClaudeExplorer.App.Tests.Services;

public class WorkspaceContextTests
{
    [Fact]
    public void ProjectLabel_is_the_final_path_segment()
    {
        var ctx = new WorkspaceContext("/home/u", "/work/my-app");
        Assert.Equal("/work/my-app", ctx.ProjectDir);
        Assert.Equal("/home/u", ctx.UserDir);
        Assert.Equal("my-app", ctx.ProjectLabel);
    }

    [Fact]
    public void Backslashes_are_normalized_and_trailing_slash_trimmed()
    {
        var ctx = new WorkspaceContext(@"C:\Users\u", @"C:\work\proj\");
        Assert.Equal("C:/work/proj", ctx.ProjectDir);
        Assert.Equal("proj", ctx.ProjectLabel);
    }

    [Fact]
    public void Root_path_gives_a_non_empty_label()
    {
        var ctx = new WorkspaceContext("/home/u", "/");
        Assert.NotEmpty(ctx.ProjectLabel);
    }

    [Fact]
    public void Windows_drive_root_gives_non_empty_label()
    {
        var ctx = new WorkspaceContext(@"C:\u", @"C:\");
        Assert.NotEmpty(ctx.ProjectLabel);
    }
}
