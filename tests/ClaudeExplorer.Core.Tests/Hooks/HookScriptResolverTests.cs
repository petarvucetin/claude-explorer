using ClaudeExplorer.Core.Hooks;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Hooks;

public class HookScriptResolverTests
{
    private const string Src = "/home/.claude";
    private const string Proj = "/repo";
    private const string User = "/home/.claude";

    [Fact]
    public void Resolves_node_script_relative_to_source_dir()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/home/.claude/hooks/posttool.js", "console.log(1)");

        var r = HookScriptResolver.Resolve(fs, "node hooks/posttool.js", Src, Proj, User);

        Assert.NotNull(r);
        Assert.Equal("/home/.claude/hooks/posttool.js", r!.Path);
        Assert.Equal("javascript", r.Language);
        Assert.True(r.Exists);
    }

    [Fact]
    public void Resolves_python_script_relative_to_project()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/repo/.claude/hooks/guard.py", "print(1)");

        var r = HookScriptResolver.Resolve(fs, "python3 .claude/hooks/guard.py", Src, Proj, User);

        Assert.Equal("python", r!.Language);
        Assert.True(r.Exists);
    }

    [Fact]
    public void Maps_shell_and_powershell_extensions()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/repo/x.sh", "echo hi");
        fs.AddFile("/repo/y.ps1", "Write-Host hi");

        Assert.Equal("bash", HookScriptResolver.Resolve(fs, "bash x.sh", "", Proj, "")!.Language);
        Assert.Equal("powershell", HookScriptResolver.Resolve(fs, "pwsh y.ps1", "", Proj, "")!.Language);
    }

    [Fact]
    public void Templated_plugin_path_is_unresolvable()
        => Assert.Null(HookScriptResolver.Resolve(
            new InMemoryFileSystem(), "\"${CLAUDE_PLUGIN_ROOT}/hooks/run-hook.cmd\" x", Src, Proj, User));

    [Fact]
    public void Bare_binary_with_no_script_file_returns_null()
        => Assert.Null(HookScriptResolver.Resolve(new InMemoryFileSystem(), "prettier --write", Src, Proj, User));

    [Fact]
    public void Known_extension_not_on_disk_returns_ref_marked_missing()
    {
        var r = HookScriptResolver.Resolve(new InMemoryFileSystem(), "bash scripts/format.sh", "", Proj, "");
        Assert.NotNull(r);
        Assert.False(r!.Exists);
        Assert.Equal("bash", r.Language);
    }
}
