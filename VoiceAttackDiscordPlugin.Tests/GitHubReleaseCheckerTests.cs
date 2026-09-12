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
    [InlineData("1.2.3.4", "v1.2.3.4", 0)]
    [InlineData("1.2.3", "v1.2.3.4", -1)]
    [InlineData("1.2.3.4", "v1.2.3", 1)]
    [InlineData("1.0", "v9999999999999999999.0", -1)] // absurd components don't throw
    [InlineData("v9999999999999999999.0", "1.0", 1)]
    public void CompareVersions_Behaves(string? current, string? latest, int expected) =>
        Assert.Equal(expected, GitHubReleaseChecker.CompareVersions(current, latest));

    [Theory]
    [InlineData("1.3.2", "v1.4.0", "Update available: v1.4.0 (you have v1.3.2). Download: https://github.com/ARA-ARA-ENJOYER/VoiceAttack-Discord-Plugin/releases/latest")]
    [InlineData("1.4.0", "v1.4.0", "You're up to date (v1.4.0).")]
    [InlineData("1.4.0", "1.4.0", "You're up to date (v1.4.0).")]
    public void BuildUpdateMessage_Announces(string? current, string? latest, string expected) =>
        Assert.Equal(expected, GitHubReleaseChecker.BuildUpdateMessage(current, latest));

    [Theory]
    [InlineData("1.4.0", "v1.3.2")] // dev build ahead of the release: silent
    [InlineData("?", "v1.4.0")]     // unknown current: silent
    [InlineData("1.4.0", null)]     // unknown latest: silent
    [InlineData("1.4.0", "garbage")]
    [InlineData(null, "v1.4.0")]
    public void BuildUpdateMessage_StaysQuiet(string? current, string? latest) =>
        Assert.Null(GitHubReleaseChecker.BuildUpdateMessage(current, latest));

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

    [Fact]
    public async Task FetchLatestVersionAsync_SendsUserAgent()
    {
        var capture = new CaptureHandler();
        using var client = new HttpClient(capture);
        await GitHubReleaseChecker.FetchLatestVersionAsync(client);
        Assert.NotNull(capture.LastRequest);
        Assert.False(string.IsNullOrWhiteSpace(string.Join(" ", capture.LastRequest!.Headers.UserAgent)));
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"tag_name":"v1.4.0"}""", Encoding.UTF8, "application/json")
            });
        }
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
