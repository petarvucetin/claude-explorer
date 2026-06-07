using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Dependencies;

public class FakePathResolverTests
{
    [Fact]
    public void Resolves_added_executables_and_returns_null_for_others()
    {
        var resolver = new FakePathResolver().Add("node", "/usr/bin/node");

        Assert.Equal("/usr/bin/node", resolver.Resolve("node"));
        Assert.Null(resolver.Resolve("python"));
    }

    [Fact]
    public void Resolution_is_case_insensitive()
    {
        var resolver = new FakePathResolver().Add("node", "/usr/bin/node");
        Assert.Equal("/usr/bin/node", resolver.Resolve("NODE"));
    }
}
