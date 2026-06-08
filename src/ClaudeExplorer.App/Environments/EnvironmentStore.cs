using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeExplorer.Core.Io;

namespace ClaudeExplorer.App.Environments;

/// <summary>Persisted UI state: the active environment id, user-added custom environments, and a
/// per-environment active-project map.</summary>
public sealed class EnvironmentState
{
    public string? ActiveId { get; init; }
    public List<ClaudeEnvironment> Custom { get; init; } = new();
    public Dictionary<string, string> Projects { get; init; } = new();

    [JsonConstructor]
    public EnvironmentState() { }

    public EnvironmentState(string? ActiveId, IEnumerable<ClaudeEnvironment> Custom, IDictionary<string, string> Projects)
    {
        this.ActiveId = ActiveId;
        this.Custom = new List<ClaudeEnvironment>(Custom);
        this.Projects = new Dictionary<string, string>(Projects);
    }

    public static EnvironmentState Empty { get; } = new(null, Array.Empty<ClaudeEnvironment>(), new Dictionary<string, string>());
}

/// <summary>Reads/writes <see cref="EnvironmentState"/> as JSON. Tolerant: missing or garbled file →
/// <see cref="EnvironmentState.Empty"/>.</summary>
public sealed class EnvironmentStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly IFileSystem _fs;
    private readonly IFileWriter _writer;
    private readonly string _path;

    public EnvironmentStore(IFileSystem fs, IFileWriter writer, string path)
    {
        _fs = fs;
        _writer = writer;
        _path = path;
    }

    public EnvironmentState Load()
    {
        if (!_fs.FileExists(_path)) return EnvironmentState.Empty;
        try
        {
            return JsonSerializer.Deserialize<EnvironmentState>(_fs.ReadAllText(_path), Options) ?? EnvironmentState.Empty;
        }
        catch (JsonException)
        {
            return EnvironmentState.Empty;
        }
    }

    public void Save(EnvironmentState state)
        => _writer.WriteAllText(_path, JsonSerializer.Serialize(state, Options));
}
