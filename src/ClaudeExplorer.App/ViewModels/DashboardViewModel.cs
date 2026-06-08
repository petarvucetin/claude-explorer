using ClaudeExplorer.App.Dashboard;
using ClaudeExplorer.App.Mvvm;

namespace ClaudeExplorer.App.ViewModels;

/// <summary>Loads the dashboard: pull raw inputs from the data source, run the pure computer,
/// expose the result. View binds to <see cref="Data"/> / <see cref="IsLoading"/> / <see cref="ErrorMessage"/>.</summary>
public sealed class DashboardViewModel : ObservableObject
{
    private readonly IDashboardDataSource _source;
    private DashboardData? _data;
    private bool _isLoading;
    private string? _errorMessage;

    public DashboardViewModel(IDashboardDataSource source) => _source = source;

    public DashboardData? Data { get => _data; private set => SetProperty(ref _data, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

    public void Load()
    {
        IsLoading = true;
        try
        {
            Data = DashboardComputer.Compute(_source.GetInputs());
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
