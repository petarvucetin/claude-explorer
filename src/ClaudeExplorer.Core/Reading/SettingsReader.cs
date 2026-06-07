using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeExplorer.Core.Io;
using ClaudeExplorer.Core.Model;

namespace ClaudeExplorer.Core.Reading;

public sealed class SettingsParseException : Exception
{
    public SettingsParseException(string message, Exception? inner = null) : base(message, inner) { }
}

public sealed class SettingsReader
{
    private static readonly JsonDocumentOptions DocOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IFileSystem _fs;

    public SettingsReader(IFileSystem fs) => _fs = fs;

    public JsonObject Read(ConfigFile file)
    {
        string text = _fs.ReadAllText(file.Path);
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text, nodeOptions: null, documentOptions: DocOptions);
        }
        catch (JsonException ex)
        {
            throw new SettingsParseException($"Invalid JSON in {file.Path}: {ex.Message}", ex);
        }

        if (node is not JsonObject obj)
            throw new SettingsParseException($"Settings root is not a JSON object: {file.Path}");

        return obj;
    }
}
