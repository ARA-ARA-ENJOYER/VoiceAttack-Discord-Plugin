using System.Text.Json;
using System.Text.RegularExpressions;

namespace VoiceAttackDiscordPlugin;

/// <summary>
/// Checks the GitHub Releases API for a newer build at startup. Privacy: one
/// anonymous GET to api.github.com per VoiceAttack load; nothing (no token, no
/// user data) is ever sent. Failures stay soft — the caller logs a yellow line.
/// </summary>
public static class GitHubReleaseChecker
{
    public const string ReleasesUrl = "https://github.com/ARA-ARA-ENJOYER/VoiceAttack-Discord-Plugin/releases/latest";
    private const string ApiLatestUrl = "https://api.github.com/repos/ARA-ARA-ENJOYER/VoiceAttack-Discord-Plugin/releases/latest";
    private const string UserAgent = "VoiceAttackDiscordPlugin update checker";

    // Shared, reused connection. Bounded lifetime so a long-lived VoiceAttack
    // session never sticks to a stale-DNS connection.
    private static readonly HttpClient SharedClient;

    static GitHubReleaseChecker()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };
        SharedClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
    }

    public static Task<string?> FetchLatestVersionAsync(CancellationToken ct = default) =>
        FetchLatestVersionAsync(SharedClient, ct);

    // Overload takes the client so tests can inject a fake handler — no network needed.
    public static async Task<string?> FetchLatestVersionAsync(HttpClient client, CancellationToken ct = default)
    {
        // User-Agent per request (not on the client) so it holds for any
        // injected client — and stays testable. GitHub's API requires it.
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiLatestUrl);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        return doc.RootElement.TryGetProperty("tag_name", out var tag)
            ? LatestVersionFromTag(tag.GetString())
            : null;
    }

    /// <summary>Normalizes a release tag ("v1.4.0", "1.4.0-beta", "1.4.0+sha") to "1.4.0", or null.</summary>
    public static string? LatestVersionFromTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var t = tag.Trim();
        if (t.StartsWith('v') || t.StartsWith('V')) t = t[1..];
        // Leading numeric dotted run only ("1.4.0-beta" → "1.4.0").
        var m = Regex.Match(t, @"^\d+(\.\d+)*");
        return m.Success ? m.Value : null;
    }

    /// <summary>
    /// Compares versions: -1 = latest is newer (update available), 0 = equal or
    /// indeterminate, 1 = current is newer (unreleased dev build).
    /// Never reports an update on garbage — indeterminate input means 0.
    /// </summary>
    public static int CompareVersions(string? current, string? latest)
    {
        var cur = Split(LatestVersionFromTag(current));
        var lat = Split(LatestVersionFromTag(latest));
        if (cur.Length == 0 || lat.Length == 0) return 0;
        var len = Math.Max(cur.Length, lat.Length);
        for (var i = 0; i < len; i++)
        {
            var c = i < cur.Length ? cur[i] : 0;
            var l = i < lat.Length ? lat[i] : 0;
            if (c != l) return c < l ? -1 : 1;
        }
        return 0;
    }

    private static long[] Split(string? normalized)
    {
        if (string.IsNullOrEmpty(normalized)) return Array.Empty<long>();
        // TryParse + clamp: stays total on absurd input (a 20-digit component
        // would overflow even long) instead of throwing out of a comparer.
        return normalized.Split('.').Select(p => long.TryParse(p, out var v) ? v : long.MaxValue).ToArray();
    }

    /// <summary>
    /// Builds the user-facing update message core (the caller adds the plugin
    /// name prefix), or null when there is nothing to say: unknown versions,
    /// unreachable release info, or a dev build newer than the release.
    /// Pure — unit-tested.
    /// </summary>
    public static string? BuildUpdateMessage(string? current, string? latest)
    {
        var display = LatestVersionFromTag(current);
        var latestDisplay = LatestVersionFromTag(latest);
        if (display == null) return null; // unknown build — stay quiet
        var compare = CompareVersions(current, latest);
        if (compare < 0)
            return $"Update available: v{latestDisplay} (you have v{display}). Download: {ReleasesUrl}";
        if (compare == 0 && latestDisplay != null)
            return $"You're up to date (v{display}).";
        return null; // indeterminate, or dev build ahead of the release
    }
}
