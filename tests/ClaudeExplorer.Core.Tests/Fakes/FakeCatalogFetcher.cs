using ClaudeExplorer.Core.Catalog;

namespace ClaudeExplorer.Core.Tests.Fakes;

/// <summary>Deterministic catalog fetcher: returns canned manifest text per URL, records every request.</summary>
public sealed class FakeCatalogFetcher : ICatalogFetcher
{
    private readonly Dictionary<string, string> _responses = new(StringComparer.Ordinal);

    /// <summary>Every URL <see cref="FetchText"/> was asked for, in order.</summary>
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
