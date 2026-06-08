using ClaudeExplorer.App.Environments;

namespace ClaudeExplorer.App.Compare;

/// <summary>Reads one environment's user-global snapshot (no project). Engine impl is not unit-tested;
/// the view model is tested against a fake.</summary>
public interface IEnvironmentCompareDataSource
{
    EnvironmentSnapshot Snapshot(ClaudeEnvironment env);
}
