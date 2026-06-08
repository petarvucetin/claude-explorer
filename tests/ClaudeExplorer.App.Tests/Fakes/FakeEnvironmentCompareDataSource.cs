using ClaudeExplorer.App.Compare;
using ClaudeExplorer.App.Environments;
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.App.Tests.Fakes;

public sealed class FakeEnvironmentCompareDataSource : IEnvironmentCompareDataSource
{
    private static readonly EnvironmentSnapshot EmptySnap = new(
        Array.Empty<EffectiveSetting>(),
        new ArtifactCatalog(Array.Empty<ResolvedArtifact>()),
        Array.Empty<McpServer>(),
        Array.Empty<string>(),
        new DependencyReport(Array.Empty<DependencyResult>()),
        new Dictionary<string, string>());

    private readonly Dictionary<string, EnvironmentSnapshot> _byId = new(StringComparer.Ordinal);
    public FakeEnvironmentCompareDataSource Add(string envId, EnvironmentSnapshot snap) { _byId[envId] = snap; return this; }
    public EnvironmentSnapshot Snapshot(ClaudeEnvironment env) => _byId[env.Id];
    public EnvironmentSnapshot Snapshot(CompareEndpoint endpoint) =>
        _byId.TryGetValue(endpoint.Id, out var snap) ? snap : EmptySnap;
}
