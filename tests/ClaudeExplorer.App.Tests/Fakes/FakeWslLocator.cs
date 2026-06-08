using ClaudeExplorer.App.Environments;

namespace ClaudeExplorer.App.Tests.Fakes;

public sealed class FakeWslLocator : IWslLocator
{
    private readonly List<string> _distros = new();
    private readonly Dictionary<string, string> _homes = new(StringComparer.Ordinal);

    public FakeWslLocator AddDistro(string name, string? home = null)
    {
        _distros.Add(name);
        if (home is not null) _homes[name] = home;
        return this;
    }

    public IReadOnlyList<string> ListDistros() => _distros;
    public string? ResolveHome(string distro) => _homes.TryGetValue(distro, out var h) ? h : null;
}
