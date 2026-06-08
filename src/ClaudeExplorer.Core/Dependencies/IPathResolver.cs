using System.Runtime.InteropServices;

namespace ClaudeExplorer.Core.Dependencies;

/// <summary>Resolves an executable name to a full path on the system PATH (like which/where).</summary>
public interface IPathResolver
{
    /// <summary>The resolved path, or <c>null</c> if the executable is not on PATH.</summary>
    string? Resolve(string executable);
}

/// <summary>
/// Real PATH resolver. Not unit-tested (it reads the machine environment), mirroring
/// <c>PhysicalFileSystem</c>. On Windows, candidate extensions come from PATHEXT.
/// </summary>
public sealed class PhysicalPathResolver : IPathResolver
{
    public string? Resolve(string executable)
    {
        if (string.IsNullOrWhiteSpace(executable)) return null;

        // An explicit path (already contains a separator) is checked directly.
        if (executable.Contains('/') || executable.Contains('\\'))
            return File.Exists(executable) ? Normalize(executable) : null;

        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        var dirs = pathVar.Split(Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var dir in dirs)
            foreach (var ext in Extensions())
            {
                var candidate = Path.Combine(dir, executable + ext);
                if (File.Exists(candidate)) return Normalize(candidate);
            }

        return null;
    }

    private static string[] Extensions()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new[] { "" };

        var pathext = Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT;.COM";
        var exts = pathext.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // PATHEXT variants FIRST: on Windows the launchable file is e.g. `npx.cmd`, not the
        // extensionless Unix shim `npx` that also ships in the same dir. "" goes last so an
        // already-qualified name (e.g. "node.exe") still resolves.
        return exts.Concat(new[] { "" }).ToArray();
    }

    private static string Normalize(string p) => p.Replace('\\', '/');
}
