using ClaudeExplorer.Core.Catalog;

namespace ClaudeExplorer.App.Tests.Fakes;

/// <summary>Deterministic catalog fetcher for App tests. Returns canned manifest text per URL.</summary>
public sealed class FakeCatalogFetcher : ICatalogFetcher
{
    private readonly Dictionary<string, string> _responses = new(StringComparer.Ordinal);

    public List<string> Requests { get; } = new();

    public FakeCatalogFetcher Add(string url, string text)
    {
        _responses[url] = text;
        return this;
    }

    public string? FetchText(string url)
    {
        Requests.Add(url);
        return _responses.TryGetValue(url, out var t) ? t : null;
    }
}
