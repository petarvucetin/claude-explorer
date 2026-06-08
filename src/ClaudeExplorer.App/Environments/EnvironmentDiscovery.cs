using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.App.Environments;

/// <summary>Enumerates Claude environments: always a Windows one, plus each WSL distro whose home
/// contains a <c>.claude</c> folder. Custom (user-added) environments are layered on by
/// <see cref="EnvironmentService"/>, not here.</summary>
public sealed class EnvironmentDiscovery
{
    private readonly IFileSystem _fs;
    private readonly IWslLocator _wsl;
    private readonly string _windowsHome;

    public EnvironmentDiscovery(IFileSystem fs, IWslLocator wsl, string windowsHome)
    {
        _fs = fs;
        _wsl = wsl;
        _windowsHome = windowsHome.Replace('\\', '/').TrimEnd('/');
    }

    public IReadOnlyList<ClaudeEnvironment> Discover()
    {
        var envs = new List<ClaudeEnvironment>
        {
            new("windows", "Windows", EnvironmentKind.Windows, _windowsHome, null),
        };

        foreach (var distro in _wsl.ListDistros())
        {
            var home = _wsl.ResolveHome(distro);
            if (home is null) continue;
            var userDir = home.Replace('\\', '/').TrimEnd('/');
            if (_fs.DirectoryExists($"{userDir}/.claude"))
                envs.Add(new ClaudeEnvironment($"wsl:{distro}", $"WSL · {distro}", EnvironmentKind.Wsl, userDir, null));
        }

        return envs;
    }
}
