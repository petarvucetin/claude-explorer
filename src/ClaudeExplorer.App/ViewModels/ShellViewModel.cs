using ClaudeExplorer.App.Dashboard;
using ClaudeExplorer.App.Mvvm;
using ClaudeExplorer.App.Services;
using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Plugins;

namespace ClaudeExplorer.App.ViewModels;

/// <summary>App-chrome state: the project label and per-type counts for the rail badges, computed
/// from the dashboard inputs (artifacts/MCP) plus the plugin inventory.</summary>
public sealed class ShellViewModel : ObservableObject
{
    private readonly IDashboardDataSource _source;
    private readonly PluginInventoryReader _plugins;
    private readonly IWorkspaceContext _workspace;

    private int _commands, _skills, _subagents, _mcp, _pluginCount;
    private bool _hasDependencyProblem;
    private bool _hasMcpProblem;

    public ShellViewModel(IDashboardDataSource source, PluginInventoryReader plugins, IWorkspaceContext workspace)
    {
        _source = source;
        _plugins = plugins;
        _workspace = workspace;
    }

    public string ProjectLabel => _workspace.ProjectLabel;
    public int Commands { get => _commands; private set => SetProperty(ref _commands, value); }
    public int Skills { get => _skills; private set => SetProperty(ref _skills, value); }
    public int Subagents { get => _subagents; private set => SetProperty(ref _subagents, value); }
    public int Mcp { get => _mcp; private set => SetProperty(ref _mcp, value); }
    public int Plugins { get => _pluginCount; private set => SetProperty(ref _pluginCount, value); }
    public bool HasDependencyProblem { get => _hasDependencyProblem; private set => SetProperty(ref _hasDependencyProblem, value); }
    public bool HasMcpProblem { get => _hasMcpProblem; private set => SetProperty(ref _hasMcpProblem, value); }

    public void Load()
    {
        var inputs = _source.GetInputs();

        Commands = inputs.Artifacts.OfKind(ArtifactKind.Command).Count();
        Skills = inputs.Artifacts.OfKind(ArtifactKind.Skill).Count();
        Subagents = inputs.Artifacts.OfKind(ArtifactKind.Subagent).Count();
        Mcp = inputs.McpServers.Count;
        Plugins = SafePluginCount();

        var stat = DashboardComputer.Compute(inputs).Stats;
        HasDependencyProblem = stat.FirstOrDefault(s => s.Label == DashboardComputer.Dependencies)?.Badge is not null;
        HasMcpProblem = stat.FirstOrDefault(s => s.Label == DashboardComputer.McpServers)?.Badge is not null;
    }

    private int SafePluginCount()
    {
        try { return _plugins.Read(_workspace.UserDir).Plugins.Count; }
        catch { return 0; }
    }
}
