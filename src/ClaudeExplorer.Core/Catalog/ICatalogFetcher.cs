using System.Net.Http;

namespace ClaudeExplorer.Core.Catalog;

/// <summary>
/// Fetches raw manifest text from a remote source. The ONLY network boundary in the catalog engine;
/// faked in tests so nothing touches the network. Metadata-only — this fetches a manifest, it never
/// downloads or runs an item.
/// </summary>
public interface ICatalogFetcher
{
    /// <summary>The response body for <paramref name="url"/>, or <c>null</c> if the fetch failed.</summary>
    string? FetchText(string url);
}

/// <summary>
/// Real fetcher over HTTP(S). Not unit-tested (it touches the network), mirroring the other
/// <c>Physical*</c> seams. Performs only GET requests with a bounded timeout.
/// </summary>
public sealed class HttpCatalogFetcher : ICatalogFetcher, IDisposable
{
    private readonly HttpClient _http;

    public HttpCatalogFetcher(TimeSpan? timeout = null)
        => _http = new HttpClient { Timeout = timeout ?? TimeSpan.FromSeconds(15) };

    public string? FetchText(string url)
    {
        try
        {
            // ConfigureAwait(false): this sync-over-async runs safely even when called from a
            // UI dispatcher context (Photino/Blazor) — continuations resume off the captured context.
            using var response = _http.GetAsync(url).ConfigureAwait(false).GetAwaiter().GetResult();
            return response.IsSuccessStatusCode
                ? response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult()
                : null;
        }
        catch (HttpRequestException) { return null; }
        catch (TaskCanceledException) { return null; } // includes timeout
    }

    public void Dispose() => _http.Dispose();
}
