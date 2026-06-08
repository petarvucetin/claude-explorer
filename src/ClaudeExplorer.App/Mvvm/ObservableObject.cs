using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClaudeExplorer.App.Mvvm;

/// <summary>
/// Minimal MVVM base: implements <see cref="INotifyPropertyChanged"/> so logic-light Blazor views
/// can subscribe to a ViewModel and re-render on change. No external MVVM toolkit (zero extra deps).
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Set <paramref name="field"/> to <paramref name="value"/> and raise
    /// <see cref="PropertyChanged"/> if it changed. Returns true when a change occurred.</summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
