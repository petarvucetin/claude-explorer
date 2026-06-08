using ClaudeExplorer.App.Environments;

namespace ClaudeExplorer.App.Services;

/// <summary>Adapts the active <see cref="EnvironmentService"/> environment to the existing
/// <see cref="IWorkspaceContext"/> the screens depend on, so switching environment re-points every
/// screen with no per-screen change.</summary>
public sealed class ActiveEnvironmentWorkspaceContext : IWorkspaceContext
{
    private readonly EnvironmentService _service;

    public ActiveEnvironmentWorkspaceContext(EnvironmentService service) => _service = service;

    public string UserDir => _service.Active.UserDir;

    public string ProjectDir => _service.Active.ProjectDir ?? "";

    public string ProjectLabel
    {
        get
        {
            var env = _service.Active;
            if (string.IsNullOrEmpty(env.ProjectDir)) return env.Name;
            var dir = env.ProjectDir.Replace('\\', '/').TrimEnd('/');
            var i = dir.LastIndexOf('/');
            var seg = i >= 0 && i < dir.Length - 1 ? dir[(i + 1)..] : dir;
            return $"{env.Name} · {seg}";
        }
    }
}
