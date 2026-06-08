using System.ComponentModel;
using System.Diagnostics;

namespace ClaudeExplorer.Core.Dependencies;

/// <summary>Result of running a probe process.</summary>
public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Success => ExitCode == 0;
}

/// <summary>
/// Runs a single external process and captures its output. Phase-3 callers use this ONLY to run
/// allowlisted <c>--version</c> probes (see <see cref="RuntimeAllowlist"/>) — it must never be used
/// to execute a discovered hook/MCP command or any non-allowlisted binary.
/// </summary>
public interface IProcessRunner
{
    ProcessResult Run(string executable, IReadOnlyList<string> arguments);
}

/// <summary>
/// Real process runner. Not unit-tested (it touches the machine), mirroring
/// <c>PhysicalFileSystem</c>. Reads both output streams asynchronously so a full pipe buffer can't
/// deadlock the wait, and enforces a timeout.
/// </summary>
public sealed class PhysicalProcessRunner : IProcessRunner
{
    private const int TimedOutExitCode = -1;
    private const int FailedToStartExitCode = -2;
    private readonly int _timeoutMs;

    public PhysicalProcessRunner(int timeoutMs = 5000) => _timeoutMs = timeoutMs;

    public ProcessResult Run(string executable, IReadOnlyList<string> arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in arguments) psi.ArgumentList.Add(arg);

        Process process;
        try
        {
            process = Process.Start(psi)!;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            // The resolved path isn't a launchable executable on this OS (e.g. an extensionless Unix
            // shim on Windows), or otherwise can't be started. A probe failure must never crash the
            // caller — report it as a non-result instead of throwing.
            return new ProcessResult(FailedToStartExitCode, "", ex.Message);
        }

        using (process)
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(_timeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
                // Let the readers observe the now-closed pipe before we dispose the Process, so we
                // don't abandon tasks bound to a disposed handle. Best-effort, bounded by the timeout.
                try { Task.WaitAll(new[] { stdoutTask, stderrTask }, _timeoutMs); } catch { /* best effort */ }
                return new ProcessResult(TimedOutExitCode, "", "");
            }

            return new ProcessResult(process.ExitCode,
                stdoutTask.GetAwaiter().GetResult(),
                stderrTask.GetAwaiter().GetResult());
        }
    }
}
