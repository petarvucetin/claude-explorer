# Dependency Health Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** From the discovered Claude config (hooks + MCP servers), extract the executables it depends on and verify each is present on this machine — *safely*, by resolving on PATH and probing only an allowlisted set of runtimes with `--version`, never executing the discovered command itself.

**Architecture:** Builds on the Phase 1 config engine. Adds a `Dependencies` namespace with two new testability seams (`IProcessRunner`, `IPathResolver`, each with a `Physical*` impl in Core and an in-memory fake in tests), an `ExecutableExtractor` (command-line → runtime name), a minimal `McpServerReader` (pulls `mcpServers` from settings files + project `.mcp.json`), a `DependencyExtractor` (effective `hooks.*` + MCP servers → deduped `DependencyRef`s), a `DependencyChecker` (resolve + allowlisted `--version` probe → Found/Missing/Unverifiable), and a `DependencyHealthService` façade. Fully fixture-driven; the `Physical*` seams are the only code that touches the real machine and are intentionally not unit-tested (matching `PhysicalFileSystem`).

**Tech Stack:** .NET 10, C#, `System.Text.Json` (`System.Text.Json.Nodes`), `System.Diagnostics.Process` (in the `Physical` runner only), xUnit. No new NuGet dependencies.

---

## Scope & decisions

- **Dependency sources (v1):** (a) `command` strings inside `hooks.*` settings of the **effective** config (Phase 1 already merges/concats these across scopes), and (b) the `command` of each **stdio** MCP server. url/sse MCP servers carry no executable → skipped.
- **Minimal MCP reader scope:** read `mcpServers` from the located settings files (User/Project/Local/Enterprise via the existing `SettingsLocator`) and from a project-root `.mcp.json`. **Deferred (noted):** `~/.claude.json` mcpServers, plugin-provided MCP servers.
- **Executable extraction:** the runtime is the **first token** of the command line (surrounding quotes honored), reduced to its file name without directory, with a trailing `.exe`/`.cmd`/`.bat`/`.com` stripped. So `npx -y @scope/pkg` → `npx`, `uv run tool` → `uv`, `python -m mod` → `python`, `docker run img` → `docker`, `/usr/bin/node app.js` → `node`. Wrapper recognition is therefore implicit — the first token *is* the runtime, so a wrapped package name is never mistaken for the dependency.
- **Allowlist (probe-safe runtimes):** `node, npm, npx, pnpm, yarn, bun, deno, uv, uvx, python, python3, docker, podman, git, claude`. Matched **case-insensitively** — executable names are case-insensitive on the primary target OS (Windows). This is a deliberate, documented deviation from the ordinal/case-sensitive matching used elsewhere for config keys and artifact names (which is correct for *those* because Claude treats them case-sensitively).
- **Safety contract (hard rule):** only allowlisted runtimes are ever executed, and only with `--version`. A discovered hook/MCP command is **never** run; an arbitrary present-but-not-allowlisted binary is **never** executed (it is reported `Unverifiable`). Resolution is read-only (`IPathResolver`); probing goes through `IProcessRunner`.
- **Classification per dependency:** `Missing` (not resolvable on PATH) · `Unverifiable` (on PATH but not allowlisted, so intentionally not probed) · `Found` (on PATH **and** allowlisted **and** probed → version captured).
- **Version string:** the first non-empty line of the probe's stdout (falling back to stderr — some tools print `--version` to stderr).
- **Dedup:** dependencies are deduped by runtime name (case-insensitive); each `DependencyRef` carries the distinct, sorted list of sources that referenced it (`hook:<Event>`, `mcp:<server>`).

## File structure

- `src/ClaudeExplorer.Core/Dependencies/IProcessRunner.cs` — `ProcessResult` record + `IProcessRunner` + `PhysicalProcessRunner`.
- `src/ClaudeExplorer.Core/Dependencies/IPathResolver.cs` — `IPathResolver` + `PhysicalPathResolver`.
- `src/ClaudeExplorer.Core/Dependencies/RuntimeAllowlist.cs` — allowlist + probe args.
- `src/ClaudeExplorer.Core/Dependencies/ExecutableExtractor.cs` — command-line → runtime name.
- `src/ClaudeExplorer.Core/Dependencies/DependencyModel.cs` — `DependencyRef`, `DependencyStatusKind`, `DependencyStatus`, `DependencyResult`, `DependencyReport`.
- `src/ClaudeExplorer.Core/Dependencies/McpServerReader.cs` — `McpServer` record + reader.
- `src/ClaudeExplorer.Core/Dependencies/DependencyExtractor.cs` — effective config + MCP servers → `DependencyRef`s.
- `src/ClaudeExplorer.Core/Dependencies/DependencyChecker.cs` — resolve + probe → `DependencyReport`.
- `src/ClaudeExplorer.Core/Dependencies/DependencyHealthService.cs` — façade.
- `tests/ClaudeExplorer.Core.Tests/Fakes/FakeProcessRunner.cs` — in-memory `IProcessRunner`.
- `tests/ClaudeExplorer.Core.Tests/Fakes/FakePathResolver.cs` — in-memory `IPathResolver`.
- Tests under `tests/ClaudeExplorer.Core.Tests/Dependencies/`.

