using ClaudeExplorer.App.Mvvm;
using ClaudeExplorer.App.Services;
using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.App.Screens.Memory;

/// <summary>Loads the CLAUDE.md memory files for the active workspace (global + project + nested) and
/// exposes them as rows with a selected file for the detail/viewer pane.</summary>
public sealed class MemoryViewModel : ObservableObject
{
    private readonly IFileSystem _fs;
    private readonly IWorkspaceContext _workspace;

    private IReadOnlyList<MemoryRow> _rows = Array.Empty<MemoryRow>();
    private MemoryRow? _selected;
    private bool _isLoading;
    private string? _errorMessage;

    public MemoryViewModel(IFileSystem fs, IWorkspaceContext workspace)
    {
        _fs = fs;
        _workspace = workspace;
    }

    public IReadOnlyList<MemoryRow> Rows { get => _rows; private set => SetProperty(ref _rows, value); }
    public MemoryRow? Selected { get => _selected; set => SetProperty(ref _selected, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

    public void Load()
    {
        IsLoading = true;
        try
        {
            Rows = MemoryRowsMapper.Discover(_fs, _workspace.UserDir, _workspace.ProjectDir);
            if (_selected is not null)
                Selected = Rows.FirstOrDefault(r => r.Path == _selected.Path);
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
