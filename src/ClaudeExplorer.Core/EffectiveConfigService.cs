using ClaudeExplorer.Core.Discovery;
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Merge;
using ClaudeExplorer.Core.Model;
using ClaudeExplorer.Core.Reading;

namespace ClaudeExplorer.Core;

/// <summary>
/// Top-level façade: locate settings files for a workspace, parse them, and compute the
/// effective merged configuration with provenance.
/// </summary>
public sealed class EffectiveConfigService
{
    private readonly SettingsLocator _locator;
    private readonly SettingsReader _reader;
    private readonly MergeEngine _engine;

    public EffectiveConfigService(IFileSystem fileSystem)
    {
        _locator = new SettingsLocator(fileSystem);
        _reader = new SettingsReader(fileSystem);
        _engine = new MergeEngine();
    }

    public EffectiveConfig Compute(string userDir, string projectDir, string? enterprisePath = null)
    {
        var files = _locator.Locate(userDir, projectDir, enterprisePath);
        var scopes = files
            .Select(f => new ScopeSettings(f.Scope, f.Path, _reader.Read(f)))
            .ToList();
        return _engine.Compute(scopes);
    }
}
