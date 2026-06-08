# Phase 9 — Multi-Environment + Compare Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Let the app see multiple Claude environments (Windows + WSL distro(s) + custom roots) as a
selectable **active environment** across all existing screens, and add a **Compare** screen that diffs
two environments (user-global only) across Settings, Commands, Skills, Agents, MCP, Plugins, and
Dependencies.

**Architecture:** A `ClaudeEnvironment` model; tested discovery (`EnvironmentDiscovery` over an
`IWslLocator` seam + `IFileSystem`); an observable `EnvironmentService` holding the environment list +
active selection (persisted via `EnvironmentStore`); `IWorkspaceContext` re-implemented as a thin
adapter over the active environment so **every existing screen becomes environment-aware with no
change**; a pure `EnvironmentComparer` over per-environment snapshots; a `CompareViewModel` + Blueprint
`Compare.razor`. Follows the Phase 7/8 pattern (tested mapper/VM + logic-light view).

**Tech Stack:** .NET 10, Blazor, Photino.Blazor, MudBlazor, xUnit. Design spec:
`docs/superpowers/specs/2026-06-08-multi-environment-compare-sync-design.md`. Compare mockup:
`ux-explorations/10-blueprint-compare.html`.

**Verified facts (from the WSL spike — rely on these):**
- `wsl.exe -l -q` lists distro names but emits **UTF-16LE** → the captured stdout has interleaved `\0`
  bytes (and `\r`). Sanitize by removing `\0` and `\r`, splitting on `\n`, trimming, dropping blanks.
  On this machine it lists `Ubuntu`, `podman-machine-default`, `docker-desktop`.
- `wsl.exe -d <distro> -- sh -c 'wslpath -w "$HOME"'` returns the Windows path of the distro home, e.g.
  `\\wsl.localhost\Ubuntu\home\petar` (clean, no NULs — it's passthrough of a Linux command's stdout).
- **.NET reads WSL files over the UNC path EVEN when forward-slash-normalized**: `File.Exists` /
  `Directory.Exists` on `//wsl.localhost/Ubuntu/.../.claude/settings.json` return true. So the existing
  `PhysicalFileSystem`/`SettingsLocator` need **no changes** — feed them `UserDir =
  \\wsl.localhost\<distro>\home\<user>` and forward-slash path building just works.
- A distro is only an "environment" when `{home}/.claude` exists (this naturally excludes
  `docker-desktop`/`podman-machine-default`). On this machine Ubuntu has no `~/.claude` yet → it would
  appear only after the user creates it (or via manual add).

**Conventions:** records for models; pure static mappers tested over Core records; engine/`Physical*`/
process-touching impls are DI-only (not unit-tested); App tests live in `tests/ClaudeExplorer.App.Tests`
(global usings: System, Collections.Generic, IO, Linq, Threading(.Tasks), Xunit). Run `dotnet` via
PowerShell. Commit per task; `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`
trailer. Photino can't run headless — `dotnet build` + tests are the gates; visual fidelity via `/run`.

---

## Task 1: `ClaudeEnvironment` model

**Files:** Create `src/ClaudeExplorer.App/Environments/ClaudeEnvironment.cs`; Test
`tests/ClaudeExplorer.App.Tests/Environments/ClaudeEnvironmentTests.cs`.

- [ ] **Test first:**

```csharp
using ClaudeExplorer.App.Environments;

namespace ClaudeExplorer.App.Tests.Environments;

public class ClaudeEnvironmentTests
{
    [Fact]
    public void Windows_environment_carries_its_fields()
    {
        var env = new ClaudeEnvironment("windows", "Windows", EnvironmentKind.Windows, "C:/Users/p", null);
        Assert.Equal("windows", env.Id);
        Assert.Equal(EnvironmentKind.Windows, env.Kind);
        Assert.Null(env.ProjectDir);
    }

    [Fact]
    public void WithProject_returns_a_copy_with_the_project_set()
    {
        var env = new ClaudeEnvironment("wsl:Ubuntu", "WSL · Ubuntu", EnvironmentKind.Wsl, "//wsl.localhost/Ubuntu/home/p", null);
        var withProj = env with { ProjectDir = "/work/app" };
        Assert.Equal("/work/app", withProj.ProjectDir);
        Assert.Null(env.ProjectDir); // original unchanged (record immutability)
    }
}
```

- [ ] **Implement** `ClaudeEnvironment.cs`:

```csharp
namespace ClaudeExplorer.App.Environments;

/// <summary>How a Claude environment's config folder is reached.</summary>
public enum EnvironmentKind { Windows, Wsl, Custom }

/// <summary>A discoverable Claude config environment: a user-global <c>.claude</c> root, optionally
/// with its own active project. WSL roots use a <c>\\wsl.localhost\&lt;distro&gt;\…</c> UNC UserDir.</summary>
public sealed record ClaudeEnvironment(
    string Id,
    string Name,
    EnvironmentKind Kind,
    string UserDir,
    string? ProjectDir);
```

- [ ] `dotnet test tests/ClaudeExplorer.App.Tests` → green. Commit: `feat(app): ClaudeEnvironment model`

---

## Task 2: WSL locator (process seam + output sanitization)

**Files:** Create `src/ClaudeExplorer.App/Environments/IWslLocator.cs`,
`src/ClaudeExplorer.App/Environments/WslLocator.cs`; Test fake
`tests/ClaudeExplorer.App.Tests/Fakes/FakeWslLocator.cs`; Test
`tests/ClaudeExplorer.App.Tests/Environments/WslLocatorSanitizeTests.cs`.

