using ClaudeExplorer.App.Environments;
using ClaudeExplorer.App.Services;
using ClaudeExplorer.App.Tests.Fakes;

namespace ClaudeExplorer.App.Tests.Services;

public class ActiveEnvironmentWorkspaceContextTests
{
    private static EnvironmentService Service(InMemoryFileSystem fs)
        => new(new EnvironmentDiscovery(fs, new FakeWslLocator(), "C:/Users/p"),
               new EnvironmentStore(fs, fs, "/s.json"));

    [Fact]
    public void Reflects_the_active_environments_dirs_and_label()
    {
        var fs = new InMemoryFileSystem();
        var svc = Service(fs); svc.Load();
        var ctx = new ActiveEnvironmentWorkspaceContext(svc);

        Assert.Equal("C:/Users/p", ctx.UserDir);
        Assert.Equal("", ctx.ProjectDir);                 // no project on Windows yet
        Assert.Equal("Windows", ctx.ProjectLabel);        // env name when no project

        svc.AddCustom("D:/cfg", "My Root");
        svc.SetActive("custom:D:/cfg");
        svc.SetProject("custom:D:/cfg", "/work/app");

        Assert.Equal("D:/cfg", ctx.UserDir);
        Assert.Equal("/work/app", ctx.ProjectDir);
        Assert.Equal("My Root · app", ctx.ProjectLabel);  // env · project segment
    }
}
