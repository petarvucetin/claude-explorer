using ClaudeExplorer.App.Dashboard;
using ClaudeExplorer.App.Mvvm;
using ClaudeExplorer.App.Services;

namespace ClaudeExplorer.App.ViewModels;

/// <summary>App-chrome state: the project label and a few rolled-up counts for the rail badges,
/// computed from the same dashboard inputs.</summary>
public sealed class ShellViewModel : ObservableObject
{
    private readonly IDashboardDataSource _source;
    private readonly IWorkspaceContext _workspace;
    private int _commandsAndSkills;
    private bool _hasDependencyProblem;
    private bool _hasMcpProblem;

    public ShellViewModel(IDashboardDataSource source, IWorkspaceContext workspace)
    {
        _source = source;
        _workspace = workspace;
    }

    public string ProjectLabel => _workspace.ProjectLabel;
    public int CommandsAndSkills { get => _commandsAndSkills; private set => SetProperty(ref _commandsAndSkills, value); }
    public bool HasDependencyProblem { get => _hasDependencyProblem; private set => SetProperty(ref _hasDependencyProblem, value); }
    public bool HasMcpProblem { get => _hasMcpProblem; private set => SetProperty(ref _hasMcpProblem, value); }

    public void Load()
    {
        var data = DashboardComputer.Compute(_source.GetInputs());
        var stat = data.Stats;
        CommandsAndSkills = Value(stat, "Commands") + Value(stat, "Skills+Agents");
        HasDependencyProblem = stat.Single(s => s.Label == "Dependencies").Badge is not null;
        HasMcpProblem = stat.Single(s => s.Label == "MCP Servers").Badge is not null;
    }

    private static int Value(IReadOnlyList<StatCard> stats, string label)
        => stats.Single(s => s.Label == label).Value;
}
