using ClaudeExplorer.App.Dashboard;
using ClaudeExplorer.App.Mvvm;

namespace ClaudeExplorer.App.ViewModels;

/// <summary>Loads the dashboard: pull raw inputs from the data source, run the pure computer,
/// expose the result. View binds to <see cref="Data"/> / <see cref="IsLoading"/>.</summary>
public sealed class DashboardViewModel : ObservableObject
{
    private readonly IDashboardDataSource _source;
    private DashboardData? _data;
    private bool _isLoading;

    public DashboardViewModel(IDashboardDataSource source) => _source = source;

    public DashboardData? Data { get => _data; private set => SetProperty(ref _data, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }

    public void Load()
    {
        IsLoading = true;
        try { Data = DashboardComputer.Compute(_source.GetInputs()); }
        finally { IsLoading = false; }
    }
}
