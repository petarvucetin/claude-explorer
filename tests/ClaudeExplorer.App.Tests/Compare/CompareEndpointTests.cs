using ClaudeExplorer.App.Compare;

namespace ClaudeExplorer.App.Tests.Compare;

public class CompareEndpointTests
{
    [Fact]
    public void Base_reads_user_dir_with_no_project()
    {
        var ep = CompareEndpoint.Base("win", "Base · Windows", "C:/Users/me");
        Assert.Equal(EndpointKind.Base, ep.Kind);
        Assert.Equal("C:/Users/me", ep.ReadUserDir);
        Assert.Equal("", ep.ReadProjectDir);
    }

    [Fact]
    public void Project_reads_project_dir_with_no_user()
    {
        var ep = CompareEndpoint.Project("p1", "Project A", "D:/work/a");
        Assert.Equal(EndpointKind.Project, ep.Kind);
        Assert.Equal("", ep.ReadUserDir);
        Assert.Equal("D:/work/a", ep.ReadProjectDir);
    }
}
