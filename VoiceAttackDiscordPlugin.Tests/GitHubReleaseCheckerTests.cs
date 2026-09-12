using System.Net;
using System.Text;
using VoiceAttackDiscordPlugin;
using Xunit;

namespace VoiceAttackDiscordPlugin.Tests;

public class GitHubReleaseCheckerTests
{
    [Theory]
    [InlineData("v1.4.0", "1.4.0")]
    [InlineData("V1.4.0", "1.4.0")]
    [InlineData("1.3.2", "1.3.2")]
    [InlineData("  v2.0.1  ", "2.0.1")]
    [InlineData("1.4.0-beta.1", "1.4.0")]
    [InlineData("1.4.0+abc123", "1.4.0")]
    public void LatestVersionFromTag_NormalizesTags(string tag, string expected) =>
        Assert.Equal(expected, GitHubReleaseChecker.LatestVersionFromTag(tag));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("v")]
    [InlineData("release-x")]
    public void LatestVersionFromTag_RejectsGarbage(string? tag) =>
        Assert.Null(GitHubReleaseChecker.LatestVersionFromTag(tag));

    [Theory]
    [InlineData("1.3.2", "v1.3.2", 0)]
    [InlineData("1.3.2", "v1.4.0", -1)]
    [InlineData("1.4.0", "v1.3.2", 1)]
    [InlineData("1.9.9", "v1.10.0", -1)] // numeric, not lexical
    [InlineData("1.3", "v1.3.0", 0)]     // missing components = 0
    [InlineData("?", "v1.4.0", 0)]       // unknown current: never cry update
    [InlineData("1.4.0", null, 0)]       // unknown latest: stay quiet
    [InlineData("1.4.0", "garbage", 0)]
    public void CompareVersions_Behaves(string? current, string? latest, int expected) =>
        Assert.Equal(expected, GitHubReleaseChecker.CompareVersions(current, latest));

    [Fact]
    public async Task FetchLatestVersionAsync_ParsesTagName()
    {
        using var client = new HttpClient(new FakeHandler("""{"tag_name":"v1.4.0","name":"x"}"""));
        Assert.Equal("1.4.0", await GitHubReleaseChecker.FetchLatestVersionAsync(client));
    }

    [Fact]
    public async Task FetchLatestVersionAsync_ReturnsNullWithoutTag()
    {
        using var client = new HttpClient(new FakeHandler("""{"name":"x"}"""));
        Assert.Null(await GitHubReleaseChecker.FetchLatestVersionAsync(client));
    }

    [Fact]
    public async Task FetchLatestVersionAsync_ThrowsOnHttpError()
    {
        using var client = new HttpClient(new FakeHandler("{}", HttpStatusCode.NotFound));
        await Assert.ThrowsAsync<HttpRequestException>(() => GitHubReleaseChecker.FetchLatestVersionAsync(client));
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly HttpStatusCode _status;

        public FakeHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            _body = body;
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
    }
}
