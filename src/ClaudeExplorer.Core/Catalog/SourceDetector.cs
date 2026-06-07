using System.Text.RegularExpressions;

namespace ClaudeExplorer.Core.Catalog;

/// <summary>
/// Detects the type of a user-added source string and normalizes it into a <see cref="CatalogSource"/>
/// (always Community trust). Recognizes a github.com URL, a bare <c>owner/repo</c>, or any other
/// http(s) URL. <see cref="CatalogSource.Location"/> is the manifest URL to fetch.
/// </summary>
public static class SourceDetector
{
    private static readonly Regex OwnerRepo =
        new(@"^[A-Za-z0-9._-]+/[A-Za-z0-9._-]+$", RegexOptions.Compiled);

    private static readonly Regex GitHubUrl = new(
        @"^https?://github\.com/(?<owner>[A-Za-z0-9._-]+)/(?<repo>[A-Za-z0-9._-]+?)(?:\.git)?/?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static CatalogSource Detect(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new FormatException("Source is empty.");
        var s = input.Trim();

        var gh = GitHubUrl.Match(s);
        if (gh.Success)
            return GitHub(gh.Groups["owner"].Value, gh.Groups["repo"].Value);

        if (OwnerRepo.IsMatch(s))
        {
            var parts = s.Split('/');
            return GitHub(parts[0], parts[1]);
        }

        if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return new CatalogSource(CatalogSourceKind.Url, TrustLevel.Community, s, ManifestUrlFor(s));

        throw new FormatException(
            $"Unrecognized source: '{input}'. Expected owner/repo, a github.com URL, or an http(s) URL.");
    }

    /// <summary>Raw URL of a GitHub repo's <c>.claude-plugin/marketplace.json</c> at HEAD.</summary>
    public static string RawGitHubManifestUrl(string owner, string repo)
        => $"https://raw.githubusercontent.com/{owner}/{repo}/HEAD/.claude-plugin/marketplace.json";

    private static CatalogSource GitHub(string owner, string repo)
        => new(CatalogSourceKind.GitHub, TrustLevel.Community, $"{owner}/{repo}",
            RawGitHubManifestUrl(owner, repo));

    private static string ManifestUrlFor(string url)
        => url.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? url
            : $"{url.TrimEnd('/')}/.claude-plugin/marketplace.json";
}