> **Note for the implementer:** the test project has global usings for `Xunit` (existing tests use `[Fact]`/`Assert` with no `using Xunit;`). Do **not** add `using Xunit;`. Paths use forward slashes throughout.

---

## Task 1: IProcessRunner seam + fake

**Files:**
- Create: `src/ClaudeExplorer.Core/Dependencies/IProcessRunner.cs`
- Create: `tests/ClaudeExplorer.Core.Tests/Fakes/FakeProcessRunner.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Dependencies/FakeProcessRunnerTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Dependencies/FakeProcessRunnerTests.cs`:
```csharp
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Dependencies;

public class FakeProcessRunnerTests
{
    [Fact]
    public void Returns_canned_result_and_records_the_invocation()
    {
        var runner = new FakeProcessRunner().AddVersion("node", "v20.10.0");

        var result = runner.Run("node", new[] { "--version" });

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("v20.10.0", result.StdOut);
        var call = Assert.Single(runner.Invocations);
        Assert.Equal("node", call.Executable);
        Assert.Equal(new[] { "--version" }, call.Arguments);
    }

    [Fact]
    public void Unknown_executable_returns_a_failure_result()
    {
        var result = new FakeProcessRunner().Run("ghost", new[] { "--version" });
        Assert.False(result.Success);
        Assert.NotEqual(0, result.ExitCode);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter FakeProcessRunnerTests`
Expected: FAIL — `IProcessRunner`/`ProcessResult`/`FakeProcessRunner` don't exist (compile error).

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Dependencies/IProcessRunner.cs`:
```csharp
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

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(_timeoutMs))
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            return new ProcessResult(TimedOutExitCode, "", "");
        }

        return new ProcessResult(process.ExitCode,
            stdoutTask.GetAwaiter().GetResult(),
            stderrTask.GetAwaiter().GetResult());
    }
}
```

Create `tests/ClaudeExplorer.Core.Tests/Fakes/FakeProcessRunner.cs`:
```csharp
using ClaudeExplorer.Core.Dependencies;

namespace ClaudeExplorer.Core.Tests.Fakes;

/// <summary>
/// Deterministic process runner. Returns a canned <see cref="ProcessResult"/> per executable and
/// records every invocation so tests can assert the allowlist/probe contract was honored.
/// </summary>
public sealed class FakeProcessRunner : IProcessRunner
{
    private readonly Dictionary<string, ProcessResult> _results = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every (executable, arguments) pair <see cref="Run"/> was called with, in order.</summary>
    public List<(string Executable, IReadOnlyList<string> Arguments)> Invocations { get; } = new();

    public FakeProcessRunner AddVersion(string executable, string stdout, int exitCode = 0)
    {
        _results[executable] = new ProcessResult(exitCode, stdout, "");
        return this;
    }

    public FakeProcessRunner AddResult(string executable, ProcessResult result)
    {
        _results[executable] = result;
        return this;
    }

