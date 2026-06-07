using ClaudeExplorer.Core.Tests.Fakes;

namespace ClaudeExplorer.Core.Tests.Catalog;

public class FakeCatalogFetcherTests
{
    [Fact]
    public void Returns_canned_text_and_records_the_request()
    {
        var fetcher = new FakeCatalogFetcher().Add("https://x/marketplace.json", "{\"ok\":true}");

        var text = fetcher.FetchText("https://x/marketplace.json");

        Assert.Equal("{\"ok\":true}", text);
        Assert.Equal(new[] { "https://x/marketplace.json" }, fetcher.Requests);
    }

    [Fact]
    public void Unknown_url_returns_null_but_is_still_recorded()
    {
        var fetcher = new FakeCatalogFetcher();
        Assert.Null(fetcher.FetchText("https://missing"));
        Assert.Single(fetcher.Requests);
    }
}
