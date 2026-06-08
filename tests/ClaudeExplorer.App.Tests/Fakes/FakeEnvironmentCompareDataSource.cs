using ClaudeExplorer.App.Compare;
using ClaudeExplorer.App.Environments;

namespace ClaudeExplorer.App.Tests.Fakes;

public sealed class FakeEnvironmentCompareDataSource : IEnvironmentCompareDataSource
{
    private readonly Dictionary<string, EnvironmentSnapshot> _byId = new(StringComparer.Ordinal);
    public FakeEnvironmentCompareDataSource Add(string envId, EnvironmentSnapshot snap) { _byId[envId] = snap; return this; }
    public EnvironmentSnapshot Snapshot(ClaudeEnvironment env) => _byId[env.Id];
}
