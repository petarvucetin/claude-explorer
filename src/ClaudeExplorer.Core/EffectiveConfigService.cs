using ClaudeExplorer.Core.Artifacts;
using ClaudeExplorer.Core.Discovery;
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Merge;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Reading;

namespace ClaudeExplorer.Core;

/// <summary>
/// Top-level façade: locate settings files for a workspace, parse them, and compute the
/// effective merged configuration with provenance. Installed plugins' <c>hooks/hooks.json</c> are
/// folded in as a lowest-precedence <see cref="ScopeKind.Plugin"/> layer, so plugin-contributed
/// hooks appear in the effective config (and therefore in the dependency health check).
/// </summary>
public sealed class EffectiveConfigService
{
    private readonly IFileSystem _fs;
    private readonly SettingsLocator _locator;
    private readonly InstalledPluginLocator _pluginLocator;
    private readonly SettingsReader _reader;
    private readonly MergeEngine _engine;

    public EffectiveConfigService(IFileSystem fileSystem)
    {
        _fs = fileSystem;
        _locator = new SettingsLocator(fileSystem);
        _pluginLocator = new InstalledPluginLocator(fileSystem);
        _reader = new SettingsReader(fileSystem);
        _engine = new MergeEngine();
    }

    public EffectiveConfig Compute(string userDir, string projectDir, string? enterprisePath = null)
    {
        var files = _locator.Locate(userDir, projectDir, enterprisePath);
        var scopes = files
            .Select(f => new ScopeSettings(f.Scope, f.Path, _reader.Read(f)))
            .ToList();

        scopes.AddRange(PluginHookScopes(userDir));

        return _engine.Compute(scopes);
    }

    /// <summary>Each installed plugin that ships a <c>hooks/hooks.json</c>, parsed into a
    /// <see cref="ScopeKind.Plugin"/> settings layer. Unreadable/invalid plugin hook files are
    /// skipped rather than failing the whole computation.</summary>
    private IEnumerable<ScopeSettings> PluginHookScopes(string userDir)
    {
        foreach (var plugin in _pluginLocator.Locate(userDir))
        {
            var hooksPath = $"{plugin.RootPath}/hooks/hooks.json";
            if (!_fs.FileExists(hooksPath)) continue;

            ScopeSettings? scope = null;
            try
            {
                scope = new ScopeSettings(ScopeKind.Plugin, hooksPath, _reader.Read(new ConfigFile(ScopeKind.Plugin, hooksPath)));
            }
            catch (SettingsParseException) { }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            if (scope is not null) yield return scope;
        }
    }
}