- [ ] **Test first** (the sanitization helpers — the only unit-testable part; the process calls mirror
  `Physical*` and aren't tested):

```csharp
using ClaudeExplorer.App.Environments;

namespace ClaudeExplorer.App.Tests.Environments;

public class WslLocatorSanitizeTests
{
    [Fact]
    public void CleanLines_strips_utf16_nul_bytes_and_blanks()
    {
        // "Ubuntu\nDebian" as UTF-16LE captured as bytes → interleaved NULs + a trailing blank.
        var raw = "U\0b\0u\0n\0t\0u\0\r\0\n\0D\0e\0b\0i\0a\0n\0\r\0\n\0\r\0\n\0";

        var lines = WslLocator.CleanLines(raw);

        Assert.Equal(new[] { "Ubuntu", "Debian" }, lines);
    }

    [Fact]
    public void CleanLines_handles_plain_utf8_too()
    {
        Assert.Equal(new[] { "Ubuntu" }, WslLocator.CleanLines("Ubuntu\r\n"));
    }

    [Fact]
    public void CleanPath_trims_nul_cr_and_whitespace()
    {
        Assert.Equal(@"\\wsl.localhost\Ubuntu\home\p",
            WslLocator.CleanPath("\\\\wsl.localhost\\Ubuntu\\home\\p\r\n"));
        Assert.Null(WslLocator.CleanPath("   \r\n"));
        Assert.Null(WslLocator.CleanPath(null));
    }
}
```

- [ ] **Implement** `IWslLocator.cs`:

```csharp
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
```

- [ ] **Implement** `WslLocator.cs`:

```csharp
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
```

- [ ] **Implement** the test fake `FakeWslLocator.cs`:

```csharp
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
```

- [ ] `dotnet test` → green. Commit: `feat(app): WSL locator seam + UTF-16 output sanitization`

---

## Task 3: `EnvironmentDiscovery`

**Files:** Create `src/ClaudeExplorer.App/Environments/EnvironmentDiscovery.cs`; Test
`tests/ClaudeExplorer.App.Tests/Environments/EnvironmentDiscoveryTests.cs`.

- [ ] **Test first:**

```csharp
using ClaudeExplorer.App.Environments;
using ClaudeExplorer.App.Tests.Fakes;

namespace ClaudeExplorer.App.Tests.Environments;

public class EnvironmentDiscoveryTests
{
    [Fact]
    public void Always_includes_a_windows_environment()
    {
        var disc = new EnvironmentDiscovery(new InMemoryFileSystem(), new FakeWslLocator(), "C:/Users/p");

        var envs = disc.Discover();

        var win = Assert.Single(envs);
        Assert.Equal(EnvironmentKind.Windows, win.Kind);
        Assert.Equal("C:/Users/p", win.UserDir);
        Assert.Equal("windows", win.Id);
    }

    [Fact]
    public void Includes_a_wsl_distro_only_when_it_has_a_dotclaude_folder()
    {
        var fs = new InMemoryFileSystem()
            .AddFile("//wsl.localhost/Ubuntu/home/p/.claude/settings.json", "{}"); // Ubuntu has .claude
        var wsl = new FakeWslLocator()
            .AddDistro("Ubuntu", "//wsl.localhost/Ubuntu/home/p")
            .AddDistro("docker-desktop", "//wsl.localhost/docker-desktop/root"); // no .claude

        var envs = new EnvironmentDiscovery(fs, wsl, "C:/Users/p").Discover();

        Assert.Equal(2, envs.Count);
        var ubuntu = envs.Single(e => e.Kind == EnvironmentKind.Wsl);
        Assert.Equal("wsl:Ubuntu", ubuntu.Id);
        Assert.Equal("WSL · Ubuntu", ubuntu.Name);
        Assert.Equal("//wsl.localhost/Ubuntu/home/p", ubuntu.UserDir);
    }

    [Fact]
    public void Skips_distros_whose_home_cannot_be_resolved()
    {
        var wsl = new FakeWslLocator().AddDistro("Broken"); // no home registered → ResolveHome null

        var envs = new EnvironmentDiscovery(new InMemoryFileSystem(), wsl, "C:/Users/p").Discover();

        Assert.Single(envs); // just Windows
    }
}
```

- [ ] **Implement** `EnvironmentDiscovery.cs`:

```csharp
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
```

- [ ] `dotnet test` → green. Commit: `feat(app): environment discovery (Windows + WSL by .claude)`

---

## Task 4: `EnvironmentStore` (persistence)

**Files:** Create `src/ClaudeExplorer.App/Environments/EnvironmentStore.cs`; Test
`tests/ClaudeExplorer.App.Tests/Environments/EnvironmentStoreTests.cs`.

Persists, to a JSON file, the user-added **custom** environments, the **active** environment id, and a
**per-environment project** map. Tolerant read (missing/garbled → empty state).

- [ ] **Test first:**

```csharp
using ClaudeExplorer.App.Environments;
using ClaudeExplorer.App.Tests.Fakes;

namespace ClaudeExplorer.App.Tests.Environments;

public class EnvironmentStoreTests
{
    private const string Path = "/home/.claude/.claude-explorer/environments.json";

    [Fact]
    public void Round_trips_state()
    {
        var fs = new InMemoryFileSystem();
        var store = new EnvironmentStore(fs, fs, Path);

        store.Save(new EnvironmentState(
            ActiveId: "wsl:Ubuntu",
            Custom: new[] { new ClaudeEnvironment("custom:x", "My Root", EnvironmentKind.Custom, "D:/cfg", null) },
            Projects: new Dictionary<string, string> { ["windows"] = "/work/app" }));

        var loaded = store.Load();

        Assert.Equal("wsl:Ubuntu", loaded.ActiveId);
        Assert.Equal("custom:x", Assert.Single(loaded.Custom).Id);
        Assert.Equal("/work/app", loaded.Projects["windows"]);
    }

    [Fact]
    public void Load_returns_empty_state_when_file_missing()
    {
        var loaded = new EnvironmentStore(new InMemoryFileSystem(), new InMemoryFileSystem(), Path).Load();

        Assert.Null(loaded.ActiveId);
        Assert.Empty(loaded.Custom);
        Assert.Empty(loaded.Projects);
    }

    [Fact]
    public void Load_returns_empty_state_on_garbled_json()
    {
        var fs = new InMemoryFileSystem().AddFile(Path, "{ not json");

        Assert.Empty(new EnvironmentStore(fs, fs, Path).Load().Custom);
    }
}
```

- [ ] **Implement** `EnvironmentStore.cs`:

```csharp
using System.Text.Json;
using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.App.Environments;

/// <summary>Persisted UI state: the active environment id, user-added custom environments, and a
/// per-environment active-project map.</summary>
public sealed record EnvironmentState(
    string? ActiveId,
    IReadOnlyList<ClaudeEnvironment> Custom,
    IReadOnlyDictionary<string, string> Projects)
{
    public static EnvironmentState Empty { get; } =
        new(null, Array.Empty<ClaudeEnvironment>(), new Dictionary<string, string>());
}

/// <summary>Reads/writes <see cref="EnvironmentState"/> as JSON. Tolerant: missing or garbled file →
/// <see cref="EnvironmentState.Empty"/>.</summary>
public sealed class EnvironmentStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly IFileSystem _fs;
    private readonly IFileWriter _writer;
    private readonly string _path;

    public EnvironmentStore(IFileSystem fs, IFileWriter writer, string path)
    {
        _fs = fs;
        _writer = writer;
        _path = path;
    }

    public EnvironmentState Load()
    {
        if (!_fs.FileExists(_path)) return EnvironmentState.Empty;
        try
        {
            return JsonSerializer.Deserialize<EnvironmentState>(_fs.ReadAllText(_path), Options) ?? EnvironmentState.Empty;
        }
        catch (JsonException)
        {
            return EnvironmentState.Empty;
        }
    }

    public void Save(EnvironmentState state)
        => _writer.WriteAllText(_path, JsonSerializer.Serialize(state, Options));
}
```

- [ ] `dotnet test` → green. Commit: `feat(app): environment store (persist custom + active + projects)`

---

## Task 5: `EnvironmentService`

**Files:** Create `src/ClaudeExplorer.App/Environments/EnvironmentService.cs`; Test
`tests/ClaudeExplorer.App.Tests/Environments/EnvironmentServiceTests.cs`.

Observable. Composes discovery + custom (from store), tracks active env + per-env project, persists on
change, raises `Changed`.

- [ ] **Test first:**

```csharp
using ClaudeExplorer.App.Environments;
using ClaudeExplorer.App.Tests.Fakes;

namespace ClaudeExplorer.App.Tests.Environments;

public class EnvironmentServiceTests
{
    private const string StorePath = "/home/.claude/.claude-explorer/environments.json";

    private static EnvironmentService Build(InMemoryFileSystem fs, FakeWslLocator wsl)
        => new(new EnvironmentDiscovery(fs, wsl, "C:/Users/p"), new EnvironmentStore(fs, fs, StorePath));

    [Fact]
    public void Loads_discovered_plus_custom_and_defaults_active_to_first()
    {
        var fs = new InMemoryFileSystem()
            .AddFile(StorePath, "{\"ActiveId\":null,\"Custom\":[{\"Id\":\"custom:x\",\"Name\":\"X\",\"Kind\":2,\"UserDir\":\"D:/cfg\",\"ProjectDir\":null}],\"Projects\":{}}");
        var svc = Build(fs, new FakeWslLocator());
        svc.Load();

        Assert.Contains(svc.Environments, e => e.Id == "windows");
        Assert.Contains(svc.Environments, e => e.Id == "custom:x");
        Assert.Equal("windows", svc.Active.Id); // first discovered, no persisted active
    }

    [Fact]
    public void SetActive_changes_active_and_raises_Changed()
    {
        var fs = new InMemoryFileSystem();
        var svc = Build(fs, new FakeWslLocator());
        svc.Load();
        svc.AddCustom("D:/cfg", "X"); // gives a second env
        var raised = 0; svc.Changed += () => raised++;

        svc.SetActive(svc.Environments.Last().Id);

        Assert.Equal(svc.Environments.Last().Id, svc.Active.Id);
        Assert.True(raised > 0);
    }

    [Fact]
    public void SetProject_attaches_a_project_to_the_active_env_and_persists()
    {
        var fs = new InMemoryFileSystem();
        var svc = Build(fs, new FakeWslLocator());
        svc.Load();

        svc.SetProject(svc.Active.Id, "/work/app");

        Assert.Equal("/work/app", svc.Active.ProjectDir);
        Assert.True(fs.FileExists(StorePath)); // persisted
    }

    [Fact]
    public void AddCustom_adds_a_custom_environment()
    {
        var fs = new InMemoryFileSystem();
        var svc = Build(fs, new FakeWslLocator());
        svc.Load();

        svc.AddCustom("D:/cfg", "My Root");

        var added = svc.Environments.Single(e => e.Kind == EnvironmentKind.Custom);
        Assert.Equal("My Root", added.Name);
        Assert.Equal("D:/cfg", added.UserDir);
    }
}
```

- [ ] **Implement** `EnvironmentService.cs`:

```csharp
namespace ClaudeExplorer.App.Environments;

/// <summary>Observable owner of the environment list + the active environment (with its per-env
/// project). Combines discovered (Windows/WSL) and persisted custom environments; persists active +
/// custom + projects on every change and raises <see cref="Changed"/> for the UI to re-render.</summary>
public sealed class EnvironmentService
{
    private readonly EnvironmentDiscovery _discovery;
    private readonly EnvironmentStore _store;
    private readonly List<ClaudeEnvironment> _environments = new();
    private readonly Dictionary<string, string> _projects = new(StringComparer.Ordinal);
    private string _activeId = "";

    public event Action? Changed;

    public EnvironmentService(EnvironmentDiscovery discovery, EnvironmentStore store)
    {
        _discovery = discovery;
        _store = store;
    }

    public IReadOnlyList<ClaudeEnvironment> Environments => _environments;
    public ClaudeEnvironment Active =>
        _environments.FirstOrDefault(e => e.Id == _activeId) ?? _environments[0];

    /// <summary>Discover + load persisted state. Call once at startup (and on Refresh).</summary>
    public void Load()
    {
        var state = _store.Load();
        _environments.Clear();
        _environments.AddRange(_discovery.Discover());
        _environments.AddRange(state.Custom);

        _projects.Clear();
        foreach (var kv in state.Projects) _projects[kv.Key] = kv.Value;
        ApplyProjects();

        _activeId = state.ActiveId is not null && _environments.Any(e => e.Id == state.ActiveId)
            ? state.ActiveId
            : _environments[0].Id;

        Changed?.Invoke();
    }

    public void Refresh() => Load();

    public void SetActive(string id)
    {
        if (_environments.All(e => e.Id != id)) return;
        _activeId = id;
        Persist();
        Changed?.Invoke();
    }

    public void SetProject(string id, string? projectDir)
    {
        if (string.IsNullOrEmpty(projectDir)) _projects.Remove(id);
        else _projects[id] = projectDir;
        ApplyProjects();
        Persist();
        Changed?.Invoke();
    }

    public void AddCustom(string userDir, string name)
    {
        var normalized = userDir.Replace('\\', '/').TrimEnd('/');
        var id = $"custom:{normalized}";
        if (_environments.Any(e => e.Id == id)) return;
        _environments.Add(new ClaudeEnvironment(id, name, EnvironmentKind.Custom, normalized, null));
        Persist();
        Changed?.Invoke();
    }

    public void Remove(string id)
    {
        var env = _environments.FirstOrDefault(e => e.Id == id);
        if (env is null || env.Kind != EnvironmentKind.Custom) return; // only custom removable
        _environments.Remove(env);
        if (_activeId == id) _activeId = _environments[0].Id;
        Persist();
        Changed?.Invoke();
    }

    private void ApplyProjects()
    {
        for (int i = 0; i < _environments.Count; i++)
        {
            var e = _environments[i];
            _environments[i] = e with { ProjectDir = _projects.TryGetValue(e.Id, out var p) ? p : null };
        }
    }

    private void Persist()
        => _store.Save(new EnvironmentState(
            _activeId,
            _environments.Where(e => e.Kind == EnvironmentKind.Custom).Select(e => e with { ProjectDir = null }).ToList(),
            new Dictionary<string, string>(_projects)));
}
```

- [ ] `dotnet test` → green. Commit: `feat(app): EnvironmentService (active env, custom, per-env project)`

---

## Task 6: `IWorkspaceContext` adapter over the active environment

**Files:** Create `src/ClaudeExplorer.App/Services/ActiveEnvironmentWorkspaceContext.cs`; Modify
`src/ClaudeExplorer.App/Program.cs` (registration — done in Task 11); Test
`tests/ClaudeExplorer.App.Tests/Services/ActiveEnvironmentWorkspaceContextTests.cs`.

The existing `WorkspaceContext` (fixed) stays for tests/other uses; the DI registration switches to this
adapter so all screens follow the active environment.

- [ ] **Test first:**

```csharp
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
```

- [ ] **Implement** `ActiveEnvironmentWorkspaceContext.cs`:

```csharp
using ClaudeExplorer.App.Environments;

namespace ClaudeExplorer.App.Services;

/// <summary>Adapts the active <see cref="EnvironmentService"/> environment to the existing
/// <see cref="IWorkspaceContext"/> the screens depend on, so switching environment re-points every
/// screen with no per-screen change.</summary>
public sealed class ActiveEnvironmentWorkspaceContext : IWorkspaceContext
{
    private readonly EnvironmentService _service;

    public ActiveEnvironmentWorkspaceContext(EnvironmentService service) => _service = service;

    public string UserDir => _service.Active.UserDir;

    public string ProjectDir => _service.Active.ProjectDir ?? "";

    public string ProjectLabel
    {
        get
        {
            var env = _service.Active;
            if (string.IsNullOrEmpty(env.ProjectDir)) return env.Name;
            var dir = env.ProjectDir.Replace('\\', '/').TrimEnd('/');
            var i = dir.LastIndexOf('/');
            var seg = i >= 0 && i < dir.Length - 1 ? dir[(i + 1)..] : dir;
            return $"{env.Name} · {seg}";
        }
    }
}
```

- [ ] `dotnet test` → green. Commit: `feat(app): workspace context adapter over active environment`

---

## Task 7: Comparison model + `EnvironmentComparer` (pure)

**Files:** Create `src/ClaudeExplorer.App/Compare/CompareModels.cs`,
`src/ClaudeExplorer.App/Compare/EnvironmentSnapshot.cs`,
`src/ClaudeExplorer.App/Compare/EnvironmentComparer.cs`; Test
`tests/ClaudeExplorer.App.Tests/Compare/EnvironmentComparerTests.cs`.

**Derivation:** for each category, reduce each environment's Core data to a `key → display-value`
dictionary, then diff generically: both+equal → `Same`; both+differ → `Differs`; only-A → `OnlyA`;
only-B → `OnlyB`. Settings values are canonicalized so list/array keys compare as sets.

- [ ] **Test first:**

```csharp
using System.Text.Json.Nodes;
using ClaudeExplorer.App.Compare;
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.App.Tests.Compare;

public class EnvironmentComparerTests
{
    private static EffectiveSetting Setting(string key, JsonNode? value) =>
        new(key, MergeStrategy.ScalarLastWins, value, null, Array.Empty<SettingContribution>(), false);

    private static ResolvedArtifact Art(ArtifactKind kind, string name, string? summary) =>
        new(new DiscoveredArtifact(kind, name, summary, new ArtifactSource(ArtifactSourceKind.User), $"/{name}"),
            Array.Empty<DiscoveredArtifact>());

    private static EnvironmentSnapshot Snap(
        IReadOnlyList<EffectiveSetting>? settings = null,
        IReadOnlyList<ResolvedArtifact>? artifacts = null,
        IReadOnlyList<McpServer>? mcp = null,
        IReadOnlyList<string>? plugins = null,
        IReadOnlyList<DependencyResult>? deps = null) =>
        new(settings ?? Array.Empty<EffectiveSetting>(),
            new ArtifactCatalog(artifacts ?? Array.Empty<ResolvedArtifact>()),
            mcp ?? Array.Empty<McpServer>(),
            plugins ?? Array.Empty<string>(),
            new DependencyReport(deps ?? Array.Empty<DependencyResult>()));

    private static CompareCategory Cat(EnvironmentComparison c, string name) => c.Categories.Single(x => x.Name == name);

    [Fact]
    public void Settings_classifies_same_differs_onlyA_onlyB()
    {
        var a = Snap(settings: new[]
        {
            Setting("model", JsonValue.Create("opus")),
            Setting("outputStyle", JsonValue.Create("concise")),
            Setting("statusLine", JsonValue.Create("ccusage")), // only A
        });
        var b = Snap(settings: new[]
        {
            Setting("model", JsonValue.Create("sonnet")),        // differs
            Setting("outputStyle", JsonValue.Create("concise")), // same
            Setting("env.DOCKER_HOST", JsonValue.Create("x")),   // only B
        });

        var cat = Cat(EnvironmentComparer.Compare(a, b), "Settings");

        Assert.Equal(DiffStatus.Differs, cat.Rows.Single(r => r.Key == "model").Status);
        Assert.Equal(DiffStatus.Same, cat.Rows.Single(r => r.Key == "outputStyle").Status);
        Assert.Equal(DiffStatus.OnlyA, cat.Rows.Single(r => r.Key == "statusLine").Status);
        Assert.Equal(DiffStatus.OnlyB, cat.Rows.Single(r => r.Key == "env.DOCKER_HOST").Status);
        Assert.Equal(1, cat.Same);
        Assert.Equal(1, cat.Differs);
        Assert.Equal(1, cat.OnlyA);
        Assert.Equal(1, cat.OnlyB);
    }

    [Fact]
    public void Settings_list_values_compare_as_sets_regardless_of_order()
    {
        var a = Snap(settings: new[] { Setting("permissions.allow", new JsonArray("git", "npm")) });
        var b = Snap(settings: new[] { Setting("permissions.allow", new JsonArray("npm", "git")) });

        Assert.Equal(DiffStatus.Same, Cat(EnvironmentComparer.Compare(a, b), "Settings")
            .Rows.Single(r => r.Key == "permissions.allow").Status);
    }

    [Fact]
    public void Commands_skills_agents_compare_by_name_and_summary()
    {
        var a = Snap(artifacts: new[]
        {
            Art(ArtifactKind.Command, "deploy", "v1"),
            Art(ArtifactKind.Skill, "lint", "same"),
        });
        var b = Snap(artifacts: new[]
        {
            Art(ArtifactKind.Command, "deploy", "v2"), // differs by summary
            Art(ArtifactKind.Skill, "lint", "same"),   // same
            Art(ArtifactKind.Subagent, "review", "x"), // only B (Agents)
        });

        var c = EnvironmentComparer.Compare(a, b);
        Assert.Equal(DiffStatus.Differs, Cat(c, "Commands").Rows.Single(r => r.Key == "deploy").Status);
        Assert.Equal(DiffStatus.Same, Cat(c, "Skills").Rows.Single(r => r.Key == "lint").Status);
        Assert.Equal(DiffStatus.OnlyB, Cat(c, "Agents").Rows.Single(r => r.Key == "review").Status);
    }

    [Fact]
    public void Mcp_plugins_dependencies_categories_present_and_diffed()
    {
        var a = Snap(
            mcp: new[] { new McpServer("ctx7", "uvx", new[] { "ctx7" }, ScopeKind.User) },
            plugins: new[] { "linear" },
            deps: new[] { new DependencyResult(new DependencyRef("node", "node", Array.Empty<string>()), new DependencyStatus(DependencyStatusKind.Found)) });
        var b = Snap(
            mcp: new[] { new McpServer("ctx7", "npx", new[] { "ctx7" }, ScopeKind.User) }, // differs (command)
            plugins: new[] { "linear", "playwright" },                                      // playwright only B
            deps: new[] { new DependencyResult(new DependencyRef("node", "node", Array.Empty<string>()), new DependencyStatus(DependencyStatusKind.Missing)) }); // differs (status)

        var c = EnvironmentComparer.Compare(a, b);
        Assert.Equal(DiffStatus.Differs, Cat(c, "MCP").Rows.Single(r => r.Key == "ctx7").Status);
        Assert.Equal(DiffStatus.OnlyB, Cat(c, "Plugins").Rows.Single(r => r.Key == "playwright").Status);
        Assert.Equal(DiffStatus.Differs, Cat(c, "Dependencies").Rows.Single(r => r.Key == "node").Status);
    }

    [Fact]
    public void Produces_seven_categories()
    {
        var c = EnvironmentComparer.Compare(Snap(), Snap());
        Assert.Equal(new[] { "Settings", "Commands", "Skills", "Agents", "MCP", "Plugins", "Dependencies" },
            c.Categories.Select(x => x.Name).ToArray());
    }
}
```

- [ ] **Implement** `CompareModels.cs`:

```csharp
namespace ClaudeExplorer.App.Compare;

/// <summary>A is the left environment, B the right.</summary>
public enum DiffStatus { Same, Differs, OnlyA, OnlyB }

public sealed record CompareRow(string Key, DiffStatus Status, string? ValueA, string? ValueB);

public sealed record CompareCategory(string Name, IReadOnlyList<CompareRow> Rows)
{
    public int Same => Rows.Count(r => r.Status == DiffStatus.Same);
    public int Differs => Rows.Count(r => r.Status == DiffStatus.Differs);
    public int OnlyA => Rows.Count(r => r.Status == DiffStatus.OnlyA);
    public int OnlyB => Rows.Count(r => r.Status == DiffStatus.OnlyB);
}

public sealed record EnvironmentComparison(IReadOnlyList<CompareCategory> Categories)
{
    public CompareCategory? Find(string name) => Categories.FirstOrDefault(c => c.Name == name);
}
```

- [ ] **Implement** `EnvironmentSnapshot.cs`:

```csharp
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.App.Compare;

/// <summary>The user-global data read for one environment (no project overlay).</summary>
public sealed record EnvironmentSnapshot(
    IReadOnlyList<EffectiveSetting> Settings,
    ArtifactCatalog Artifacts,
    IReadOnlyList<McpServer> Mcp,
    IReadOnlyList<string> Plugins,
    DependencyReport Dependencies);
```

- [ ] **Implement** `EnvironmentComparer.cs`:

```csharp
using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.App.Compare;

/// <summary>Pure diff of two environment snapshots into per-category rows. No IO — tested by
/// constructing Core records directly.</summary>
public static class EnvironmentComparer
{
    public static EnvironmentComparison Compare(EnvironmentSnapshot a, EnvironmentSnapshot b)
        => new(new List<CompareCategory>
        {
            BuildCategory("Settings", SettingsMap(a), SettingsMap(b)),
            BuildCategory("Commands", ArtifactMap(a, ArtifactKind.Command), ArtifactMap(b, ArtifactKind.Command)),
            BuildCategory("Skills", ArtifactMap(a, ArtifactKind.Skill), ArtifactMap(b, ArtifactKind.Skill)),
            BuildCategory("Agents", ArtifactMap(a, ArtifactKind.Subagent), ArtifactMap(b, ArtifactKind.Subagent)),
            BuildCategory("MCP", McpMap(a), McpMap(b)),
            BuildCategory("Plugins", PluginMap(a), PluginMap(b)),
            BuildCategory("Dependencies", DepMap(a), DepMap(b)),
        });

    private static CompareCategory BuildCategory(
        string name, IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b)
    {
        var rows = new List<CompareRow>();
        foreach (var key in a.Keys.Union(b.Keys).OrderBy(k => k, StringComparer.Ordinal))
        {
            var hasA = a.TryGetValue(key, out var va);
            var hasB = b.TryGetValue(key, out var vb);
            var status = (hasA, hasB) switch
            {
                (true, true) => va == vb ? DiffStatus.Same : DiffStatus.Differs,
                (true, false) => DiffStatus.OnlyA,
                _ => DiffStatus.OnlyB,
            };
            rows.Add(new CompareRow(key, status, hasA ? va : null, hasB ? vb : null));
        }
        return new CompareCategory(name, rows);
    }

    private static Dictionary<string, string> SettingsMap(EnvironmentSnapshot s)
        => s.Settings.ToDictionary(x => x.Key, x => Canonical(x.Value), StringComparer.Ordinal);

    private static Dictionary<string, string> ArtifactMap(EnvironmentSnapshot s, ArtifactKind kind)
        => s.Artifacts.OfKind(kind).ToDictionary(a => a.Winner.Name, a => a.Winner.Summary ?? "", StringComparer.Ordinal);

    private static Dictionary<string, string> McpMap(EnvironmentSnapshot s)
        => s.Mcp.GroupBy(m => m.Name, StringComparer.Ordinal)
               .ToDictionary(g => g.Key, g => $"{g.First().Command} {string.Join(" ", g.First().Args)}".Trim(), StringComparer.Ordinal);

    private static Dictionary<string, string> PluginMap(EnvironmentSnapshot s)
        => s.Plugins.Distinct(StringComparer.Ordinal).ToDictionary(p => p, _ => "installed", StringComparer.Ordinal);

    private static Dictionary<string, string> DepMap(EnvironmentSnapshot s)
        => s.Dependencies.Results.GroupBy(r => r.Ref.Name, StringComparer.Ordinal)
               .ToDictionary(g => g.Key, g => g.First().Status.Kind.ToString(), StringComparer.Ordinal);

    /// <summary>Canonical comparable form of a setting value; arrays compare as sorted sets.</summary>
    private static string Canonical(JsonNode? node)
    {
        if (node is null) return "";
        if (node is JsonArray arr)
            return "[" + string.Join(",", arr.Select(e => e?.ToJsonString() ?? "null").OrderBy(x => x, StringComparer.Ordinal)) + "]";
        return node.ToJsonString();
    }
}
```

- [ ] `dotnet test` → green. Commit: `feat(app): environment comparer (7-category diff, pure + tested)`

---

## Task 8: Compare data source seam

**Files:** Create `src/ClaudeExplorer.App/Compare/IEnvironmentCompareDataSource.cs`,
`src/ClaudeExplorer.App/Compare/EngineEnvironmentCompareDataSource.cs`; Test fake
`tests/ClaudeExplorer.App.Tests/Fakes/FakeEnvironmentCompareDataSource.cs`.

- [ ] **Implement** `IEnvironmentCompareDataSource.cs`:

```csharp
using ClaudeExplorer.App.Environments;

namespace ClaudeExplorer.App.Compare;

/// <summary>Reads one environment's user-global snapshot (no project). Engine impl is not unit-tested;
/// the view model is tested against a fake.</summary>
public interface IEnvironmentCompareDataSource
{
    EnvironmentSnapshot Snapshot(ClaudeEnvironment env);
}
```

- [ ] **Implement** `EngineEnvironmentCompareDataSource.cs` (engine impl; not unit-tested):

```csharp
using ClaudeExplorer.App.Environments;
using ClaudeExplorer.Core;
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Recommendations;

namespace ClaudeExplorer.App.Compare;

public sealed class EngineEnvironmentCompareDataSource : IEnvironmentCompareDataSource
{
    private readonly EffectiveConfigService _config;
    private readonly ArtifactCatalogService _artifacts;
    private readonly McpServerReader _mcp;
    private readonly InstalledPluginsReader _plugins;
    private readonly DependencyHealthService _health;

    public EngineEnvironmentCompareDataSource(
        EffectiveConfigService config, ArtifactCatalogService artifacts, McpServerReader mcp,
        InstalledPluginsReader plugins, DependencyHealthService health)
    {
        _config = config;
        _artifacts = artifacts;
        _mcp = mcp;
        _plugins = plugins;
        _health = health;
    }

    public EnvironmentSnapshot Snapshot(ClaudeEnvironment env)
    {
        var user = env.UserDir;
        const string noProject = "";
        return new EnvironmentSnapshot(
            _config.Compute(user, noProject).Settings,
            _artifacts.Build(user, noProject),
            _mcp.Read(user, noProject),
            _plugins.Read(user).ToList(), // InstalledPluginsReader.Read returns IReadOnlySet<string>
            _health.Check(user, noProject));
    }
}
```

- [ ] **Implement** the fake `FakeEnvironmentCompareDataSource.cs`:

```csharp
using ClaudeExplorer.App.Compare;
using ClaudeExplorer.App.Environments;

namespace ClaudeExplorer.App.Tests.Fakes;

public sealed class FakeEnvironmentCompareDataSource : IEnvironmentCompareDataSource
{
    private readonly Dictionary<string, EnvironmentSnapshot> _byId = new(StringComparer.Ordinal);
    public FakeEnvironmentCompareDataSource Add(string envId, EnvironmentSnapshot snap) { _byId[envId] = snap; return this; }
    public EnvironmentSnapshot Snapshot(ClaudeEnvironment env) => _byId[env.Id];
}
```

> Confirmed: `InstalledPluginsReader.Read(userDir)` returns `IReadOnlySet<string>` (plugin names from
> the on-disk plugin cache); the snapshot stores `IReadOnlyList<string>`, hence the `.ToList()` above.

- [ ] `dotnet build` → clean. Commit: `feat(app): compare data source seam (engine snapshot per env)`

---

## Task 9: `CompareViewModel`

**Files:** Create `src/ClaudeExplorer.App/Compare/CompareViewModel.cs`; Test
`tests/ClaudeExplorer.App.Tests/Compare/CompareViewModelTests.cs`.

Selects a left + right environment (default: active env vs the first other env), loads both snapshots,
runs the comparer, exposes the comparison + a selected category. Mirrors `DashboardViewModel`
(ObservableObject, IsLoading, ErrorMessage, try/catch Load).

- [ ] **Test first:**

```csharp
using System.Text.Json.Nodes;
using ClaudeExplorer.App.Compare;
using ClaudeExplorer.App.Environments;
using ClaudeExplorer.App.Tests.Fakes;
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.App.Tests.Compare;

public class CompareViewModelTests
{
    private static EnvironmentSnapshot Snap(string model) => new(
        new[] { new EffectiveSetting("model", MergeStrategy.ScalarLastWins, JsonValue.Create(model), null, Array.Empty<SettingContribution>(), false) },
        new ArtifactCatalog(Array.Empty<ResolvedArtifact>()),
        Array.Empty<McpServer>(), Array.Empty<string>(), new DependencyReport(Array.Empty<DependencyResult>()));

    private static EnvironmentService TwoEnvService(InMemoryFileSystem fs)
    {
        var svc = new EnvironmentService(new EnvironmentDiscovery(fs, new FakeWslLocator(), "C:/Users/p"),
                                         new EnvironmentStore(fs, fs, "/s.json"));
        svc.Load();
        svc.AddCustom("D:/wsl", "WSL · Ubuntu");
        return svc;
    }

    [Fact]
    public void Load_compares_the_two_selected_environments()
    {
        var fs = new InMemoryFileSystem();
        var svc = TwoEnvService(fs);
        var win = svc.Environments[0];
        var other = svc.Environments.Last();
        var source = new FakeEnvironmentCompareDataSource()
            .Add(win.Id, Snap("opus"))
            .Add(other.Id, Snap("sonnet"));
        var vm = new CompareViewModel(svc, source);

        vm.Load();

        Assert.False(vm.IsLoading);
        Assert.NotNull(vm.Comparison);
        var settings = vm.Comparison!.Find("Settings")!;
        Assert.Equal(DiffStatus.Differs, settings.Rows.Single(r => r.Key == "model").Status);
        Assert.Equal("Settings", vm.SelectedCategory!.Name); // defaults to first category
    }

    [Fact]
    public void SelectCategory_changes_the_visible_category()
    {
        var fs = new InMemoryFileSystem();
        var svc = TwoEnvService(fs);
        var source = new FakeEnvironmentCompareDataSource()
            .Add(svc.Environments[0].Id, Snap("opus"))
            .Add(svc.Environments.Last().Id, Snap("opus"));
        var vm = new CompareViewModel(svc, source);
        vm.Load();

        vm.SelectCategory("MCP");

        Assert.Equal("MCP", vm.SelectedCategory!.Name);
    }
}
```

- [ ] **Implement** `CompareViewModel.cs`:

```csharp
using ClaudeExplorer.App.Environments;
using ClaudeExplorer.App.Mvvm;

namespace ClaudeExplorer.App.Compare;

/// <summary>Drives the Compare screen: pick left/right environments, snapshot both, diff, expose the
/// comparison + the selected category. View binds to <see cref="Comparison"/> / <see cref="LeftEnv"/> /
/// <see cref="RightEnv"/> / <see cref="SelectedCategory"/>.</summary>
public sealed class CompareViewModel : ObservableObject
{
    private readonly EnvironmentService _environments;
    private readonly IEnvironmentCompareDataSource _source;

    private ClaudeEnvironment? _left;
    private ClaudeEnvironment? _right;
    private EnvironmentComparison? _comparison;
    private CompareCategory? _selected;
    private bool _isLoading;
    private string? _error;

    public CompareViewModel(EnvironmentService environments, IEnvironmentCompareDataSource source)
    {
        _environments = environments;
        _source = source;
    }

    public IReadOnlyList<ClaudeEnvironment> Environments => _environments.Environments;
    public ClaudeEnvironment? LeftEnv { get => _left; private set => SetProperty(ref _left, value); }
    public ClaudeEnvironment? RightEnv { get => _right; private set => SetProperty(ref _right, value); }
    public EnvironmentComparison? Comparison { get => _comparison; private set => SetProperty(ref _comparison, value); }
    public CompareCategory? SelectedCategory { get => _selected; private set => SetProperty(ref _selected, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string? ErrorMessage { get => _error; private set => SetProperty(ref _error, value); }

    public void SetEnvironments(string leftId, string rightId)
    {
        LeftEnv = _environments.Environments.FirstOrDefault(e => e.Id == leftId);
        RightEnv = _environments.Environments.FirstOrDefault(e => e.Id == rightId);
        Load();
    }

    public void SelectCategory(string name)
        => SelectedCategory = Comparison?.Find(name) ?? SelectedCategory;

    public void Load()
    {
        IsLoading = true;
        try
        {
            var envs = _environments.Environments;
            LeftEnv ??= envs.FirstOrDefault();
            RightEnv ??= envs.Skip(1).FirstOrDefault() ?? LeftEnv;
            if (LeftEnv is null || RightEnv is null)
            {
                ErrorMessage = "Need two environments to compare.";
                Comparison = null;
                return;
            }
            ErrorMessage = null;
            Comparison = EnvironmentComparer.Compare(_source.Snapshot(LeftEnv), _source.Snapshot(RightEnv));
            SelectedCategory = Comparison.Categories.FirstOrDefault();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
```

- [ ] `dotnet test` → green. Commit: `feat(app): CompareViewModel (select envs, snapshot, diff)`

---

## Task 10: Views — environment selector, Compare screen, rail

**Files:** Create `src/ClaudeExplorer.App/Pages/Compare.razor`,
`src/ClaudeExplorer.App/Components/EnvironmentSelector.razor`; Modify
`src/ClaudeExplorer.App/Components/TopBar.razor`, `src/ClaudeExplorer.App/Components/LeftRail.razor`,
`src/ClaudeExplorer.App/Layout/MainLayout.razor`, `src/ClaudeExplorer.App/wwwroot/css/blueprint.css`,
`_Imports.razor`. Visual source of truth: `ux-explorations/10-blueprint-compare.html` (port its
`.envchip`, `.cats`, `.summary`, `.scount`, table/`.stat`/row-accent CSS into `blueprint.css`).

- [ ] **`EnvironmentSelector.razor`** — top-bar control bound to `EnvironmentService`: shows the active
  environment as an `.envchip` (color by `Kind`), a dropdown to switch active (calls
  `Service.SetActive`), an "Add environment…" item (prompts a path → `Service.AddCustom`), and a
  "Refresh" that calls `Service.Refresh`. Inject `EnvironmentService`; subscribe to `Changed` →
  `StateHasChanged`; `IDisposable` unsubscribe.

- [ ] **`TopBar.razor`** — replace the static project chip with `<EnvironmentSelector />`. Keep brand +
  coord + the existing refresh button (which calls `RefreshService.Request()`).

- [ ] **`LeftRail.razor`** — add an "Analyze" section label and a **Compare** `NavLink` to `/compare`
  (icon as in the mockup). Optionally render the environment list in the `.foot` block from
  `EnvironmentService.Environments`. (Inject `EnvironmentService` if showing the list; dispose handler.)

- [ ] **`MainLayout.razor`** — on init, ensure `EnvironmentService.Load()` has run once (call it if
  `Environments` is empty) so the selector + screens have data; subscribe to `EnvironmentService.Changed`
  → `RefreshService.Request()` so switching environment reloads all screens. Unsubscribe in `Dispose`.

- [ ] **`Compare.razor`** (`@page "/compare"`): inject `CompareViewModel`; on init `Vm.Load()`,
  subscribe `PropertyChanged` + `RefreshService.Requested`, `IDisposable` unsubscribe (mirror
  `Dashboard.razor`). Render: pagehead "Environment Compare" + the two env names; category tabs from
  `Vm.Comparison.Categories` (each shows name + a small differ/only count), clicking calls
  `Vm.SelectCategory`; a summary bar of `SelectedCategory` counts (same/differs/onlyA/onlyB using
  `Pill`); the diff table `Key | <LeftEnv.Name> | <RightEnv.Name> | Status` over
  `SelectedCategory.Rows` — `ValueA`/`ValueB` in `code`, absent side shown as "— not set —", row accent
  + a status `Pill` per `DiffStatus`. Show `ErrorMessage` when set.

- [ ] `dotnet build` → clean. Commit: `feat(app): environment selector + Compare screen (Blueprint)`

---

## Task 11: DI + navigation wiring

**Files:** Modify `src/ClaudeExplorer.App/Program.cs`, `_Imports.razor`.

- [ ] In `Program.cs`, register the new services and switch the workspace context. After the existing
  Core-seam registrations, add:

```csharp
        // Environments.
        builder.Services.AddSingleton<IWslLocator>(sp => new WslLocator(sp.GetRequiredService<IProcessRunner>()));
        var winHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        builder.Services.AddSingleton(sp => new EnvironmentDiscovery(
            sp.GetRequiredService<IFileSystem>(), sp.GetRequiredService<IWslLocator>(), winHome));
        builder.Services.AddSingleton(sp => new EnvironmentStore(
            sp.GetRequiredService<IFileSystem>(), sp.GetRequiredService<IFileWriter>(),
            $"{winHome.Replace('\\', '/')}/.claude/.claude-explorer/environments.json"));
        builder.Services.AddSingleton(sp =>
        {
            var svc = new EnvironmentService(sp.GetRequiredService<EnvironmentDiscovery>(), sp.GetRequiredService<EnvironmentStore>());
            svc.Load();
            return svc;
        });

        // Compare.
        builder.Services.AddSingleton<IEnvironmentCompareDataSource, EngineEnvironmentCompareDataSource>();
        builder.Services.AddTransient<CompareViewModel>();
        builder.Services.AddSingleton(sp => new InstalledPluginsReader(sp.GetRequiredService<IFileSystem>()));
```

  And **replace** the existing `IWorkspaceContext` registration (the `new WorkspaceContext(home, project)`
  line from the prior phase) with:

```csharp
        builder.Services.AddSingleton<IWorkspaceContext>(sp =>
            new ActiveEnvironmentWorkspaceContext(sp.GetRequiredService<EnvironmentService>()));
```

  (Remove the now-unused `WorkspaceResolver.ResolveProjectDir(...)` call + `project` local for the old
  registration; `WorkspaceResolver` stays in the codebase for future "open project" wiring.)

  Add `using ClaudeExplorer.App.Environments;` and `using ClaudeExplorer.App.Compare;` to `Program.cs`.

- [ ] Add `@using ClaudeExplorer.App.Environments` and `@using ClaudeExplorer.App.Compare` to
  `_Imports.razor`.

- [ ] `dotnet build` + `dotnet test` → all green. Commit: `feat(app): wire environments + compare into DI/nav`

---

## Task 12: Docs

**Files:** Modify `docs/superpowers/plans/2026-06-07-00-roadmap.md`, `docs/superpowers/HANDOFF.md`.

- [ ] Add a Phase 9 row (Done after merge); update the test count; record the multi-environment +
  compare architecture and that **Phase 10 — environment settings sync** is next (it consumes the
  Compare Settings rows + the Phase-6 safe-mutation layer). Note artifact/MCP/plugin file-sync still
  deferred.
- [ ] Commit: `docs: mark Phase 9 (multi-environment + compare) done; next Phase 10`

---

## Self-Review

**Spec coverage:** environment model (T1); discovery Windows+WSL+custom (T2 locator, T3 discovery, T5
custom); persistence (T4); active env + per-env project + observable (T5); `IWorkspaceContext` adapter →
all screens env-aware (T6); compare across all 7 categories, pure+tested (T7); engine snapshot (T8);
compare VM (T9); env selector + Compare view + rail (T10); DI/nav incl. workspace swap (T11); docs +
Phase-10 handoff (T12). Sync is explicitly Phase 10 (not here). ✅

**Placeholder scan:** complete code for all C# tasks + tests; T10 view tasks reference the in-repo
mockup + the `Dashboard.razor` exemplar with precise binding instructions (concrete source, not a
placeholder); one verification note on `InstalledPluginsReader.Read` signature. ✅

**Type consistency:** `ClaudeEnvironment`/`EnvironmentKind`, `IWslLocator`/`WslLocator.CleanLines/CleanPath`,
`EnvironmentDiscovery.Discover`, `EnvironmentState`/`EnvironmentStore.Load/Save`, `EnvironmentService`
(`Environments/Active/Load/Refresh/SetActive/SetProject/AddCustom/Remove/Changed`),
`ActiveEnvironmentWorkspaceContext`, `DiffStatus/CompareRow/CompareCategory/EnvironmentComparison`,
`EnvironmentSnapshot`, `EnvironmentComparer.Compare`, `IEnvironmentCompareDataSource.Snapshot`,
`CompareViewModel` (`Load/SetEnvironments/SelectCategory/Comparison/SelectedCategory/IsLoading/ErrorMessage`)
— consistent across tasks. Core signatures match those used in Phases 7–8. ✅

**Test isolation:** discovery/store/service/comparer/VM tested via `InMemoryFileSystem` + `FakeWslLocator`
+ `FakeEnvironmentCompareDataSource` (constructing Core records); `WslLocator`/`EngineEnvironmentCompareDataSource`/
env-based registrations are DI-only. No real machine in tests; the WSL/UNC behavior was validated by the
documented spike. ✅
