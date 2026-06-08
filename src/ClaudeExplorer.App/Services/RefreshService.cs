namespace ClaudeExplorer.App.Services;

/// <summary>App-wide refresh signal. Chrome raises it; pages/view-model hosts subscribe and reload.</summary>
public sealed class RefreshService
{
    public event Action? Requested;
    public void Request() => Requested?.Invoke();
}
