using ClaudeExplorer.App.Environments;

namespace ClaudeExplorer.App.Compare;

/// <summary>
/// App-wide, navigation-persistent selection of a compare pair (A and optional B). Lives as a DI
/// singleton so the A/B chosen on one artifact screen still applies on the next. Compare is OFF until
/// <see cref="EndpointB"/> is set; A defaults to the active environment's base. Screens read
/// <see cref="Comparison"/> for their own category and subscribe to <see cref="Changed"/>.
/// </summary>
public sealed class CompareContext
{
    private readonly EnvironmentService _environments;
    private readonly ProjectRegistry _projects;
    private readonly IEnvironmentCompareDataSource _source;

    private string? _aId;
    private string? _bId;
    private EnvironmentComparison? _comparison;

    public event Action? Changed;

    public CompareContext(EnvironmentService environments, ProjectRegistry projects, IEnvironmentCompareDataSource source)
    {
        _environments = environments;
        _projects = projects;
        _source = source;
        _environments.Changed += OnEndpointsChanged;
        _projects.Changed += OnEndpointsChanged;
    }

    /// <summary>All selectable endpoints: every environment base + every registered project.</summary>
    public IReadOnlyList<CompareEndpoint> Endpoints =>
        _environments.Environments.Select(e => CompareEndpoint.Base(e.Id, e.Name, e.UserDir))
            .Concat(_projects.All.Select(p => CompareEndpoint.Project(p.Id, p.Name, p.ProjectDir)))
            .ToList();

    public CompareEndpoint? EndpointA => Resolve(_aId) ?? DefaultA();
    public CompareEndpoint? EndpointB => Resolve(_bId);

    public bool IsComparing => EndpointA is not null && EndpointB is not null;

    public void SetA(string id) { _aId = id; Rebuild(); }

    public void SetB(string id) { _bId = id; Rebuild(); }

    public void ClearB() { _bId = null; _comparison = null; Changed?.Invoke(); }

    public void Swap()
    {
        if (EndpointA is null || EndpointB is null) return;
        (_aId, _bId) = (EndpointB.Id, EndpointA.Id);
        Rebuild();
    }

    /// <summary>The diff for one category (e.g. "Commands"), or null when compare is off.</summary>
    public CompareCategory? Comparison(string category) => _comparison?.Find(category);

    private CompareEndpoint? DefaultA()
    {
        var active = _environments.Active;
        return CompareEndpoint.Base(active.Id, active.Name, active.UserDir);
    }

    private CompareEndpoint? Resolve(string? id) =>
        id is null ? null : Endpoints.FirstOrDefault(e => e.Id == id);

    private void Rebuild()
    {
        var a = EndpointA;
        var b = EndpointB;
        _comparison = (a is not null && b is not null)
            ? EnvironmentComparer.Compare(_source.Snapshot(a), _source.Snapshot(b))
            : null;
        Changed?.Invoke();
    }

    // Endpoints changed (env added/removed, project added): if a selected id disappeared, drop it.
    private void OnEndpointsChanged()
    {
        if (_aId is not null && Resolve(_aId) is null) _aId = null;
        if (_bId is not null && Resolve(_bId) is null) _bId = null;
        Rebuild();
    }
}
