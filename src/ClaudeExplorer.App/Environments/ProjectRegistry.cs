namespace ClaudeExplorer.App.Environments;

/// <summary>A user-registered project folder used as a Compare endpoint.</summary>
public sealed record ProjectEndpointDef(string Id, string Name, string EnvId, string ProjectDir);

/// <summary>
/// Owns the list of project folders the user added as Compare endpoints, persisted in the shared
/// <see cref="EnvironmentState"/> (field <c>ComparedProjects</c>). Independent of the per-environment
/// active project (<see cref="EnvironmentService"/>) — registering a Compare endpoint never repoints
/// the active workspace. Mirrors EnvironmentService's observable shape.
/// </summary>
public sealed class ProjectRegistry
{
    private readonly EnvironmentStore _store;
    private readonly List<ProjectEndpointDef> _projects = new();

    public event Action? Changed;

    public ProjectRegistry(EnvironmentStore store) => _store = store;

    public IReadOnlyList<ProjectEndpointDef> All => _projects;

    public void Load()
    {
        _projects.Clear();
        _projects.AddRange(_store.Load().ComparedProjects);
        Changed?.Invoke();
    }

    public void Add(string name, string envId, string projectDir)
    {
        var dir = projectDir.Replace('\\', '/').TrimEnd('/');
        var id = $"{envId}|{dir}";
        if (_projects.Any(p => p.Id == id)) return;
        _projects.Add(new ProjectEndpointDef(id, name, envId, dir));
        Persist();
        Changed?.Invoke();
    }

    public void Remove(string id)
    {
        if (_projects.RemoveAll(p => p.Id == id) > 0) { Persist(); Changed?.Invoke(); }
    }

    // Preserve the rest of EnvironmentState (active id, custom envs, active-project map) on save.
    private void Persist()
    {
        var s = _store.Load();
        _store.Save(new EnvironmentState
        {
            ActiveId = s.ActiveId,
            Custom = s.Custom,
            Projects = s.Projects,
            ComparedProjects = new List<ProjectEndpointDef>(_projects),
        });
    }
}