    public ProcessResult Run(string executable, IReadOnlyList<string> arguments)
    {
        Invocations.Add((executable, arguments));
        return _results.TryGetValue(executable, out var r) ? r : new ProcessResult(-1, "", "not found");
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter FakeProcessRunnerTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): IProcessRunner seam + fake"
```

---

## Task 2: IPathResolver seam + fake

**Files:**
- Create: `src/ClaudeExplorer.Core/Dependencies/IPathResolver.cs`
- Create: `tests/ClaudeExplorer.Core.Tests/Fakes/FakePathResolver.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Dependencies/FakePathResolverTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Dependencies/FakePathResolverTests.cs`:
```csharp
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
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter FakePathResolverTests`
Expected: FAIL — `IPathResolver`/`FakePathResolver` don't exist (compile error).

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Dependencies/IPathResolver.cs`:
```csharp
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
        // Include "" first so an already-qualified name (e.g. "node.exe") still resolves.
        return new[] { "" }.Concat(exts).ToArray();
    }

    private static string Normalize(string p) => p.Replace('\\', '/');
}
```

Create `tests/ClaudeExplorer.Core.Tests/Fakes/FakePathResolver.cs`:
```csharp
using ClaudeExplorer.Core.Dependencies;

namespace ClaudeExplorer.Core.Tests.Fakes;

/// <summary>Deterministic resolver: only executables explicitly added are "on PATH".</summary>
public sealed class FakePathResolver : IPathResolver
{
    private readonly Dictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase);

    public FakePathResolver Add(string executable, string path)
    {
        _paths[executable] = path;
        return this;
    }

    public string? Resolve(string executable)
        => _paths.TryGetValue(executable, out var p) ? p : null;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter FakePathResolverTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): IPathResolver seam + fake"
```

---

## Task 3: Runtime allowlist

**Files:**
- Create: `src/ClaudeExplorer.Core/Dependencies/RuntimeAllowlist.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Dependencies/RuntimeAllowlistTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Dependencies/RuntimeAllowlistTests.cs`:
```csharp
using ClaudeExplorer.Core.Dependencies;

namespace ClaudeExplorer.Core.Tests.Dependencies;

public class RuntimeAllowlistTests
{
    [Theory]
    [InlineData("node")]
    [InlineData("npx")]
    [InlineData("uvx")]
    [InlineData("python3")]
    [InlineData("docker")]
    [InlineData("git")]
    [InlineData("claude")]
    public void Known_runtimes_are_allowed(string name)
    {
        Assert.True(RuntimeAllowlist.IsAllowed(name));
    }

    [Theory]
    [InlineData("rm")]
    [InlineData("curl")]
    [InlineData("my-custom-tool")]
    [InlineData("")]
    public void Unknown_executables_are_not_allowed(string name)
    {
        Assert.False(RuntimeAllowlist.IsAllowed(name));
    }

    [Fact]
    public void Membership_is_case_insensitive()
    {
        Assert.True(RuntimeAllowlist.IsAllowed("NODE"));
        Assert.True(RuntimeAllowlist.IsAllowed("Python3"));
    }

    [Fact]
    public void Probe_arguments_are_version_only()
    {
        Assert.Equal(new[] { "--version" }, RuntimeAllowlist.ProbeArguments);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter RuntimeAllowlistTests`
Expected: FAIL — `RuntimeAllowlist` doesn't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Dependencies/RuntimeAllowlist.cs`:
```csharp
namespace ClaudeExplorer.Core.Dependencies;

/// <summary>
/// The fixed set of runtimes we are willing to probe with a <c>--version</c> call, plus the probe
/// arguments. Membership is matched case-insensitively because executable names are
/// case-insensitive on the primary target OS (Windows); this is a deliberate exception to the
/// ordinal name matching used elsewhere for config keys and artifact names.
/// </summary>
public static class RuntimeAllowlist
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "node", "npm", "npx", "pnpm", "yarn", "bun", "deno",
        "uv", "uvx", "python", "python3",
        "docker", "podman", "git", "claude",
    };

    /// <summary>Arguments used for every probe — we only ever ask for a version.</summary>
    public static readonly IReadOnlyList<string> ProbeArguments = new[] { "--version" };

    public static bool IsAllowed(string executable) => Allowed.Contains(executable);

    /// <summary>A sorted snapshot of the allowlist, for display/tests.</summary>
    public static IReadOnlyList<string> Names => Allowed.OrderBy(x => x, StringComparer.Ordinal).ToList();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter RuntimeAllowlistTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): probe-safe runtime allowlist"
```

---

## Task 4: Executable extractor

**Files:**
- Create: `src/ClaudeExplorer.Core/Dependencies/ExecutableExtractor.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Dependencies/ExecutableExtractorTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Dependencies/ExecutableExtractorTests.cs`:
```csharp
using ClaudeExplorer.Core.Dependencies;

namespace ClaudeExplorer.Core.Tests.Dependencies;

public class ExecutableExtractorTests
{
    [Theory]
    [InlineData("npx -y @playwright/mcp@latest", "npx")]
    [InlineData("uvx some-mcp-server", "uvx")]
    [InlineData("uv run mytool", "uv")]
    [InlineData("python -m http.server", "python")]
    [InlineData("docker run --rm img", "docker")]
    [InlineData("podman run img", "podman")]
    [InlineData("/usr/local/bin/node script.js", "node")]
    public void Extracts_first_token_as_runtime_name(string commandLine, string expected)
    {
        Assert.Equal(expected, ExecutableExtractor.Extract(commandLine));
    }

    [Fact]
    public void Honors_quoting_and_strips_windows_extension()
    {
        Assert.Equal("node",
            ExecutableExtractor.Extract("\"C:/Program Files/nodejs/node.exe\" app.js"));
    }

    [Fact]
    public void Leading_whitespace_is_ignored()
    {
        Assert.Equal("git", ExecutableExtractor.Extract("   git status"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Blank_input_yields_null(string? commandLine)
    {
        Assert.Null(ExecutableExtractor.Extract(commandLine));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter ExecutableExtractorTests`
Expected: FAIL — `ExecutableExtractor` doesn't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Dependencies/ExecutableExtractor.cs`:
```csharp
namespace ClaudeExplorer.Core.Dependencies;

/// <summary>
/// Pulls the underlying executable out of a command line. The executable is the first token
/// (surrounding quotes honored), reduced to its file name without directory and without a trailing
/// Windows extension — so <c>/usr/bin/node</c> → <c>node</c> and <c>npx -y @scope/pkg</c> →
/// <c>npx</c>. Returns <c>null</c> for blank input.
/// </summary>
public static class ExecutableExtractor
{
    private static readonly string[] WindowsExtensions = { ".exe", ".cmd", ".bat", ".com" };

    public static string? Extract(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine)) return null;

        var first = FirstToken(commandLine);
        return first.Length == 0 ? null : BaseName(first);
    }

    private static string FirstToken(string s)
    {
        int i = 0;
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;

        if (i < s.Length && (s[i] == '"' || s[i] == '\''))
        {
            char quote = s[i++];
            int start = i;
            while (i < s.Length && s[i] != quote) i++;
            return s.Substring(start, i - start);
        }

        int tokenStart = i;
        while (i < s.Length && !char.IsWhiteSpace(s[i])) i++;
        return s.Substring(tokenStart, i - tokenStart);
    }

    private static string BaseName(string token)
    {
        var trimmed = token.Replace('\\', '/').TrimEnd('/');
        var slash = trimmed.LastIndexOf('/');
        var name = slash >= 0 ? trimmed.Substring(slash + 1) : trimmed;

        foreach (var ext in WindowsExtensions)
            if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return name.Substring(0, name.Length - ext.Length);

        return name;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter ExecutableExtractorTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): executable extraction from command lines"
```

---

## Task 5: Dependency domain model

**Files:**
- Create: `src/ClaudeExplorer.Core/Dependencies/DependencyModel.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Dependencies/DependencyModelTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Dependencies/DependencyModelTests.cs`:
```csharp
using ClaudeExplorer.Core.Dependencies;

namespace ClaudeExplorer.Core.Tests.Dependencies;

public class DependencyModelTests
{
    private static DependencyResult R(string name, DependencyStatusKind kind)
        => new(new DependencyRef(name, name, new[] { "hook:PreToolUse" }), new DependencyStatus(kind));

    [Fact]
    public void Report_counts_results_by_kind()
    {
        var report = new DependencyReport(new[]
        {
            R("node", DependencyStatusKind.Found),
            R("npx", DependencyStatusKind.Found),
            R("python3", DependencyStatusKind.Missing),
            R("mytool", DependencyStatusKind.Unverifiable),
        });

        Assert.Equal(2, report.Count(DependencyStatusKind.Found));
        Assert.Equal(1, report.Count(DependencyStatusKind.Missing));
        Assert.Equal(1, report.Count(DependencyStatusKind.Unverifiable));
    }

    [Fact]
    public void AllHealthy_is_false_when_anything_is_missing()
    {
        var healthy = new DependencyReport(new[] { R("node", DependencyStatusKind.Found), R("x", DependencyStatusKind.Unverifiable) });
        var broken = new DependencyReport(new[] { R("node", DependencyStatusKind.Found), R("python3", DependencyStatusKind.Missing) });

        Assert.True(healthy.AllHealthy);
        Assert.False(broken.AllHealthy);
    }

    [Fact]
    public void Status_carries_version_and_path()
    {
        var status = new DependencyStatus(DependencyStatusKind.Found, "v20.10.0", "/usr/bin/node");
        Assert.Equal("v20.10.0", status.Version);
        Assert.Equal("/usr/bin/node", status.Path);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter DependencyModelTests`
Expected: FAIL — model types don't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Dependencies/DependencyModel.cs`:
```csharp
namespace ClaudeExplorer.Core.Dependencies;

/// <summary>
/// A distinct executable the discovered config depends on. Deduped by <see cref="Name"/> across all
/// hooks/MCP servers; <see cref="ReferencedBy"/> lists each source (e.g. "hook:PreToolUse",
/// "mcp:playwright").
/// </summary>
public sealed record DependencyRef(
    string Name,
    string Raw,
    IReadOnlyList<string> ReferencedBy);

public enum DependencyStatusKind
{
    /// <summary>Resolved on PATH and version-probed (an allowlisted runtime).</summary>
    Found,
    /// <summary>Not resolvable on PATH.</summary>
    Missing,
    /// <summary>Present on PATH but not in the probe allowlist, so intentionally not executed.</summary>
    Unverifiable,
}

public sealed record DependencyStatus(
    DependencyStatusKind Kind,
    string? Version = null,
    string? Path = null);

public sealed record DependencyResult(DependencyRef Ref, DependencyStatus Status);

public sealed record DependencyReport(IReadOnlyList<DependencyResult> Results)
{
    public int Count(DependencyStatusKind kind) => Results.Count(r => r.Status.Kind == kind);

    /// <summary>True when nothing is outright missing (Unverifiable is not a failure).</summary>
    public bool AllHealthy => Results.All(r => r.Status.Kind != DependencyStatusKind.Missing);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter DependencyModelTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): dependency health domain model"
```

---

## Task 6: Minimal MCP server reader

**Files:**
- Create: `src/ClaudeExplorer.Core/Dependencies/McpServerReader.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Dependencies/McpServerReaderTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Dependencies/McpServerReaderTests.cs`:
```csharp
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Dependencies;

public class McpServerReaderTests
{
    [Fact]
    public void Reads_stdio_server_from_project_mcp_json_with_command_and_args()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/repo/.mcp.json",
                """{ "mcpServers": { "pw": { "command": "uvx", "args": ["playwright-mcp", "--headless"] } } }""");

        var servers = new McpServerReader(fs).Read("/home", "/repo");

        var pw = Assert.Single(servers);
        Assert.Equal("pw", pw.Name);
        Assert.Equal("uvx", pw.Command);
        Assert.Equal(new[] { "playwright-mcp", "--headless" }, pw.Args);
        Assert.Equal(ScopeKind.Project, pw.Scope);
    }

    [Fact]
    public void Url_server_has_null_command()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/repo/.mcp.json",
                """{ "mcpServers": { "remote": { "type": "sse", "url": "https://example.com/mcp" } } }""");

        var remote = Assert.Single(new McpServerReader(fs).Read("/home", "/repo"));
        Assert.Equal("remote", remote.Name);
        Assert.Null(remote.Command);
        Assert.Empty(remote.Args);
    }

    [Fact]
    public void Reads_mcpServers_block_from_settings_files_with_their_scope()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/home/.claude/settings.json",
                """{ "mcpServers": { "usersrv": { "command": "node", "args": ["server.js"] } } }""")
            .AddFile("/repo/.claude/settings.json",
                """{ "mcpServers": { "projsrv": { "command": "npx", "args": ["@x/mcp"] } } }""");

        var servers = new McpServerReader(fs).Read("/home", "/repo");

        Assert.Contains(servers, s => s.Name == "usersrv" && s.Command == "node" && s.Scope == ScopeKind.User);
        Assert.Contains(servers, s => s.Name == "projsrv" && s.Command == "npx" && s.Scope == ScopeKind.Project);
    }

    [Fact]
    public void Missing_and_malformed_sources_are_skipped()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("/repo/.mcp.json", "{ not valid json ");

        Assert.Empty(new McpServerReader(fs).Read("/home", "/repo"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter McpServerReaderTests`
Expected: FAIL — `McpServer`/`McpServerReader` don't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Dependencies/McpServerReader.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Discovery;
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Dependencies;

/// <summary>
/// An MCP server definition relevant to dependency health. Only stdio servers (those with a
/// <c>command</c>) carry an executable dependency; url/sse servers have <c>Command == null</c>.
/// </summary>
public sealed record McpServer(string Name, string? Command, IReadOnlyList<string> Args, ScopeKind Scope);

/// <summary>
/// Minimal reader for MCP server definitions: pulls the <c>mcpServers</c> object from the located
/// settings files and from a project-root <c>.mcp.json</c>. Malformed/missing sources are skipped.
/// (Full MCP/plugin parsing — including <c>~/.claude.json</c> — is a later phase.)
/// </summary>
public sealed class McpServerReader
{
    private static readonly JsonDocumentOptions DocOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IFileSystem _fs;

