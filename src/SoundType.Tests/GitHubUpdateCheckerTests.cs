using System.Net;
using System.Text;
using SoundType.Core.Services;

namespace SoundType.Tests;

public sealed class GitHubUpdateCheckerTests
{
    [Theory]
    [InlineData("1.0.0", "v1.0", false)]
    [InlineData("1.0.0", "v1.0.1", true)]
    [InlineData("1.0.0", "2.0.0", true)]
    [InlineData("1.0.0", "v0.9.9", false)]
    public async Task CheckLatestReleaseAsync_ReportsOnlyNewerStableRelease(string currentVersion, string latestTag, bool updateAvailable)
    {
        using HttpClient client = new(new StaticJsonHandler($$"""
            {
              "tag_name": "{{latestTag}}",
              "html_url": "https://github.com/M3RCU3Y/SoundType/releases/tag/{{latestTag}}",
              "name": "SoundType {{latestTag}}",
              "assets": [
                {
                  "name": "SoundType-win-x64-Release-portable.zip",
                  "browser_download_url": "https://github.com/M3RCU3Y/SoundType/releases/download/{{latestTag}}/SoundType-win-x64-Release-portable.zip"
                },
                {
                  "name": "SoundType-win-x64-Release-portable.sha256",
                  "browser_download_url": "https://github.com/M3RCU3Y/SoundType/releases/download/{{latestTag}}/SoundType-win-x64-Release-portable.sha256"
                }
              ]
            }
            """));
        GitHubUpdateChecker checker = new(client);

        UpdateCheckResult result = await checker.CheckLatestReleaseAsync(new Version(currentVersion));

        Assert.Equal(updateAvailable, result.UpdateAvailable);
        Assert.Equal(latestTag, result.LatestTag);
        Assert.Equal("https://github.com/M3RCU3Y/SoundType/releases/tag/" + latestTag, result.ReleaseUrl);
        Assert.Equal("https://github.com/M3RCU3Y/SoundType/releases/download/" + latestTag + "/SoundType-win-x64-Release-portable.zip", result.PortableZipUrl);
        Assert.Equal("https://github.com/M3RCU3Y/SoundType/releases/download/" + latestTag + "/SoundType-win-x64-Release-portable.sha256", result.PortableChecksumUrl);
    }

    [Fact]
    public async Task CheckLatestReleaseAsync_DoesNotOfferUpdate_WhenNewerReleaseHasNoPortableZip()
    {
        using HttpClient client = new(new StaticJsonHandler("""
            {
              "tag_name": "v1.0.1",
              "html_url": "https://github.com/M3RCU3Y/SoundType/releases/tag/v1.0.1",
              "assets": [
                {
                  "name": "SoundType-win-x64-Release-portable.sha256",
                  "browser_download_url": "https://github.com/M3RCU3Y/SoundType/releases/download/v1.0.1/SoundType-win-x64-Release-portable.sha256"
                }
              ]
            }
            """));
        GitHubUpdateChecker checker = new(client);

        UpdateCheckResult result = await checker.CheckLatestReleaseAsync(new Version(1, 0, 0));

        Assert.False(result.UpdateAvailable);
        Assert.Null(result.PortableZipUrl);
        Assert.NotNull(result.PortableChecksumUrl);
        Assert.True(result.CheckSucceeded);
    }

    [Fact]
    public async Task CheckLatestReleaseAsync_DoesNotOfferUpdate_WhenNewerReleaseHasNoPortableChecksum()
    {
        using HttpClient client = new(new StaticJsonHandler("""
            {
              "tag_name": "v1.0.1",
              "html_url": "https://github.com/M3RCU3Y/SoundType/releases/tag/v1.0.1",
              "assets": [
                {
                  "name": "SoundType-win-x64-Release-portable.zip",
                  "browser_download_url": "https://github.com/M3RCU3Y/SoundType/releases/download/v1.0.1/SoundType-win-x64-Release-portable.zip"
                }
              ]
            }
            """));
        GitHubUpdateChecker checker = new(client);

        UpdateCheckResult result = await checker.CheckLatestReleaseAsync(new Version(1, 0, 0));

        Assert.False(result.UpdateAvailable);
        Assert.NotNull(result.PortableZipUrl);
        Assert.Null(result.PortableChecksumUrl);
        Assert.True(result.CheckSucceeded);
    }

    [Fact]
    public async Task CheckLatestReleaseAsync_ReturnsUnavailable_WhenGitHubCannotBeReached()
    {
        using HttpClient client = new(new StaticStatusHandler(HttpStatusCode.NotFound));
        GitHubUpdateChecker checker = new(client);

        UpdateCheckResult result = await checker.CheckLatestReleaseAsync(new Version(1, 0, 0));

        Assert.False(result.UpdateAvailable);
        Assert.False(result.CheckSucceeded);
        Assert.Equal(GitHubUpdateChecker.ReleasesUrl, result.ReleaseUrl);
    }

    private sealed class StaticJsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("https://api.github.com/repos/M3RCU3Y/SoundType/releases/latest", request.RequestUri?.ToString());
            Assert.Contains("SoundType", request.Headers.UserAgent.ToString());

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class StaticStatusHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode));
    }
}
