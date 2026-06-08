using ClaudeExplorer.App.Mvvm;
using ClaudeExplorer.App.Services;
using ClaudeExplorer.Core.Plugins;

namespace ClaudeExplorer.App.Screens.Plugins;

/// <summary>Loads the installed-plugin inventory (<see cref="PluginInventoryReader"/>) for the active
/// workspace and exposes marketplaces + plugins + a selected plugin for the detail panel.</summary>
public sealed class PluginsViewModel : ObservableObject
{
    private readonly PluginInventoryReader _reader;
    private readonly IWorkspaceContext _workspace;

    private PluginInventory? _inventory;
    private InstalledPluginInfo? _selected;
    private bool _isLoading;
    private string? _errorMessage;

    public PluginsViewModel(PluginInventoryReader reader, IWorkspaceContext workspace)
    {
        _reader = reader;
        _workspace = workspace;
    }

    public PluginInventory? Inventory { get => _inventory; private set => SetProperty(ref _inventory, value); }
    public InstalledPluginInfo? Selected { get => _selected; set => SetProperty(ref _selected, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

    public void Load()
    {
        IsLoading = true;
        try
        {
            Inventory = _reader.Read(_workspace.UserDir);
            if (_selected is not null)
                Selected = Inventory.Plugins.FirstOrDefault(p => p.Name == _selected.Name && p.Marketplace == _selected.Marketplace);
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