    public McpServerReader(IFileSystem fs) => _fs = fs;

    public IReadOnlyList<McpServer> Read(string userDir, string projectDir, string? enterprisePath = null)
    {
        var servers = new List<McpServer>();

        foreach (var file in new SettingsLocator(_fs).Locate(userDir, projectDir, enterprisePath))
            servers.AddRange(ReadFrom(TryParse(file.Path), file.Scope));

        var mcpJson = $"{projectDir}/.mcp.json";
        if (_fs.FileExists(mcpJson))
            servers.AddRange(ReadFrom(TryParse(mcpJson), ScopeKind.Project));

        return servers;
    }

    private JsonObject? TryParse(string path)
    {
        if (!_fs.FileExists(path)) return null;
        try { return JsonNode.Parse(_fs.ReadAllText(path), nodeOptions: null, documentOptions: DocOptions) as JsonObject; }
        catch (JsonException) { return null; }
    }

    private static IEnumerable<McpServer> ReadFrom(JsonObject? root, ScopeKind scope)
    {
        if (root?["mcpServers"] is not JsonObject servers) yield break;

        foreach (var (name, def) in servers)
        {
            if (def is not JsonObject obj) continue;
            var command = (string?)obj["command"];
            var args = obj["args"] is JsonArray arr
                ? arr.Select(a => (string?)a ?? "").Where(a => a.Length > 0).ToList()
                : new List<string>();
            yield return new McpServer(name, command, args, scope);
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter McpServerReaderTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): minimal MCP server reader"
```

---

## Task 7: Dependency extractor

**Files:**
- Create: `src/ClaudeExplorer.Core/Dependencies/DependencyExtractor.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Dependencies/DependencyExtractorTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Dependencies/DependencyExtractorTests.cs`:
```csharp
using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Tests.Dependencies;

public class DependencyExtractorTests
{
    private static EffectiveConfig HooksConfig(string @event, string hooksJson)
    {
        var setting = new EffectiveSetting(
            $"hooks.{@event}", MergeStrategy.ArrayConcat, JsonNode.Parse(hooksJson),
            Winner: null, Contributions: Array.Empty<SettingContribution>(), HasConflict: false);
        return new EffectiveConfig(new[] { setting });
    }

    [Fact]
    public void Extracts_runtimes_from_nested_hook_command_strings()
    {
        var config = HooksConfig("PreToolUse",
            """[ { "matcher": "Bash", "hooks": [ { "type": "command", "command": "npx -y eslint" } ] } ]""");

        var refs = new DependencyExtractor().Extract(config, Array.Empty<McpServer>());

        var npx = Assert.Single(refs);
        Assert.Equal("npx", npx.Name);
        Assert.Equal(new[] { "hook:PreToolUse" }, npx.ReferencedBy);
    }

    [Fact]
    public void Extracts_command_from_stdio_mcp_server_and_skips_url_servers()
    {
        var servers = new[]
        {
            new McpServer("pw", "uvx", new[] { "playwright-mcp" }, ScopeKind.Project),
            new McpServer("remote", null, Array.Empty<string>(), ScopeKind.Project),
        };

        var refs = new DependencyExtractor().Extract(new EffectiveConfig(Array.Empty<EffectiveSetting>()), servers);

        var uvx = Assert.Single(refs);
        Assert.Equal("uvx", uvx.Name);
        Assert.Equal("uvx playwright-mcp", uvx.Raw);
        Assert.Equal(new[] { "mcp:pw" }, uvx.ReferencedBy);
    }

    [Fact]
    public void Deduplicates_by_runtime_and_merges_sources_sorted()
    {
        var config = HooksConfig("PreToolUse",
            """[ { "hooks": [ { "command": "npx run-a" }, { "command": "npx run-b" } ] } ]""");
        var servers = new[] { new McpServer("srv", "npx", new[] { "@x/mcp" }, ScopeKind.Project) };

        var refs = new DependencyExtractor().Extract(config, servers);

        var npx = Assert.Single(refs);
        Assert.Equal("npx", npx.Name);
        Assert.Equal(new[] { "hook:PreToolUse", "mcp:srv" }, npx.ReferencedBy);
    }

    [Fact]
    public void Non_hook_settings_are_ignored()
    {
        var setting = new EffectiveSetting("model", MergeStrategy.ScalarLastWins,
            JsonValue.Create("opus"), null, Array.Empty<SettingContribution>(), false);
        var config = new EffectiveConfig(new[] { setting });

        Assert.Empty(new DependencyExtractor().Extract(config, Array.Empty<McpServer>()));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter DependencyExtractorTests`
Expected: FAIL — `DependencyExtractor` doesn't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Dependencies/DependencyExtractor.cs`:
```csharp
using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Dependencies;

/// <summary>
/// Turns discovered config into a deduped list of executable dependencies: the <c>command</c>
/// strings inside <c>hooks.*</c> settings, plus the <c>command</c> of each stdio MCP server.
/// Deduped by runtime name; each ref lists the distinct sources that referenced it.
/// </summary>
public sealed class DependencyExtractor
{
    private const string HookPrefix = "hooks.";

    public IReadOnlyList<DependencyRef> Extract(EffectiveConfig config, IReadOnlyList<McpServer> mcpServers)
    {
        var raw = new List<(string Name, string Raw, string Source)>();

        foreach (var setting in config.Settings)
        {
            if (!setting.Key.StartsWith(HookPrefix, StringComparison.Ordinal)) continue;
            var evt = setting.Key.Substring(HookPrefix.Length);
            foreach (var command in CollectCommands(setting.Value))
            {
                var exe = ExecutableExtractor.Extract(command);
                if (exe is not null) raw.Add((exe, command, $"hook:{evt}"));
            }
        }

        foreach (var server in mcpServers)
        {
            if (server.Command is null) continue;
            var exe = ExecutableExtractor.Extract(server.Command);
            if (exe is null) continue;
            var rawCmd = server.Args.Count > 0
                ? $"{server.Command} {string.Join(' ', server.Args)}"
                : server.Command;
            raw.Add((exe, rawCmd, $"mcp:{server.Name}"));
        }

        return raw
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new DependencyRef(
                Name: g.Key,
                Raw: g.First().Raw,
                ReferencedBy: g.Select(x => x.Source)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToList()))
            .OrderBy(d => d.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Recursively collects the value of every property literally named "command".</summary>
    private static IEnumerable<string> CollectCommands(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    if (key == "command" && value is JsonValue v
                        && v.TryGetValue<string>(out var s) && s.Length > 0)
                        yield return s;
                    else
                        foreach (var c in CollectCommands(value)) yield return c;
                }
                break;
            case JsonArray arr:
                foreach (var item in arr)
                    foreach (var c in CollectCommands(item)) yield return c;
                break;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter DependencyExtractorTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): extract executable dependencies from config"
```

---

## Task 8: Dependency checker (resolve + safe probe)

**Files:**
- Create: `src/ClaudeExplorer.Core/Dependencies/DependencyChecker.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Dependencies/DependencyCheckerTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Dependencies/DependencyCheckerTests.cs`:
```csharp
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Dependencies;

public class DependencyCheckerTests
{
    private static DependencyRef Ref(string name) => new(name, name, new[] { "hook:PreToolUse" });

    [Fact]
    public void Missing_when_not_on_path_and_runner_is_never_called()
    {
        var resolver = new FakePathResolver(); // nothing on PATH
        var runner = new FakeProcessRunner();

        var report = new DependencyChecker(resolver, runner).Check(new[] { Ref("node") });

        var result = Assert.Single(report.Results);
        Assert.Equal(DependencyStatusKind.Missing, result.Status.Kind);
        Assert.Null(result.Status.Path);
        Assert.Empty(runner.Invocations);
    }

    [Fact]
    public void Found_when_allowlisted_and_on_path_with_version_from_probe()
    {
        var resolver = new FakePathResolver().Add("node", "/usr/bin/node");
        var runner = new FakeProcessRunner().AddVersion("node", "v20.10.0");

        var report = new DependencyChecker(resolver, runner).Check(new[] { Ref("node") });

        var result = Assert.Single(report.Results);
        Assert.Equal(DependencyStatusKind.Found, result.Status.Kind);
        Assert.Equal("v20.10.0", result.Status.Version);
        Assert.Equal("/usr/bin/node", result.Status.Path);
        var call = Assert.Single(runner.Invocations);
        Assert.Equal("node", call.Executable);
        Assert.Equal(new[] { "--version" }, call.Arguments);
    }

    [Fact]
    public void Unverifiable_when_present_but_not_allowlisted_and_runner_is_never_called()
    {
        var resolver = new FakePathResolver().Add("my-tool", "/opt/bin/my-tool");
        var runner = new FakeProcessRunner();

        var report = new DependencyChecker(resolver, runner).Check(new[] { Ref("my-tool") });

        var result = Assert.Single(report.Results);
        Assert.Equal(DependencyStatusKind.Unverifiable, result.Status.Kind);
        Assert.Equal("/opt/bin/my-tool", result.Status.Path);
        Assert.Empty(runner.Invocations); // SAFETY: arbitrary binaries are never executed
    }

    [Fact]
    public void Version_falls_back_to_stderr_and_uses_first_nonempty_line()
    {
        var resolver = new FakePathResolver().Add("python3", "/usr/bin/python3");
        var runner = new FakeProcessRunner().AddResult("python3", new ProcessResult(0, "", "\nPython 3.11.5\n"));

        var report = new DependencyChecker(resolver, runner).Check(new[] { Ref("python3") });

        Assert.Equal("Python 3.11.5", Assert.Single(report.Results).Status.Version);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter DependencyCheckerTests`
Expected: FAIL — `DependencyChecker` doesn't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Dependencies/DependencyChecker.cs`:
```csharp
namespace ClaudeExplorer.Core.Dependencies;

/// <summary>
/// Classifies each <see cref="DependencyRef"/> as Found / Missing / Unverifiable.
/// Safety contract: a runtime is executed ONLY when it is on the allowlist, and ONLY with
/// <c>--version</c>. A discovered command is never run, and a present-but-non-allowlisted binary is
/// reported Unverifiable without being executed.
/// </summary>
public sealed class DependencyChecker
{
    private readonly IPathResolver _resolver;
    private readonly IProcessRunner _runner;

    public DependencyChecker(IPathResolver resolver, IProcessRunner runner)
    {
        _resolver = resolver;
        _runner = runner;
    }

    public DependencyReport Check(IReadOnlyList<DependencyRef> refs)
        => new(refs.Select(CheckOne).ToList());

    private DependencyResult CheckOne(DependencyRef dep)
    {
        var path = _resolver.Resolve(dep.Name);
        if (path is null)
            return new DependencyResult(dep, new DependencyStatus(DependencyStatusKind.Missing));

        if (!RuntimeAllowlist.IsAllowed(dep.Name))
            return new DependencyResult(dep, new DependencyStatus(DependencyStatusKind.Unverifiable, Path: path));

        // Allowlisted + present: the only case where we execute anything, and only `--version`.
        var probe = _runner.Run(dep.Name, RuntimeAllowlist.ProbeArguments);
        return new DependencyResult(dep,
            new DependencyStatus(DependencyStatusKind.Found, ParseVersion(probe), path));
    }

    private static string? ParseVersion(ProcessResult probe)
    {
        var text = !string.IsNullOrWhiteSpace(probe.StdOut) ? probe.StdOut : probe.StdErr;
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0) return trimmed;
        }
        return null;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter DependencyCheckerTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): dependency checker with safe allowlisted probing"
```

---

## Task 9: DependencyHealthService façade + integration

**Files:**
- Create: `src/ClaudeExplorer.Core/Dependencies/DependencyHealthService.cs`
- Test: `tests/ClaudeExplorer.Core.Tests/Dependencies/DependencyHealthServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ClaudeExplorer.Core.Tests/Dependencies/DependencyHealthServiceTests.cs`:
```csharp
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Dependencies;

public class DependencyHealthServiceTests
{
    [Fact]
    public void End_to_end_classifies_hook_and_mcp_dependencies()
    {
        var fs = new InMemoryFileSystem()
            // hooks: one npx command (present) and one python3 command (missing)
            .AddFile("/home/.claude/settings.json", """
                {
                  "hooks": {
                    "PreToolUse": [
                      { "matcher": "Bash", "hooks": [ { "type": "command", "command": "npx -y eslint" } ] },
                      { "matcher": "Edit", "hooks": [ { "type": "command", "command": "python3 -m guard" } ] }
                    ]
                  }
                }
                """)
            // MCP: a stdio uvx server (present) and an sse server (no command -> skipped)
            .AddFile("/repo/.mcp.json", """
                {
                  "mcpServers": {
                    "pw": { "command": "uvx", "args": ["playwright-mcp"] },
                    "remote": { "type": "sse", "url": "https://example.com/mcp" }
                  }
                }
                """);

        var resolver = new FakePathResolver()
            .Add("npx", "/usr/bin/npx")
            .Add("uvx", "/usr/bin/uvx"); // python3 intentionally absent
        var runner = new FakeProcessRunner()
            .AddVersion("npx", "10.2.0")
            .AddVersion("uvx", "uv 0.4.0");

        var report = new DependencyHealthService(fs, resolver, runner).Check("/home", "/repo");

        Assert.Equal(3, report.Results.Count); // npx, python3, uvx ("remote" skipped)
        Assert.Equal(2, report.Count(DependencyStatusKind.Found));
        Assert.Equal(1, report.Count(DependencyStatusKind.Missing));
        Assert.False(report.AllHealthy);

        var npx = report.Results.Single(r => r.Ref.Name == "npx");
        Assert.Equal(DependencyStatusKind.Found, npx.Status.Kind);
        Assert.Equal("10.2.0", npx.Status.Version);
        Assert.Equal(new[] { "hook:PreToolUse" }, npx.Ref.ReferencedBy);

        var python = report.Results.Single(r => r.Ref.Name == "python3");
        Assert.Equal(DependencyStatusKind.Missing, python.Status.Kind);

        var uvx = report.Results.Single(r => r.Ref.Name == "uvx");
        Assert.Equal(DependencyStatusKind.Found, uvx.Status.Kind);
        Assert.Equal(new[] { "mcp:pw" }, uvx.Ref.ReferencedBy);
    }

    [Fact]
    public void Empty_workspace_yields_empty_report()
    {
        var report = new DependencyHealthService(
                new InMemoryFileSystem(), new FakePathResolver(), new FakeProcessRunner())
            .Check("/home", "/repo");

        Assert.Empty(report.Results);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter DependencyHealthServiceTests`
Expected: FAIL — `DependencyHealthService` doesn't exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ClaudeExplorer.Core/Dependencies/DependencyHealthService.cs`:
```csharp
using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.Core.Dependencies;

/// <summary>
/// Top-level façade: compute the effective config + read MCP servers, extract executable
/// dependencies, and check each one safely. Answers "will this config actually work on this
/// machine, and what's broken?"
/// </summary>
public sealed class DependencyHealthService
{
    private readonly EffectiveConfigService _config;
    private readonly McpServerReader _mcp;
    private readonly DependencyExtractor _extractor;
    private readonly DependencyChecker _checker;

    public DependencyHealthService(IFileSystem fileSystem, IPathResolver resolver, IProcessRunner runner)
    {
        _config = new EffectiveConfigService(fileSystem);
        _mcp = new McpServerReader(fileSystem);
        _extractor = new DependencyExtractor();
        _checker = new DependencyChecker(resolver, runner);
    }

    public DependencyReport Check(string userDir, string projectDir, string? enterprisePath = null)
    {
        var config = _config.Compute(userDir, projectDir, enterprisePath);
        var servers = _mcp.Read(userDir, projectDir, enterprisePath);
        var refs = _extractor.Extract(config, servers);
        return _checker.Check(refs);
    }
}
```

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test`
Expected: PASS — all Phase 1 + Phase 2 + Phase 3 tests green (53 prior + the new Phase-3 tests).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): DependencyHealthService facade"
```

---

## Self-Review

**Spec coverage (roadmap Phase 3 deliverables):**
- Seam `IProcessRunner` (+ Physical + fake) → Task 1. ✓
- Seam `IPathResolver` (+ Physical + fake) → Task 2. ✓
- Executable extraction from hooks command strings → Task 7 (+ Task 4 parser). ✓
- Executable extraction from MCP `command`+`args`; minimal MCP reader added here → Tasks 6, 7. ✓
- Wrapper recognition (npx/uvx/uv/python/docker/podman/plain binaries) → Task 4 (first-token = runtime), tested. ✓
- Allowlist of safe runtimes for `--version` → Task 3. ✓
- `DependencyChecker` → resolve on PATH + allowlisted `--version` only; Found/Missing/Unverifiable → Task 8. ✓
- Models `DependencyRef(name, raw, referencedBy[])`, `DependencyStatus(kind, version?, path?)`, `DependencyReport` → Task 5. ✓
- Façade `DependencyHealthService` → Task 9. ✓
- **Hard rule** (allowlist + `--version` only; never execute the discovered command / arbitrary binary) → enforced in Task 8, asserted by the "runner never called" tests in Tasks 8 & the Unverifiable path. ✓
- Tests: wrapper extraction, allowlist enforcement, missing exe, fake runner/resolver → Tasks 4, 8, 9. ✓

**Deferred (noted, not forgotten):** `~/.claude.json` mcpServers; plugin-provided MCP servers; transitive runtimes (e.g. node behind npx); capturing the wrapped package name as metadata; probe flags other than `--version`; parallel/cached probing. These belong to later phases / the tech-debt issue (CLA-16).

**Placeholder scan:** none — every code step contains complete code; every run step has an exact command + expected result.

**Type consistency:** `ProcessResult`, `IProcessRunner`, `PhysicalProcessRunner`, `FakeProcessRunner` (with `AddVersion`/`AddResult`/`Invocations`), `IPathResolver`, `PhysicalPathResolver`, `FakePathResolver` (with `Add`/`Resolve`), `RuntimeAllowlist` (`IsAllowed`/`ProbeArguments`/`Names`), `ExecutableExtractor.Extract`, `DependencyRef(Name, Raw, ReferencedBy)`, `DependencyStatusKind` (`Found`/`Missing`/`Unverifiable`), `DependencyStatus(Kind, Version?, Path?)`, `DependencyResult(Ref, Status)`, `DependencyReport(Results)` (`Count`/`AllHealthy`), `McpServer(Name, Command, Args, Scope)`, `McpServerReader.Read`, `DependencyExtractor.Extract`, `DependencyChecker.Check`, `DependencyHealthService.Check` are used identically across all tasks and match the Phase-1 types they consume (`EffectiveConfig`, `EffectiveSetting`, `MergeStrategy`, `SettingContribution`, `ScopeKind`, `IFileSystem`, `SettingsLocator`, `EffectiveConfigService`).

---

## Execution Handoff

Plan saved to `docs/superpowers/plans/2026-06-07-03-dependency-health.md`. Execute via superpowers:subagent-driven-development (one implementer for the cohesive engine, then spec + code-quality review), then finishing-a-development-branch — per the playbook in `docs/superpowers/HANDOFF.md`.
