using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SoundType.Core.Services;

public sealed class GitHubUpdateChecker(HttpClient? httpClient = null)
{
    public const string LatestReleaseApiUrl = "https://api.github.com/repos/M3RCU3Y/SoundType/releases/latest";
    public const string ReleasesUrl = "https://github.com/M3RCU3Y/SoundType/releases";

    private readonly HttpClient _httpClient = httpClient ?? CreateHttpClient();

    public async Task<UpdateCheckResult> CheckLatestReleaseAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, LatestReleaseApiUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("SoundType", "1.0"));
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return UpdateCheckResult.Unavailable(ReleasesUrl);
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            GitHubRelease? release = await JsonSerializer.DeserializeAsync<GitHubRelease>(
                stream,
                JsonOptions,
                cancellationToken);

            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            {
                return UpdateCheckResult.Unavailable(ReleasesUrl);
            }

            Version? latestVersion = ParseReleaseVersion(release.TagName);
            string releaseUrl = string.IsNullOrWhiteSpace(release.HtmlUrl) ? ReleasesUrl : release.HtmlUrl;
            string? portableZipUrl = ResolvePortableZipUrl(release.Assets);
            string? portableChecksumUrl = ResolvePortableChecksumUrl(release.Assets);
            if (latestVersion is null)
            {
                return UpdateCheckResult.Unavailable(releaseUrl, release.TagName);
            }

            bool updateAvailable = latestVersion.CompareTo(NormalizeVersion(currentVersion)) > 0 &&
                !string.IsNullOrWhiteSpace(portableZipUrl) &&
                !string.IsNullOrWhiteSpace(portableChecksumUrl);
            return new UpdateCheckResult(true, updateAvailable, release.TagName, releaseUrl, portableZipUrl, portableChecksumUrl);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            return UpdateCheckResult.Unavailable(ReleasesUrl);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SoundType", "1.0"));
        client.Timeout = TimeSpan.FromSeconds(6);
        return client;
    }

    private static Version? ParseReleaseVersion(string tagName)
    {
        string candidate = tagName.Trim();
        if (candidate.StartsWith('v') || candidate.StartsWith('V'))
        {
            candidate = candidate[1..];
        }

        int suffixStart = candidate.IndexOfAny(['-', '+']);
        if (suffixStart >= 0)
        {
            candidate = candidate[..suffixStart];
        }

        return Version.TryParse(candidate, out Version? version)
            ? NormalizeVersion(version)
            : null;
    }

    private static Version NormalizeVersion(Version version) =>
        new(
            Math.Max(version.Major, 0),
            Math.Max(version.Minor, 0),
            Math.Max(version.Build, 0),
            Math.Max(version.Revision, 0));

    private static string? ResolvePortableZipUrl(IReadOnlyList<GitHubReleaseAsset>? assets) =>
        ResolveAssetUrl(assets, "-portable.zip");

    private static string? ResolvePortableChecksumUrl(IReadOnlyList<GitHubReleaseAsset>? assets) =>
        ResolveAssetUrl(assets, "-portable.sha256");

    private static string? ResolveAssetUrl(IReadOnlyList<GitHubReleaseAsset>? assets, string suffix) =>
        assets?
            .Select(asset => new
            {
                Name = asset.Name ?? "",
                Url = asset.BrowserDownloadUrl ?? ""
            })
            .Where(asset =>
                !string.IsNullOrWhiteSpace(asset.Url) &&
                asset.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(asset => asset.Name.Contains("win-x64", StringComparison.OrdinalIgnoreCase))
            .Select(asset => asset.Url)
            .FirstOrDefault();

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubReleaseAsset> Assets { get; set; } = [];
    }

    private sealed class GitHubReleaseAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}

public sealed record UpdateCheckResult(
    bool CheckSucceeded,
    bool UpdateAvailable,
    string? LatestTag,
    string ReleaseUrl,
    string? PortableZipUrl,
    string? PortableChecksumUrl)
{
    public static UpdateCheckResult Unavailable(string releaseUrl, string? latestTag = null) =>
        new(false, false, latestTag, releaseUrl, null, null);
}
