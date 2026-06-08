using ClaudeExplorer.App.Mvvm;
using ClaudeExplorer.App.Services;
using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Mcp;

namespace ClaudeExplorer.App.Screens.Mcp;

/// <summary>Loads MCP servers (<see cref="McpInventoryReader"/>) for the active workspace, joins them
/// with dependency health, and exposes rows + a selected row for the detail panel.</summary>
public sealed class McpViewModel : ObservableObject
{
    private readonly McpInventoryReader _reader;
    private readonly DependencyHealthService _health;
    private readonly IWorkspaceContext _workspace;

    private McpView? _view;
    private McpRow? _selected;
    private bool _isLoading;
    private string? _errorMessage;

    public McpViewModel(McpInventoryReader reader, DependencyHealthService health, IWorkspaceContext workspace)
    {
        _reader = reader;
        _health = health;
        _workspace = workspace;
    }

    public McpView? View { get => _view; private set => SetProperty(ref _view, value); }
    public McpRow? Selected { get => _selected; set => SetProperty(ref _selected, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

    public void Load()
    {
        IsLoading = true;
        try
        {
            var servers = _reader.Read(_workspace.UserDir, _workspace.ProjectDir);
            var report = _health.Check(_workspace.UserDir, _workspace.ProjectDir);
            View = McpRowsMapper.Map(servers, report);
            if (_selected is not null)
                Selected = View.Rows.FirstOrDefault(r => r.Name == _selected.Name && r.SourceFile == _selected.SourceFile);
            ErrorMessage = null;
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
