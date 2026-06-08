using ClaudeExplorer.Core.Dependencies;

namespace ClaudeExplorer.App.Environments;

/// <summary>
/// Real WSL locator over <see cref="IProcessRunner"/>. Not unit-tested (it touches the machine,
/// mirroring the Core <c>Physical*</c> seams) — but the output sanitization helpers
/// <see cref="CleanLines"/> / <see cref="CleanPath"/> ARE tested, because <c>wsl.exe -l -q</c> emits
/// UTF-16LE (interleaved NUL bytes).
/// </summary>
public sealed class WslLocator : IWslLocator
{
    private readonly IProcessRunner _runner;
    private readonly string _wsl;

    public WslLocator(IProcessRunner runner, string wslExecutable = "wsl.exe")
    {
        _runner = runner;
        _wsl = wslExecutable;
    }

    public IReadOnlyList<string> ListDistros()
    {
        var result = _runner.Run(_wsl, new[] { "-l", "-q" });
        return result.Success ? CleanLines(result.StdOut) : Array.Empty<string>();
    }

    public string? ResolveHome(string distro)
    {
        var result = _runner.Run(_wsl, new[] { "-d", distro, "--", "sh", "-c", "wslpath -w \"$HOME\"" });
        return result.Success ? CleanPath(result.StdOut) : null;
    }

    /// <summary>Split process output into clean lines, tolerating UTF-16LE NUL interleaving.</summary>
    public static IReadOnlyList<string> CleanLines(string? raw)
        => (raw ?? "")
            .Replace("\0", "")
            .Replace("\r", "")
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

    /// <summary>Clean a single path line; null when empty.</summary>
    public static string? CleanPath(string? raw)
    {
        var cleaned = (raw ?? "").Replace("\0", "").Replace("\r", "").Replace("\n", "").Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }
}
