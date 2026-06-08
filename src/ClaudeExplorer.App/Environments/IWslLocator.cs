namespace ClaudeExplorer.App.Environments;

/// <summary>Resolves WSL distro names and their home directories (as Windows-accessible paths).
/// The real impl shells out to <c>wsl.exe</c>; tests use a fake.</summary>
public interface IWslLocator
{
    /// <summary>Installed WSL distro names (empty when WSL is absent).</summary>
    IReadOnlyList<string> ListDistros();

    /// <summary>The distro's home as a Windows path (<c>\\wsl.localhost\&lt;distro&gt;\home\…</c>),
    /// or null if it can't be resolved.</summary>
    string? ResolveHome(string distro);
}
