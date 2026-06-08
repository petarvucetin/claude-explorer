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
